using UnityEngine;
using Crest;

/// <summary>
/// ENHANCED: PlayerWaterDetector that gracefully handles scenes with and without water.
/// Automatically detects if a scene has water and disables water detection for non-water scenes.
/// Prevents errors when transitioning between water and non-water scenes.
/// </summary>
public class PlayerWaterDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask waterLayerMask = -1;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

    [Header("ENHANCED: Scene Compatibility")]
    [SerializeField] private bool allowScenesWithoutWater = true;
    [SerializeField] private bool forceWaterDetectionOff = false; // For testing non-water scenes
    [SerializeField] private float sceneWaterCheckDelay = 0.5f; // Delay before checking for water in new scenes

    [Header("Detection Points")]
    [SerializeField] private Transform feetPoint;
    [SerializeField] private Transform chestPoint;
    [SerializeField] private Transform headPoint;

    [Header("Auto-Setup")]
    [SerializeField] private bool autoCreateDetectionPoints = true;
    [SerializeField] private float feetOffset = 0.1f;
    [SerializeField] private float chestOffset = 1.0f;
    [SerializeField] private float headOffset = 1.8f;

    [Header("Water Transition Settings")]
    [SerializeField] private float waterEntryThreshold = 0.1f;
    [SerializeField] private float waterExitThreshold = 0.05f;
    [SerializeField] private float underwaterThreshold = 0.1f;

    // ENHANCED: Scene water capability tracking
    private bool sceneHasWater = false;
    private bool hasCheckedForWater = false;
    private bool isWaterSystemInitialized = false;

    // Crest water sampling - separate helpers for each detection point
    private SampleHeightHelper feetSampleHelper;
    private SampleHeightHelper chestSampleHelper;
    private SampleHeightHelper headSampleHelper;
    private SampleHeightHelper generalSampleHelper;
    private OceanRenderer oceanRenderer;

    // Optimization: cache initialization state to avoid multiple null checks per frame
    private bool isWaterDetectionReady = false;

    // Water state tracking
    private bool isInWater = false;
    private bool isHeadUnderwater = false;
    private bool wasInWater = false;
    private bool wasHeadUnderwater = false;

    // Current water data
    private float waterHeightAtFeet;
    private float waterHeightAtChest;
    private float waterHeightAtHead;
    private float feetDepth;
    private float chestDepth;
    private float headDepth;

    // Events for PlayerController
    public event System.Action OnWaterEntered;
    public event System.Action OnWaterExited;
    public event System.Action OnHeadSubmerged;
    public event System.Action OnHeadSurfaced;

    // ENHANCED: Public properties with scene awareness
    public bool IsInWater => sceneHasWater && isInWater;
    public bool IsHeadUnderwater => sceneHasWater && isHeadUnderwater;
    public float FeetDepth => sceneHasWater ? feetDepth : 0f;
    public float ChestDepth => sceneHasWater ? chestDepth : 0f;
    public float HeadDepth => sceneHasWater ? headDepth : 0f;
    public float WaterHeightAtPosition => sceneHasWater ? waterHeightAtFeet : 0f;

    // ENHANCED: Additional properties for system integration
    public bool SceneHasWater => sceneHasWater;
    public bool IsWaterDetectionEnabled => isWaterDetectionReady && sceneHasWater;

    private void Awake()
    {
        SetupDetectionPoints();
        // Don't initialize Crest components immediately - wait for scene to fully load
    }

    private void Start()
    {
        // ENHANCED: Delayed water system check to allow scene to fully load
        StartCoroutine(DelayedWaterSystemInitialization());
    }

    /// <summary>
    /// ENHANCED: Delayed initialization to handle scene loading properly
    /// </summary>
    private System.Collections.IEnumerator DelayedWaterSystemInitialization()
    {
        // Wait for scene to fully load
        yield return new WaitForSecondsRealtime(sceneWaterCheckDelay);

        // Check if this scene has water
        CheckSceneForWater();

        if (sceneHasWater && !forceWaterDetectionOff)
        {
            InitializeCrestComponents();
        }
        else
        {
            InitializeNonWaterScene();
        }

        ValidateSetup();
        hasCheckedForWater = true;

        DebugLog($"Water detection initialization complete - Scene has water: {sceneHasWater}");
    }

    /// <summary>
    /// ENHANCED: Checks if the current scene has water systems
    /// </summary>
    private void CheckSceneForWater()
    {
        DebugLog("Checking scene for water systems...");

        // Check for OceanRenderer (Crest water system)
        oceanRenderer = FindFirstObjectByType<OceanRenderer>();

        // Could also check for other water systems here if needed
        // var otherWaterSystem = FindFirstObjectByType<OtherWaterSystemComponent>();

        sceneHasWater = oceanRenderer != null && !forceWaterDetectionOff;

        if (sceneHasWater)
        {
            DebugLog("Scene has water - enabling water detection");
        }
        else
        {
            DebugLog("Scene has no water - disabling water detection");
        }
    }

    /// <summary>
    /// ENHANCED: Initializes for scenes without water
    /// </summary>
    private void InitializeNonWaterScene()
    {
        DebugLog("Initializing for non-water scene");

        // Clear any previous water state
        isInWater = false;
        isHeadUnderwater = false;
        wasInWater = false;
        wasHeadUnderwater = false;

        // Reset depth values
        feetDepth = 0f;
        chestDepth = 0f;
        headDepth = 0f;

        // Mark as "ready" but with no water detection
        isWaterDetectionReady = false;
        isWaterSystemInitialized = true;

        DebugLog("Non-water scene initialization complete");
    }

    private void Update()
    {
        // ENHANCED: Only run water detection if scene has water
        if (isWaterDetectionReady && sceneHasWater)
        {
            UpdateWaterDetection();
            CheckWaterStateChanges();
        }
        // ENHANCED: For non-water scenes, ensure player isn't flagged as in water
        else if (hasCheckedForWater && !sceneHasWater)
        {
            EnsureNotInWater();
        }
    }

    /// <summary>
    /// ENHANCED: Ensures player is not flagged as in water for non-water scenes
    /// </summary>
    private void EnsureNotInWater()
    {
        if (isInWater || isHeadUnderwater)
        {
            DebugLog("Clearing water state for non-water scene");

            bool wasPlayerInWater = isInWater;
            bool wasPlayerHeadUnder = isHeadUnderwater;

            isInWater = false;
            isHeadUnderwater = false;

            // Trigger exit events if player was previously in water
            if (wasPlayerInWater)
            {
                OnWaterExited?.Invoke();
            }

            if (wasPlayerHeadUnder)
            {
                OnHeadSurfaced?.Invoke();
            }
        }
    }

    /// <summary>
    /// Sets up detection points automatically if they don't exist
    /// </summary>
    private void SetupDetectionPoints()
    {
        if (!autoCreateDetectionPoints) return;

        // Create detection points if they don't exist
        if (feetPoint == null)
        {
            GameObject feetObj = new GameObject("FeetDetectionPoint");
            feetObj.transform.SetParent(transform);
            feetObj.transform.localPosition = Vector3.up * feetOffset;
            feetPoint = feetObj.transform;
        }

        if (chestPoint == null)
        {
            GameObject chestObj = new GameObject("ChestDetectionPoint");
            chestObj.transform.SetParent(transform);
            chestObj.transform.localPosition = Vector3.up * chestOffset;
            chestPoint = chestObj.transform;
        }

        if (headPoint == null)
        {
            GameObject headObj = new GameObject("HeadDetectionPoint");
            headObj.transform.SetParent(transform);
            headObj.transform.localPosition = Vector3.up * headOffset;
            headPoint = headObj.transform;
        }

        DebugLog("Detection points set up automatically");
    }

    /// <summary>
    /// ENHANCED: Initialize Crest water system components with error handling
    /// </summary>
    private void InitializeCrestComponents()
    {
        DebugLog("Initializing Crest water system components");

        if (oceanRenderer == null)
        {
            Debug.LogError("[PlayerWaterDetector] OceanRenderer is null during Crest initialization!");
            InitializeNonWaterScene();
            return;
        }

        try
        {
            // Initialize separate water height sampling helpers for each detection point
            feetSampleHelper = new SampleHeightHelper();
            chestSampleHelper = new SampleHeightHelper();
            headSampleHelper = new SampleHeightHelper();
            generalSampleHelper = new SampleHeightHelper();

            // Test the helpers with a simple initialization
            Vector3 testPosition = transform.position;
            feetSampleHelper.Init(testPosition, 0f, false, this);

            // If we get here without exception, water system is working
            isWaterDetectionReady = true;
            isWaterSystemInitialized = true;

            DebugLog("Crest components initialized successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Failed to initialize Crest components: {e.Message}");
            InitializeNonWaterScene();
        }
    }

    /// <summary>
    /// ENHANCED: Validates setup with scene awareness
    /// </summary>
    private void ValidateSetup()
    {
        bool isValid = true;

        if (feetPoint == null)
        {
            Debug.LogError("[PlayerWaterDetector] Feet detection point is not assigned!");
            isValid = false;
        }

        if (chestPoint == null)
        {
            Debug.LogError("[PlayerWaterDetector] Chest detection point is not assigned!");
            isValid = false;
        }

        if (headPoint == null)
        {
            Debug.LogError("[PlayerWaterDetector] Head detection point is not assigned!");
            isValid = false;
        }

        // ENHANCED: Different validation for water vs non-water scenes
        if (sceneHasWater && !forceWaterDetectionOff)
        {
            if (!isWaterDetectionReady)
            {
                Debug.LogError("[PlayerWaterDetector] Water scene detected but water detection not ready!");
                isValid = false;
            }
            else
            {
                DebugLog("Water detector validation passed for water scene");
            }
        }
        else
        {
            if (allowScenesWithoutWater)
            {
                DebugLog("Water detector validation passed for non-water scene");
            }
            else
            {
                Debug.LogWarning("[PlayerWaterDetector] Non-water scene detected but allowScenesWithoutWater is false");
            }
        }

        if (!isValid)
        {
            Debug.LogError("[PlayerWaterDetector] Validation failed");
            isWaterDetectionReady = false;
        }
    }

    /// <summary>
    /// Updates water height sampling at all detection points
    /// </summary>
    private void UpdateWaterDetection()
    {
        if (feetPoint != null && feetSampleHelper != null)
        {
            waterHeightAtFeet = SampleWaterHeightAtPosition(feetPoint.position, feetSampleHelper);
            feetDepth = Mathf.Max(0f, waterHeightAtFeet - feetPoint.position.y);
        }

        if (chestPoint != null && chestSampleHelper != null)
        {
            waterHeightAtChest = SampleWaterHeightAtPosition(chestPoint.position, chestSampleHelper);
            chestDepth = Mathf.Max(0f, waterHeightAtChest - chestPoint.position.y);
        }

        if (headPoint != null && headSampleHelper != null)
        {
            waterHeightAtHead = SampleWaterHeightAtPosition(headPoint.position, headSampleHelper);
            headDepth = Mathf.Max(0f, waterHeightAtHead - headPoint.position.y);
        }

        // Update water state based on depth thresholds
        UpdateWaterStates();
    }

    /// <summary>
    /// Updates the current water states based on detection point depths
    /// </summary>
    private void UpdateWaterStates()
    {
        // Store previous states
        wasInWater = isInWater;
        wasHeadUnderwater = isHeadUnderwater;

        // Determine if player is in water based on feet depth with hysteresis
        if (!isInWater)
        {
            // Enter water when feet are deep enough
            isInWater = feetDepth > waterEntryThreshold;
        }
        else
        {
            // Exit water when feet are shallow enough (prevents rapid toggling)
            isInWater = feetDepth > waterExitThreshold;
        }

        // Determine if head is underwater
        isHeadUnderwater = headDepth > underwaterThreshold;
    }

    /// <summary>
    /// Checks for water state changes and triggers appropriate events
    /// </summary>
    private void CheckWaterStateChanges()
    {
        // Check for water entry/exit
        if (isInWater != wasInWater)
        {
            if (isInWater)
            {
                DebugLog($"Player entered water - Feet depth: {feetDepth:F2}m");
                OnWaterEntered?.Invoke();
            }
            else
            {
                DebugLog($"Player exited water - Feet depth: {feetDepth:F2}m");
                OnWaterExited?.Invoke();
            }
        }

        // Check for head submersion changes
        if (isHeadUnderwater != wasHeadUnderwater)
        {
            if (isHeadUnderwater)
            {
                DebugLog($"Player head submerged - Head depth: {headDepth:F2}m");
                OnHeadSubmerged?.Invoke();
            }
            else
            {
                DebugLog($"Player head surfaced - Head depth: {headDepth:F2}m");
                OnHeadSurfaced?.Invoke();
            }
        }
    }

    /// <summary>
    /// ENHANCED: Safe water height sampling with error handling
    /// </summary>
    private float SampleWaterHeightAtPosition(Vector3 worldPosition, SampleHeightHelper helper)
    {
        if (helper == null || oceanRenderer == null || !sceneHasWater)
            return 0f;

        try
        {
            // Initialize the helper for this query
            helper.Init(worldPosition, 0f, false, this);

            // Sample the water height
            float waterHeight;
            bool success = helper.Sample(out waterHeight);

            if (success)
            {
                return waterHeight;
            }

            // Return sea level if sampling failed
            return oceanRenderer.SeaLevel;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Error sampling water height: {e.Message}");
            return 0f;
        }
    }

    /// <summary>
    /// ENHANCED: Safe water depth calculation
    /// </summary>
    public float GetWaterDepthAtPosition(Vector3 worldPosition)
    {
        if (!sceneHasWater || !isWaterDetectionReady)
            return 0f;

        float waterHeight = SampleWaterHeightAtPosition(worldPosition, generalSampleHelper);
        return Mathf.Max(0f, waterHeight - worldPosition.y);
    }

    /// <summary>
    /// ENHANCED: Safe water surface normal calculation
    /// </summary>
    public Vector3 GetWaterSurfaceNormal()
    {
        if (oceanRenderer == null || !isInWater || feetSampleHelper == null || !sceneHasWater)
            return Vector3.up;

        try
        {
            // Sample water height and normal at feet position
            feetSampleHelper.Init(feetPoint.position, 0f, false, this);

            float waterHeight;
            Vector3 waterNormal;
            bool success = feetSampleHelper.Sample(out waterHeight, out waterNormal);

            if (success)
            {
                return waterNormal;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerWaterDetector] Error getting water surface normal: {e.Message}");
        }

        return Vector3.up;
    }

    /// <summary>
    /// ENHANCED: Safe water state check
    /// </summary>
    public void ForceWaterStateCheck()
    {
        if (isWaterDetectionReady && sceneHasWater)
        {
            UpdateWaterDetection();
            CheckWaterStateChanges();
        }
        else if (!sceneHasWater)
        {
            EnsureNotInWater();
        }
    }

    /// <summary>
    /// ENHANCED: Water state info with scene awareness
    /// </summary>
    public string GetWaterStateInfo()
    {
        if (!sceneHasWater)
        {
            return "Water State - Scene has no water, water detection disabled";
        }

        return $"Water State - InWater: {isInWater}, HeadUnder: {isHeadUnderwater}, " +
               $"FeetDepth: {feetDepth:F2}m, ChestDepth: {chestDepth:F2}m, HeadDepth: {headDepth:F2}m, " +
               $"SceneHasWater: {sceneHasWater}";
    }

    /// <summary>
    /// ENHANCED: Manual scene water refresh (useful when water is added/removed at runtime)
    /// </summary>
    public void RefreshSceneWaterDetection()
    {
        DebugLog("Manually refreshing scene water detection");
        hasCheckedForWater = false;
        isWaterSystemInitialized = false;
        StartCoroutine(DelayedWaterSystemInitialization());
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerWaterDetector] {message}");
        }
    }

    #region Gizmos and Visual Debug

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // ENHANCED: Different gizmo colors for water vs non-water scenes
        Color sceneColor = sceneHasWater ? Color.white : Color.gray;

        // Draw detection points
        if (feetPoint != null)
        {
            Gizmos.color = sceneHasWater ? (isInWater ? Color.blue : Color.gray) : Color.red;
            Gizmos.DrawWireSphere(feetPoint.position, 0.1f);

            // Draw water level at feet
            if (Application.isPlaying && isInWater && sceneHasWater)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(new Vector3(feetPoint.position.x, waterHeightAtFeet, feetPoint.position.z), 0.05f);
            }
        }

        if (chestPoint != null)
        {
            Gizmos.color = sceneHasWater ? (chestDepth > 0 ? Color.blue : Color.gray) : Color.red;
            Gizmos.DrawWireSphere(chestPoint.position, 0.08f);
        }

        if (headPoint != null)
        {
            if (sceneHasWater)
            {
                Gizmos.color = isHeadUnderwater ? Color.red : (headDepth > 0 ? Color.blue : Color.gray);
            }
            else
            {
                Gizmos.color = Color.red;
            }
            Gizmos.DrawWireSphere(headPoint.position, 0.06f);
        }

        // Draw connection lines
        if (feetPoint != null && chestPoint != null)
        {
            Gizmos.color = sceneColor;
            Gizmos.DrawLine(feetPoint.position, chestPoint.position);
        }

        if (chestPoint != null && headPoint != null)
        {
            Gizmos.color = sceneColor;
            Gizmos.DrawLine(chestPoint.position, headPoint.position);
        }

        // Draw depth indicators in play mode (only for water scenes)
        if (Application.isPlaying && sceneHasWater)
        {
            DrawDepthIndicator(feetPoint, feetDepth, Color.blue);
            DrawDepthIndicator(chestPoint, chestDepth, Color.cyan);
            DrawDepthIndicator(headPoint, headDepth, isHeadUnderwater ? Color.red : Color.magenta);
        }

#if UNITY_EDITOR
        // ENHANCED: Labels show scene water status
        string sceneStatus = sceneHasWater ? "Water Scene" : "No Water";

        if (feetPoint != null)
        {
            string depthText = Application.isPlaying && sceneHasWater ? feetDepth.ToString("F2") + "m" : sceneStatus;
            UnityEditor.Handles.Label(feetPoint.position + Vector3.right * 0.2f, $"Feet: {depthText}");
        }

        if (chestPoint != null)
        {
            string depthText = Application.isPlaying && sceneHasWater ? chestDepth.ToString("F2") + "m" : sceneStatus;
            UnityEditor.Handles.Label(chestPoint.position + Vector3.right * 0.2f, $"Chest: {depthText}");
        }

        if (headPoint != null)
        {
            string depthText = Application.isPlaying && sceneHasWater ? headDepth.ToString("F2") + "m" : sceneStatus;
            UnityEditor.Handles.Label(headPoint.position + Vector3.right * 0.2f, $"Head: {depthText}");
        }
#endif
    }

    private void DrawDepthIndicator(Transform point, float depth, Color color)
    {
        if (point == null || depth <= 0) return;

        Gizmos.color = color;
        Vector3 start = point.position;
        Vector3 end = start + Vector3.down * depth;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.03f);
    }

    #endregion

    #region Editor Helpers

#if UNITY_EDITOR
    [ContextMenu("Refresh Water Detection")]
    private void EditorRefreshWaterDetection()
    {
        if (Application.isPlaying)
        {
            RefreshSceneWaterDetection();
        }
        else
        {
            Debug.Log("Water detection refresh only works in play mode");
        }
    }

    [ContextMenu("Test Water Entry")]
    private void TestWaterEntry()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Water entry test only works in play mode");
            return;
        }

        if (!sceneHasWater)
        {
            Debug.LogWarning("Cannot test water entry - scene has no water");
            return;
        }

        Debug.Log("Forcing water entry event");
        OnWaterEntered?.Invoke();
    }

    [ContextMenu("Test Water Exit")]
    private void TestWaterExit()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Water exit test only works in play mode");
            return;
        }

        Debug.Log("Forcing water exit event");
        OnWaterExited?.Invoke();
    }

    [ContextMenu("Log Current Water State")]
    private void LogCurrentWaterState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Water state logging only works in play mode");
            return;
        }

        Debug.Log(GetWaterStateInfo());
    }

    [ContextMenu("Force No Water Scene")]
    private void ForceNoWaterScene()
    {
        forceWaterDetectionOff = true;
        if (Application.isPlaying)
        {
            RefreshSceneWaterDetection();
        }
        Debug.Log("Forced water detection off for testing");
    }

    [ContextMenu("Re-enable Water Detection")]
    private void ReEnableWaterDetection()
    {
        forceWaterDetectionOff = false;
        if (Application.isPlaying)
        {
            RefreshSceneWaterDetection();
        }
        Debug.Log("Re-enabled water detection");
    }
#endif

    #endregion
}