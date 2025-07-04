using UnityEngine;
using Crest;

/// <summary>
/// Detects when the player enters/exits water using Crest Water System API.
/// Handles water level detection relative to player body parts (feet, chest, head).
/// Triggers appropriate events for PlayerController to switch movement modes.
/// </summary>
public class PlayerWaterDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask waterLayerMask = -1;
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugGizmos = true;

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
    [SerializeField] private float waterEntryThreshold = 0.1f;    // How deep feet must be to enter swimming
    [SerializeField] private float waterExitThreshold = 0.05f;    // How shallow feet must be to exit swimming
    [SerializeField] private float underwaterThreshold = 0.1f;    // How deep head must be to be "underwater"

    // Crest water sampling - separate helpers for each detection point
    private SampleHeightHelper feetSampleHelper;
    private SampleHeightHelper chestSampleHelper;
    private SampleHeightHelper headSampleHelper;
    private SampleHeightHelper generalSampleHelper; // For GetWaterDepthAtPosition calls
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

    // Public properties for other systems
    public bool IsInWater => isInWater;
    public bool IsHeadUnderwater => isHeadUnderwater;
    public float FeetDepth => feetDepth;
    public float ChestDepth => chestDepth;
    public float HeadDepth => headDepth;
    public float WaterHeightAtPosition => waterHeightAtFeet;

    private void Awake()
    {
        SetupDetectionPoints();
        InitializeCrestComponents();
    }

    private void Start()
    {
        ValidateSetup();
    }

    private void Update()
    {
        if (isWaterDetectionReady)
        {
            UpdateWaterDetection();
            CheckWaterStateChanges();
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
    /// Initialize Crest water system components
    /// </summary>
    private void InitializeCrestComponents()
    {
        // Find OceanRenderer in the scene
        oceanRenderer = FindFirstObjectByType<OceanRenderer>();
        if (oceanRenderer == null)
        {
            Debug.LogWarning("[PlayerWaterDetector] No OceanRenderer found in scene. Water detection will not work.");
            isWaterDetectionReady = false;
            return;
        }

        // Initialize separate water height sampling helpers for each detection point
        feetSampleHelper = new SampleHeightHelper();
        chestSampleHelper = new SampleHeightHelper();
        headSampleHelper = new SampleHeightHelper();
        generalSampleHelper = new SampleHeightHelper();

        // Cache readiness state
        isWaterDetectionReady = true;

        DebugLog("Crest components initialized successfully with multiple sample helpers");
    }

    /// <summary>
    /// Validates that all required components are set up correctly
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

        if (!isWaterDetectionReady)
        {
            Debug.LogError("[PlayerWaterDetector] Crest water detection not ready! Check OceanRenderer setup.");
            isValid = false;
        }

        if (isValid)
        {
            DebugLog("Water detector validation passed");
        }
        else
        {
            Debug.LogError("[PlayerWaterDetector] Validation failed - water detection will not work properly");
            isWaterDetectionReady = false; // Disable if validation fails
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
    /// Samples water height at a specific world position using Crest API with specified helper
    /// </summary>
    private float SampleWaterHeightAtPosition(Vector3 worldPosition, SampleHeightHelper helper)
    {
        if (helper == null || oceanRenderer == null)
            return 0f;

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

    /// <summary>
    /// Gets the water depth at a specific world position
    /// </summary>
    public float GetWaterDepthAtPosition(Vector3 worldPosition)
    {
        float waterHeight = SampleWaterHeightAtPosition(worldPosition, generalSampleHelper);
        return Mathf.Max(0f, waterHeight - worldPosition.y);
    }

    /// <summary>
    /// Gets the water surface normal at the player's position (useful for swimming physics)
    /// </summary>
    public Vector3 GetWaterSurfaceNormal()
    {
        if (oceanRenderer == null || !isInWater || feetSampleHelper == null)
            return Vector3.up;

        // Sample water height and normal at feet position
        feetSampleHelper.Init(feetPoint.position, 0f, false, this);

        float waterHeight;
        Vector3 waterNormal;
        bool success = feetSampleHelper.Sample(out waterHeight, out waterNormal);

        if (success)
        {
            return waterNormal;
        }

        return Vector3.up;
    }

    /// <summary>
    /// Forces a water state check (useful for testing or initialization)
    /// </summary>
    public void ForceWaterStateCheck()
    {
        if (isWaterDetectionReady)
        {
            UpdateWaterDetection();
            CheckWaterStateChanges();
        }
    }

    /// <summary>
    /// Returns detailed water state information for debugging
    /// </summary>
    public string GetWaterStateInfo()
    {
        return $"Water State - InWater: {isInWater}, HeadUnder: {isHeadUnderwater}, " +
               $"FeetDepth: {feetDepth:F2}m, ChestDepth: {chestDepth:F2}m, HeadDepth: {headDepth:F2}m";
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

        // Draw detection points
        if (feetPoint != null)
        {
            Gizmos.color = isInWater ? Color.blue : Color.gray;
            Gizmos.DrawWireSphere(feetPoint.position, 0.1f);

            // Draw water level at feet
            if (Application.isPlaying && isInWater)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(new Vector3(feetPoint.position.x, waterHeightAtFeet, feetPoint.position.z), 0.05f);
            }
        }

        if (chestPoint != null)
        {
            Gizmos.color = chestDepth > 0 ? Color.blue : Color.gray;
            Gizmos.DrawWireSphere(chestPoint.position, 0.08f);
        }

        if (headPoint != null)
        {
            Gizmos.color = isHeadUnderwater ? Color.red : (headDepth > 0 ? Color.blue : Color.gray);
            Gizmos.DrawWireSphere(headPoint.position, 0.06f);
        }

        // Draw connection lines
        if (feetPoint != null && chestPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(feetPoint.position, chestPoint.position);
        }

        if (chestPoint != null && headPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(chestPoint.position, headPoint.position);
        }

        // Draw depth indicators in play mode
        if (Application.isPlaying)
        {
            DrawDepthIndicator(feetPoint, feetDepth, Color.blue);
            DrawDepthIndicator(chestPoint, chestDepth, Color.cyan);
            DrawDepthIndicator(headPoint, headDepth, isHeadUnderwater ? Color.red : Color.magenta);
        }

#if UNITY_EDITOR
        // Draw labels
        if (feetPoint != null)
            UnityEditor.Handles.Label(feetPoint.position + Vector3.right * 0.2f,
                $"Feet: {(Application.isPlaying ? feetDepth.ToString("F2") + "m" : "N/A")}");

        if (chestPoint != null)
            UnityEditor.Handles.Label(chestPoint.position + Vector3.right * 0.2f,
                $"Chest: {(Application.isPlaying ? chestDepth.ToString("F2") + "m" : "N/A")}");

        if (headPoint != null)
            UnityEditor.Handles.Label(headPoint.position + Vector3.right * 0.2f,
                $"Head: {(Application.isPlaying ? headDepth.ToString("F2") + "m" : "N/A")}");
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
    [ContextMenu("Test Water Entry")]
    private void TestWaterEntry()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Water entry test only works in play mode");
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
#endif

    #endregion
}