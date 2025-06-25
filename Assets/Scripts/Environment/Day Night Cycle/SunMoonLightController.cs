using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Controls scene-specific sun and moon lighting based on the day/night cycle.
/// Automatically connects to the persistent DayNightCycleManager when the scene loads.
/// Handles directional light rotation, intensity, color, and ambient lighting transitions.
/// </summary>
public class SunMoonLightController : MonoBehaviour
{
    [Header("Light References")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    [SerializeField] private bool autoFindLights = true;

    [Header("Sun Configuration")]
    [SerializeField] private AnimationCurve sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Gradient sunColorGradient = new Gradient();
    [SerializeField] private float maxSunIntensity = 1.2f;

    [Header("Moon Configuration")]
    [SerializeField] private AnimationCurve moonIntensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private Gradient moonColorGradient = new Gradient();
    [SerializeField] private float maxMoonIntensity = 0.3f;

    [Header("Ambient Lighting")]
    [SerializeField] private bool controlAmbientLighting = true;
    [SerializeField] private Gradient ambientColorGradient = new Gradient();
    [SerializeField] private AnimationCurve ambientIntensityCurve = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f);
    [SerializeField] private float maxAmbientIntensity = 1f;

    [Header("Sky and Fog")]
    [SerializeField] private bool controlSkybox = true;
    [SerializeField] private Material skyboxMaterial;
    [SerializeField] private AnimationCurve skyboxExposureCurve = AnimationCurve.EaseInOut(0f, 0.8f, 1f, 1.3f);
    [SerializeField] private bool controlFog = true;
    [SerializeField] private Gradient fogColorGradient = new Gradient();

    [Header("Light Rotation")]
    [SerializeField] private bool rotateLights = true;
    [SerializeField] private float sunRiseHour = 6f;
    [SerializeField] private float sunSetHour = 18f;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool enableLightingUpdates = true;
    [SerializeField] private bool useFallbackUpdates = true;
    [SerializeField] private float fallbackUpdateInterval = 0.5f;
    [SerializeField] private bool useSmoothInterpolation = true;
    [SerializeField] private float interpolationSpeed = 2f;

    // Current state
    [ShowInInspector, ReadOnly] private float currentTimeOfDay = 6f;
    [ShowInInspector, ReadOnly] private float targetTimeOfDay = 6f;
    [ShowInInspector, ReadOnly] private bool isConnectedToDayNightCycle = false;
    [ShowInInspector, ReadOnly] private float lastEventTime = -1f;
    [ShowInInspector, ReadOnly] private float lastFallbackUpdate = 0f;

    // Cached components and values
    private Material skyboxInstance;
    private Color originalFogColor;
    private float originalAmbientIntensity;

    private void Awake()
    {
        if (autoFindLights)
        {
            FindLightReferences();
        }

        CacheOriginalValues();
        InitializeGradients();
    }

    private void Start()
    {
        ConnectToDayNightCycle();

        // Set initial lighting state
        if (DayNightCycleManager.Instance != null)
        {
            currentTimeOfDay = DayNightCycleManager.Instance.GetCurrentTimeOfDay();
            UpdateLighting(currentTimeOfDay);
        }
    }

    private void Update()
    {
        // Smooth interpolation between event updates
        if (useSmoothInterpolation && enableLightingUpdates)
        {
            if (Mathf.Abs(currentTimeOfDay - targetTimeOfDay) > 0.01f)
            {
                currentTimeOfDay = Mathf.MoveTowards(currentTimeOfDay, targetTimeOfDay,
                    interpolationSpeed * Time.deltaTime);
                UpdateLighting(currentTimeOfDay);
            }
        }

        // Fallback update system in case events aren't working
        if (useFallbackUpdates && enableLightingUpdates &&
            Time.time - lastFallbackUpdate > fallbackUpdateInterval)
        {
            CheckForTimeUpdates();
            lastFallbackUpdate = Time.time;
        }
    }

    private void OnEnable()
    {
        ConnectToDayNightCycle();
    }

    private void OnDisable()
    {
        DisconnectFromDayNightCycle();
    }

    #region Connection Management

    /// <summary>
    /// Connects to the persistent DayNightCycleManager and subscribes to time events.
    /// </summary>
    private void ConnectToDayNightCycle()
    {
        // First disconnect any existing connections
        DisconnectFromDayNightCycle();

        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.OnTimeChanged += OnTimeChanged;
            isConnectedToDayNightCycle = true;

            // Get current time immediately
            currentTimeOfDay = DayNightCycleManager.Instance.GetCurrentTimeOfDay();
            UpdateLighting(currentTimeOfDay);

            DebugLog($"Connected to DayNightCycleManager - Current time: {currentTimeOfDay:F2}");
        }
        else
        {
            isConnectedToDayNightCycle = false;
            DebugLog("DayNightCycleManager not found - will retry in 0.5s");

            // Retry connection after a short delay
            if (gameObject.activeInHierarchy)
            {
                Invoke(nameof(ConnectToDayNightCycle), 0.5f);
            }
        }
    }

    /// <summary>
    /// Disconnects from the DayNightCycleManager events.
    /// </summary>
    private void DisconnectFromDayNightCycle()
    {
        if (DayNightCycleManager.Instance != null)
        {
            DayNightCycleManager.OnTimeChanged -= OnTimeChanged;
        }
        isConnectedToDayNightCycle = false;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Automatically finds sun and moon lights in the scene.
    /// </summary>
    private void FindLightReferences()
    {
        if (sunLight == null)
        {
            // Look for a light named "Sun" or "Directional Light"
            GameObject sunObj = GameObject.Find("Sun");
            if (sunObj == null) sunObj = GameObject.Find("Directional Light");
            if (sunObj == null)
            {
                // Find the brightest directional light
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light light in lights)
                {
                    if (light.type == LightType.Directional && light.intensity > 0.5f)
                    {
                        sunLight = light;
                        break;
                    }
                }
            }
            else
            {
                sunLight = sunObj.GetComponent<Light>();
            }
        }

        if (moonLight == null)
        {
            // Look for a light named "Moon"
            GameObject moonObj = GameObject.Find("Moon");
            if (moonObj != null)
            {
                moonLight = moonObj.GetComponent<Light>();
            }
        }

        DebugLog($"Light references found - Sun: {sunLight != null}, Moon: {moonLight != null}");
    }

    /// <summary>
    /// Caches original lighting values for restoration if needed.
    /// </summary>
    private void CacheOriginalValues()
    {
        originalFogColor = RenderSettings.fogColor;
        originalAmbientIntensity = RenderSettings.ambientIntensity;

        if (controlSkybox && RenderSettings.skybox != null)
        {
            skyboxInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxInstance;
        }
    }

    /// <summary>
    /// Initializes gradients with default values if they're empty.
    /// </summary>
    private void InitializeGradients()
    {
        // Initialize sun color gradient if empty
        if (sunColorGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] sunColors = new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.4f, 0.1f), 0f),  // Dawn/Dusk orange
                new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.5f), // Midday warm white
                new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f)   // Dawn/Dusk orange
            };
            GradientAlphaKey[] sunAlphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            sunColorGradient.SetKeys(sunColors, sunAlphas);
        }

        // Initialize moon color gradient if empty
        if (moonColorGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] moonColors = new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.7f, 0.8f, 1f), 0f),   // Cool blue
                new GradientColorKey(new Color(0.9f, 0.9f, 1f), 0.5f), // Pale white
                new GradientColorKey(new Color(0.7f, 0.8f, 1f), 1f)    // Cool blue
            };
            GradientAlphaKey[] moonAlphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            moonColorGradient.SetKeys(moonColors, moonAlphas);
        }

        // Initialize ambient color gradient if empty
        if (ambientColorGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] ambientColors = new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 0.3f, 0.5f), 0f),   // Night blue
                new GradientColorKey(new Color(0.5f, 0.7f, 1f), 0.25f),  // Dawn blue
                new GradientColorKey(new Color(1f, 0.95f, 0.8f), 0.5f),  // Day warm
                new GradientColorKey(new Color(0.8f, 0.5f, 0.3f), 0.75f), // Dusk warm
                new GradientColorKey(new Color(0.2f, 0.3f, 0.5f), 1f)    // Night blue
            };
            GradientAlphaKey[] ambientAlphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            ambientColorGradient.SetKeys(ambientColors, ambientAlphas);
        }

        // Initialize fog color gradient if empty
        if (fogColorGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] fogColors = new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 0.15f, 0.3f), 0f),  // Night dark blue
                new GradientColorKey(new Color(0.8f, 0.8f, 0.9f), 0.5f), // Day light gray
                new GradientColorKey(new Color(0.1f, 0.15f, 0.3f), 1f)   // Night dark blue
            };
            GradientAlphaKey[] fogAlphas = new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            fogColorGradient.SetKeys(fogColors, fogAlphas);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles time changes from the DayNightCycleManager.
    /// </summary>
    private void OnTimeChanged(float timeOfDay)
    {
        lastEventTime = Time.time;

        if (useSmoothInterpolation)
        {
            // Set target for smooth interpolation
            targetTimeOfDay = timeOfDay;
        }
        else
        {
            // Immediate update
            currentTimeOfDay = timeOfDay;
            if (enableLightingUpdates)
            {
                UpdateLighting(timeOfDay);
            }
        }

        DebugLog($"Event Update - Target Time: {timeOfDay:F2}");
    }

    /// <summary>
    /// Fallback method to check for time updates if events aren't working.
    /// </summary>
    private void CheckForTimeUpdates()
    {
        if (DayNightCycleManager.Instance == null)
        {
            // Try to reconnect
            ConnectToDayNightCycle();
            return;
        }

        float managerTime = DayNightCycleManager.Instance.GetCurrentTimeOfDay();

        // Check if time has changed significantly
        if (Mathf.Abs(managerTime - targetTimeOfDay) > 0.05f)
        {
            if (useSmoothInterpolation)
            {
                targetTimeOfDay = managerTime;
            }
            else
            {
                currentTimeOfDay = managerTime;
                if (enableLightingUpdates)
                {
                    UpdateLighting(currentTimeOfDay);
                }
            }
            DebugLog($"Fallback Update - Target Time: {managerTime:F2} (Events may not be working)");
        }
    }

    #endregion

    #region Lighting Updates

    /// <summary>
    /// Updates all lighting components based on the current time of day.
    /// </summary>
    private void UpdateLighting(float timeOfDay)
    {
        float normalizedTime = timeOfDay / 24f;

        UpdateSunLight(normalizedTime);
        UpdateMoonLight(normalizedTime);
        UpdateAmbientLighting(normalizedTime);
        UpdateSkybox(normalizedTime);
        UpdateFog(normalizedTime);

        // Always update rotation when rotateLights is enabled
        if (rotateLights)
        {
            UpdateLightRotation(timeOfDay);
        }
    }

    /// <summary>
    /// Updates the sun light intensity, color, and visibility.
    /// </summary>
    private void UpdateSunLight(float normalizedTime)
    {
        if (sunLight == null) return;

        // Calculate sun visibility (day time)
        float sunVisibility = CalculateSunVisibility(currentTimeOfDay);

        // Update intensity - use direct visibility if curve is problematic
        float curveValue = sunIntensityCurve.Evaluate(sunVisibility);
        float intensity = (curveValue > 0.001f ? curveValue : sunVisibility * 0.5f) * maxSunIntensity;
        sunLight.intensity = intensity;

        // Update color
        sunLight.color = sunColorGradient.Evaluate(normalizedTime);

        // Enable/disable based on visibility (not just intensity)
        bool shouldBeEnabled = sunVisibility > 0.001f;
        sunLight.enabled = shouldBeEnabled;

        DebugLog($"Sun - Time: {currentTimeOfDay:F2}, Visibility: {sunVisibility:F2}, CurveValue: {curveValue:F3}, Intensity: {intensity:F2}, Enabled: {shouldBeEnabled}, Range: {sunRiseHour}-{sunSetHour}");
    }

    /// <summary>
    /// Updates the moon light intensity, color, and visibility.
    /// </summary>
    private void UpdateMoonLight(float normalizedTime)
    {
        if (moonLight == null) return;

        // Calculate moon visibility (night time)
        float moonVisibility = CalculateMoonVisibility(currentTimeOfDay);

        // Update intensity - ensure 0 visibility = 0 intensity
        float curveValue = moonVisibility > 0.001f ? moonIntensityCurve.Evaluate(moonVisibility) : 0f;
        float intensity = curveValue * maxMoonIntensity;
        moonLight.intensity = intensity;

        // Update color
        moonLight.color = moonColorGradient.Evaluate(normalizedTime);

        // Enable/disable based on visibility
        bool shouldBeEnabled = moonVisibility > 0.001f;
        moonLight.enabled = shouldBeEnabled;

        DebugLog($"Moon - Time: {currentTimeOfDay:F2}, Visibility: {moonVisibility:F2}, CurveValue: {curveValue:F3}, Intensity: {intensity:F2}, Enabled: {shouldBeEnabled}");
    }

    /// <summary>
    /// Updates ambient lighting settings.
    /// </summary>
    private void UpdateAmbientLighting(float normalizedTime)
    {
        if (!controlAmbientLighting) return;

        // Update ambient color
        RenderSettings.ambientSkyColor = ambientColorGradient.Evaluate(normalizedTime);

        // Update ambient intensity
        float ambientIntensity = ambientIntensityCurve.Evaluate(normalizedTime) * maxAmbientIntensity;
        RenderSettings.ambientIntensity = ambientIntensity;
    }

    /// <summary>
    /// Updates skybox material properties.
    /// </summary>
    private void UpdateSkybox(float normalizedTime)
    {
        if (!controlSkybox || skyboxInstance == null) return;

        // Update skybox exposure based on time
        if (skyboxInstance.HasProperty("_Exposure"))
        {
            float exposure = skyboxExposureCurve.Evaluate(normalizedTime);
            skyboxInstance.SetFloat("_Exposure", exposure);
        }

        // Update sun size or other skybox properties if available
        if (skyboxInstance.HasProperty("_SunSize"))
        {
            float sunSize = Mathf.Lerp(0.04f, 0.08f, normalizedTime);
            skyboxInstance.SetFloat("_SunSize", sunSize);
        }
    }

    /// <summary>
    /// Updates fog color and density.
    /// </summary>
    private void UpdateFog(float normalizedTime)
    {
        if (!controlFog) return;

        // Update fog color
        RenderSettings.fogColor = fogColorGradient.Evaluate(normalizedTime);
    }

    /// <summary>
    /// Updates light rotation to simulate sun/moon movement across the sky.
    /// </summary>
    private void UpdateLightRotation(float timeOfDay)
    {
        // Calculate sun rotation (continuous 24-hour cycle)
        if (sunLight != null)
        {
            float sunAngle = CalculateSunAngle(timeOfDay);
            sunLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);
            DebugLog($"Sun Rotation - Time: {timeOfDay:F2}, Angle: {sunAngle:F1}°");
        }

        // Moon is simply 180° opposite to sun
        if (moonLight != null)
        {
            float sunAngle = CalculateSunAngle(timeOfDay);
            float moonAngle = sunAngle + 180f;

            // Normalize moon angle
            while (moonAngle > 180f) moonAngle -= 360f;
            while (moonAngle < -180f) moonAngle += 360f;

            moonLight.transform.rotation = Quaternion.Euler(moonAngle, 30f, 0f);
            DebugLog($"Moon Rotation - Time: {timeOfDay:F2}, Angle: {moonAngle:F1}°");
        }
    }

    #endregion

    #region Calculation Helpers

    /// <summary>
    /// Calculates sun visibility (0-1) based on time of day.
    /// </summary>
    private float CalculateSunVisibility(float timeOfDay)
    {
        if (timeOfDay >= sunRiseHour && timeOfDay <= sunSetHour)
        {
            // Daytime - calculate position in day using a curve that peaks at noon
            float dayProgress = (timeOfDay - sunRiseHour) / (sunSetHour - sunRiseHour);
            // Use sine curve to peak at noon and fade at sunrise/sunset
            return Mathf.Sin(dayProgress * Mathf.PI);
        }

        return 0f; // Nighttime
    }

    /// <summary>
    /// Calculates moon visibility (0-1) based on time of day.
    /// </summary>
    private float CalculateMoonVisibility(float timeOfDay)
    {
        if (timeOfDay < sunRiseHour || timeOfDay > sunSetHour)
        {
            // Nighttime - calculate position in night
            float nightTime;
            if (timeOfDay > sunSetHour)
            {
                // Evening to midnight
                nightTime = timeOfDay - sunSetHour;
            }
            else
            {
                // Midnight to morning
                nightTime = timeOfDay + (24f - sunSetHour);
            }

            float totalNightDuration = 24f - (sunSetHour - sunRiseHour);
            float nightProgress = nightTime / totalNightDuration;

            // Use sine curve to peak at midnight
            return Mathf.Sin(nightProgress * Mathf.PI);
        }

        return 0f; // Daytime
    }

    /// <summary>
    /// Calculates the sun angle for light rotation (full 24-hour cycle).
    /// Sunrise (6 AM) = -10°, rotates 15° per hour.
    /// </summary>
    private float CalculateSunAngle(float timeOfDay)
    {
        // Calculate hours since sunrise (6 AM)
        float hoursSinceSunrise = timeOfDay - 6f;

        // Handle negative hours (before 6 AM - previous day cycle)
        if (hoursSinceSunrise < 0f)
            hoursSinceSunrise += 24f;

        // Sun angle: starts at -10° at sunrise, rotates 15° per hour
        float angle = -10f + (hoursSinceSunrise * 15f);

        // Normalize angle to -180° to +180° range
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;

        return angle;
    }

    #endregion

    #region Manual Control

    /// <summary>
    /// Manually sets the lighting for a specific time (for testing).
    /// </summary>
    [Button("Set Lighting Time")]
    public void SetLightingTime(float timeOfDay)
    {
        timeOfDay = Mathf.Clamp(timeOfDay, 0f, 23.99f);
        currentTimeOfDay = timeOfDay;
        UpdateLighting(timeOfDay);
        DebugLog($"Lighting manually set to time: {timeOfDay:F2}");
    }

    /// <summary>
    /// Toggles lighting updates on/off.
    /// </summary>
    [Button("Toggle Lighting Updates")]
    public void ToggleLightingUpdates()
    {
        enableLightingUpdates = !enableLightingUpdates;
        DebugLog($"Lighting updates {(enableLightingUpdates ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Forces a reconnection to the day/night cycle manager.
    /// </summary>
    [Button("Force Reconnect")]
    public void ForceReconnect()
    {
        ConnectToDayNightCycle();
    }

    /// <summary>
    /// Manually syncs with the day/night cycle manager time.
    /// </summary>
    [Button("Sync Time Now")]
    public void SyncTimeNow()
    {
        if (DayNightCycleManager.Instance != null)
        {
            float managerTime = DayNightCycleManager.Instance.GetCurrentTimeOfDay();
            currentTimeOfDay = managerTime;
            UpdateLighting(currentTimeOfDay);
            DebugLog($"Manual sync - Time: {currentTimeOfDay:F2}");
        }
        else
        {
            DebugLog("Cannot sync - DayNightCycleManager not found");
        }
    }

    /// <summary>
    /// Tests the event system by checking when the last event was received.
    /// </summary>
    [Button("Test Event System")]
    public void TestEventSystem()
    {
        DebugLog($"Connection Status: {isConnectedToDayNightCycle}");
        DebugLog($"Manager Time: {(DayNightCycleManager.Instance?.GetCurrentTimeOfDay() ?? -1):F2}");
        DebugLog($"Local Time: {currentTimeOfDay:F2}");
        DebugLog($"Last Event: {(lastEventTime > 0 ? $"{Time.time - lastEventTime:F1}s ago" : "Never")}");
        DebugLog($"Fallback Updates: {useFallbackUpdates}");
    }

    /// <summary>
    /// Resets all lighting to original values.
    /// </summary>
    [Button("Reset to Original")]
    public void ResetToOriginalLighting()
    {
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;

        if (sunLight != null)
        {
            sunLight.intensity = 1f;
            sunLight.color = Color.white;
        }

        if (moonLight != null)
        {
            moonLight.intensity = 0f;
        }

        DebugLog("Lighting reset to original values");
    }

    #endregion

    #region Public Getters

    /// <summary>
    /// Gets the current time of day being used for lighting.
    /// </summary>
    public float GetCurrentTimeOfDay() => currentTimeOfDay;

    /// <summary>
    /// Checks if this controller is connected to the day/night cycle manager.
    /// </summary>
    public bool IsConnected() => isConnectedToDayNightCycle;

    /// <summary>
    /// Gets the current sun light reference.
    /// </summary>
    public Light GetSunLight() => sunLight;

    /// <summary>
    /// Gets the current moon light reference.
    /// </summary>
    public Light GetMoonLight() => moonLight;

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SunMoonLight] {message}");
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && enableLightingUpdates)
        {
            UpdateLighting(currentTimeOfDay);
        }
    }
}