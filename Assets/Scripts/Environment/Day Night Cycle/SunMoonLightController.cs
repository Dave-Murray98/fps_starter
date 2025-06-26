using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Simple sun and moon light controller that connects to DayNightCycleManager.
/// Handles light rotation, intensity, and color based on time of day.
/// </summary>
public class SunMoonLightController : MonoBehaviour
{
    [Header("Light References")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;

    [Header("Sun Settings")]
    [SerializeField] private float maxSunIntensity = 1.2f;
    [SerializeField] private Color dayColor = Color.white;
    [SerializeField] private Color sunriseColor = new Color(1f, 0.6f, 0.4f);

    [Header("Moon Settings")]
    [SerializeField] private float maxMoonIntensity = 0.3f;
    [SerializeField] private Color moonColor = new Color(0.8f, 0.9f, 1f);

    [Header("Rotation")]
    [SerializeField] private bool rotateLights = true;
    [SerializeField] private float sunriseHour = 6f;
    [SerializeField] private float sunsetHour = 18f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Current values for smooth interpolation
    private float currentSunAngle = 0f;
    private float currentMoonAngle = 180f;
    private float currentSunIntensity = 0f;
    private float currentMoonIntensity = 0f;
    private Color currentSunColor = Color.white;
    private Color currentMoonColor = Color.white;

    private void Start()
    {
        // Auto-find lights if not assigned
        if (sunLight == null) sunLight = GameObject.Find("Sun")?.GetComponent<Light>();
        if (moonLight == null) moonLight = GameObject.Find("Moon")?.GetComponent<Light>();

        ConnectToInGameTimeManager();
    }

    private void Update()
    {
        // Update everything smoothly using unscaled time (works when paused)
        float deltaTime = Time.unscaledDeltaTime;

        if (rotateLights)
        {
            UpdateLightRotation();
        }

        UpdateLightIntensities(deltaTime);
    }

    private void OnEnable()
    {
        ConnectToInGameTimeManager();
    }

    private void OnDisable()
    {
        if (InGameTimeManager.Instance != null)
        {
            InGameTimeManager.OnTimeChanged -= OnTimeChanged;
        }
    }

    private void ConnectToInGameTimeManager()
    {
        if (InGameTimeManager.Instance != null)
        {
            InGameTimeManager.OnTimeChanged -= OnTimeChanged; // Prevent duplicates
            InGameTimeManager.OnTimeChanged += OnTimeChanged;

            // Set initial lighting
            OnTimeChanged(InGameTimeManager.Instance.GetCurrentTimeOfDay());
        }
        else
        {
            // Retry connection
            Invoke(nameof(ConnectToInGameTimeManager), 0.5f);
        }
    }

    private void OnTimeChanged(float timeOfDay)
    {
        // Just store the time - smooth updates happen in Update()
        DebugLog($"Time changed to: {timeOfDay:F2}");
    }

    private void UpdateLightIntensities(float deltaTime)
    {
        if (InGameTimeManager.Instance == null) return;

        float timeOfDay = InGameTimeManager.Instance.GetCurrentTimeOfDay();

        // Calculate target values
        float targetSunIntensity, targetMoonIntensity;
        Color targetSunColor, targetMoonColor;

        CalculateTargetLightValues(timeOfDay, out targetSunIntensity, out targetMoonIntensity,
            out targetSunColor, out targetMoonColor);

        // Smooth interpolation
        float intensitySpeed = 2f; // How fast intensity changes
        float colorSpeed = 3f; // How fast color changes

        currentSunIntensity = Mathf.MoveTowards(currentSunIntensity, targetSunIntensity,
            intensitySpeed * deltaTime);
        currentMoonIntensity = Mathf.MoveTowards(currentMoonIntensity, targetMoonIntensity,
            intensitySpeed * deltaTime);

        currentSunColor = Color.Lerp(currentSunColor, targetSunColor, colorSpeed * deltaTime);
        currentMoonColor = Color.Lerp(currentMoonColor, targetMoonColor, colorSpeed * deltaTime);

        // Apply to lights
        ApplyLightValues();
    }

    private void CalculateTargetLightValues(float timeOfDay, out float sunIntensity, out float moonIntensity,
        out Color sunColor, out Color moonColor)
    {
        // Calculate if it's daytime
        bool isDaytime = timeOfDay >= sunriseHour && timeOfDay <= sunsetHour;

        // Sun calculations
        if (isDaytime)
        {
            // Calculate sun intensity (peaks at noon)
            float dayProgress = (timeOfDay - sunriseHour) / (sunsetHour - sunriseHour);
            sunIntensity = Mathf.Sin(dayProgress * Mathf.PI) * maxSunIntensity;

            // Color transition (sunrise/sunset = orange, midday = white)
            float colorBlend = Mathf.Abs(dayProgress - 0.5f) * 2f; // 0 at noon, 1 at sunrise/sunset
            sunColor = Color.Lerp(dayColor, sunriseColor, colorBlend);
        }
        else
        {
            sunIntensity = 0f;
            sunColor = dayColor;
        }

        // Moon calculations
        bool isNighttime = timeOfDay < sunriseHour || timeOfDay > sunsetHour;
        if (isNighttime)
        {
            moonIntensity = maxMoonIntensity;
            moonColor = this.moonColor;
        }
        else
        {
            moonIntensity = 0f;
            moonColor = this.moonColor;
        }
    }

    private void ApplyLightValues()
    {
        if (sunLight != null)
        {
            sunLight.intensity = currentSunIntensity;
            sunLight.color = currentSunColor;
            sunLight.enabled = currentSunIntensity > 0.01f;
        }

        if (moonLight != null)
        {
            moonLight.intensity = currentMoonIntensity;
            moonLight.color = currentMoonColor;
            moonLight.enabled = currentMoonIntensity > 0.01f;
        }
    }

    private void UpdateLightRotation()
    {
        if (InGameTimeManager.Instance == null) return;

        float timeOfDay = InGameTimeManager.Instance.GetCurrentTimeOfDay();

        // Calculate target angles
        float targetSunAngle = (timeOfDay / 24f) * 360f - 90f; // -90 so sunrise is at 0°
        float targetMoonAngle = targetSunAngle + 180f; // Moon opposite to sun

        // Normalize to 0-360
        while (targetSunAngle < 0f) targetSunAngle += 360f;
        while (targetSunAngle >= 360f) targetSunAngle -= 360f;
        while (targetMoonAngle >= 360f) targetMoonAngle -= 360f;

        // Smooth rotation using unscaled time (works when paused)
        float rotationSpeed = 60f; // degrees per second
        currentSunAngle = Mathf.MoveTowardsAngle(currentSunAngle, targetSunAngle,
            rotationSpeed * Time.unscaledDeltaTime);
        currentMoonAngle = Mathf.MoveTowardsAngle(currentMoonAngle, targetMoonAngle,
            rotationSpeed * Time.unscaledDeltaTime);

        // Apply rotations
        if (sunLight != null)
            sunLight.transform.rotation = Quaternion.Euler(currentSunAngle, 30f, 0f);
        if (moonLight != null)
            moonLight.transform.rotation = Quaternion.Euler(currentMoonAngle, 30f, 0f);
    }

    [Button("Sync with InGame Time Manager")]
    public void SyncNow()
    {
        if (InGameTimeManager.Instance != null)
        {
            OnTimeChanged(InGameTimeManager.Instance.GetCurrentTimeOfDay());
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SunMoonLight] {message}");
        }
    }
}