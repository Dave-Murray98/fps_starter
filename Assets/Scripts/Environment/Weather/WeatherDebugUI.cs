using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;
using System.Collections.Generic;

/// <summary>
/// Debug UI component for displaying Weather System information in the scene.
/// Shows current weather events, temperature breakdown, and weather timing information.
/// Can be used alongside or integrated with DayNightDebugUI.
/// </summary>
public class WeatherDebugUI : MonoBehaviour
{
    [Header("Text Components")]
    [SerializeField] private TextMeshProUGUI weatherEventsText;
    [SerializeField] private TextMeshProUGUI temperatureBreakdownText;
    [SerializeField] private TextMeshProUGUI weatherTimingText;
    [SerializeField] private TextMeshProUGUI weatherSystemStatusText;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindTextComponents = true;
    [SerializeField] private string weatherEventsTextName = "WeatherEventsText";
    [SerializeField] private string temperatureBreakdownTextName = "TemperatureBreakdownText";
    [SerializeField] private string weatherTimingTextName = "WeatherTimingText";
    [SerializeField] private string weatherSystemStatusTextName = "WeatherSystemStatusText";

    [Header("Display Settings")]
    [SerializeField] private bool showWeatherEvents = true;
    [SerializeField] private bool showTemperatureBreakdown = true;
    [SerializeField] private bool showWeatherTiming = true;
    [SerializeField] private bool showSystemStatus = true;
    [SerializeField] private bool showIntensityBars = true;

    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.2f;
    [SerializeField] private bool enableUpdates = true;

    [Header("Debug Info")]
    [ShowInInspector, ReadOnly] private bool isConnected = false;
    [ShowInInspector, ReadOnly] private float lastUpdateTime = 0f;

    // Cached values to avoid unnecessary updates
    private List<WeatherEventInstance> lastActiveEvents = new List<WeatherEventInstance>();
    private float lastTemperature = float.MinValue;
    private int lastEventCount = -1;

    private void Awake()
    {
        if (autoFindTextComponents)
        {
            FindTextComponents();
        }
    }

    private void Start()
    {
        ConnectToWeatherSystem();
        lastUpdateTime = -updateInterval; // Force initial update
    }

    private void Update()
    {
        if (enableUpdates && Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplays();
            lastUpdateTime = Time.time;
        }
    }

    private void OnEnable()
    {
        ConnectToWeatherSystem();
    }

    #region Connection Management

    /// <summary>
    /// Connects to the WeatherManager and subscribes to events.
    /// </summary>
    private void ConnectToWeatherSystem()
    {
        if (WeatherManager.Instance != null)
        {
            isConnected = true;
            //            Debug.Log("[WeatherDebugUI] Connected to WeatherManager");
        }
        else
        {
            isConnected = false;
            // Debug.Log("[WeatherDebugUI] WeatherManager not found - will retry");
            Invoke(nameof(ConnectToWeatherSystem), 0.5f);
        }
    }

    #endregion

    #region Component Discovery

    /// <summary>
    /// Automatically finds Text Mesh Pro components in the scene by name.
    /// </summary>
    private void FindTextComponents()
    {
        if (weatherEventsText == null)
            weatherEventsText = FindTextComponentByName(weatherEventsTextName);

        if (temperatureBreakdownText == null)
            temperatureBreakdownText = FindTextComponentByName(temperatureBreakdownTextName);

        if (weatherTimingText == null)
            weatherTimingText = FindTextComponentByName(weatherTimingTextName);

        if (weatherSystemStatusText == null)
            weatherSystemStatusText = FindTextComponentByName(weatherSystemStatusTextName);

        Debug.Log($"[WeatherDebugUI] Auto-found text components - " +
                 $"Events: {weatherEventsText != null}, Temp: {temperatureBreakdownText != null}, " +
                 $"Timing: {weatherTimingText != null}, Status: {weatherSystemStatusText != null}");
    }

    /// <summary>
    /// Finds a TextMeshProUGUI component by GameObject name.
    /// </summary>
    private TextMeshProUGUI FindTextComponentByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        if (found != null)
        {
            TextMeshProUGUI textComponent = found.GetComponent<TextMeshProUGUI>();
            if (textComponent == null)
            {
                Debug.LogWarning($"[WeatherDebugUI] GameObject '{objectName}' found but has no TextMeshProUGUI component");
            }
            return textComponent;
        }
        return null;
    }

    #endregion

    #region Display Updates

    /// <summary>
    /// Updates all text displays with current weather information.
    /// </summary>
    private void UpdateDisplays()
    {
        if (!isConnected || WeatherManager.Instance == null)
        {
            ConnectToWeatherSystem();
            return;
        }

        // Get current values
        var currentEvents = WeatherManager.Instance.GetActiveWeatherEvents();
        float currentTemp = WeatherManager.Instance.GetCurrentTemperature();

        // Check if values have changed
        bool eventsChanged = HasEventsChanged(currentEvents);
        bool tempChanged = Mathf.Abs(currentTemp - lastTemperature) > 0.1f;

        if (!eventsChanged && !tempChanged) return; // No updates needed

        // Update individual displays
        if (showWeatherEvents && weatherEventsText != null && eventsChanged)
        {
            weatherEventsText.text = GetWeatherEventsString(currentEvents);
        }

        if (showTemperatureBreakdown && temperatureBreakdownText != null && tempChanged)
        {
            temperatureBreakdownText.text = GetTemperatureBreakdownString();
        }

        if (showWeatherTiming && weatherTimingText != null && eventsChanged)
        {
            weatherTimingText.text = GetWeatherTimingString(currentEvents);
        }

        if (showSystemStatus && weatherSystemStatusText != null && (eventsChanged || tempChanged))
        {
            weatherSystemStatusText.text = GetSystemStatusString(currentEvents);
        }

        // Cache current values
        lastActiveEvents = new List<WeatherEventInstance>(currentEvents);
        lastTemperature = currentTemp;
        lastEventCount = currentEvents.Count;
    }

    /// <summary>
    /// Checks if the weather events list has changed.
    /// </summary>
    private bool HasEventsChanged(List<WeatherEventInstance> currentEvents)
    {
        if (currentEvents.Count != lastEventCount) return true;

        for (int i = 0; i < currentEvents.Count; i++)
        {
            if (i >= lastActiveEvents.Count) return true;

            var current = currentEvents[i];
            var last = lastActiveEvents[i];

            if (current.EventType != last.EventType ||
                current.CurrentPhase != last.CurrentPhase ||
                Mathf.Abs(current.CurrentIntensity - last.CurrentIntensity) > 0.05f)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region String Formatting

    /// <summary>
    /// Formats the active weather events string.
    /// </summary>
    private string GetWeatherEventsString(List<WeatherEventInstance> events)
    {
        if (events.Count == 0)
            return "Weather: Clear";

        var info = new System.Text.StringBuilder();
        info.AppendLine($"Active Weather ({events.Count}):");

        foreach (var weatherEvent in events)
        {
            string intensityBar = showIntensityBars ? GetIntensityBar(weatherEvent.CurrentIntensity) : "";
            string phaseIcon = GetPhaseIcon(weatherEvent.CurrentPhase);

            info.AppendLine($"{phaseIcon} {weatherEvent.DisplayName} {intensityBar}");
            info.AppendLine($"   {weatherEvent.CurrentPhase} - {weatherEvent.RemainingDuration:F1}h left");
        }

        return info.ToString().TrimEnd();
    }

    /// <summary>
    /// Formats the temperature breakdown string.
    /// </summary>
    private string GetTemperatureBreakdownString()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("Temperature Breakdown:");

        float totalTemp = WeatherManager.Instance.GetCurrentTemperature();
        float weatherModifier = WeatherManager.Instance.GetWeatherTemperatureModifier();

        // Calculate base temperature
        float baseTemp = totalTemp - weatherModifier;

        info.AppendLine($"Total: {totalTemp:F1}°C");
        info.AppendLine($"├ Base: {baseTemp:F1}°C");
        info.AppendLine($"└ Weather: {weatherModifier:+0.0;-0.0}°C");

        return info.ToString();
    }

    /// <summary>
    /// Formats the weather timing information string.
    /// </summary>
    private string GetWeatherTimingString(List<WeatherEventInstance> events)
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("Weather Timing:");

        if (events.Count == 0)
        {
            info.AppendLine("No active events");
            return info.ToString();
        }

        foreach (var weatherEvent in events)
        {
            float elapsed = weatherEvent.GetElapsedDuration();
            float progress = weatherEvent.GetOverallProgress();

            info.AppendLine($"{weatherEvent.DisplayName}:");
            info.AppendLine($"  Progress: {progress:P0} ({elapsed:F1}/{weatherEvent.TotalDuration:F1}h)");
            info.AppendLine($"  Phase: {weatherEvent.CurrentPhase} ({weatherEvent.PhaseProgress:P0})");
        }

        return info.ToString();
    }

    /// <summary>
    /// Formats the system status string.
    /// </summary>
    private string GetSystemStatusString(List<WeatherEventInstance> events)
    {
        var info = new System.Text.StringBuilder();

        string dominantWeather = "Clear";
        if (events.Count > 0)
        {
            var dominant = WeatherManager.Instance.GetDominantWeather();
            dominantWeather = dominant?.DisplayName ?? "Mixed";
        }

        info.AppendLine($"Dominant: {dominantWeather}");
        info.AppendLine($"Events: {events.Count}");

        if (InGameTimeManager.Instance != null)
        {
            info.AppendLine($"Season: {InGameTimeManager.Instance.GetCurrentSeason()}");
            info.AppendLine($"Time: {InGameTimeManager.Instance.GetFormattedTime()}");
        }

        return info.ToString();
    }

    /// <summary>
    /// Creates a visual intensity bar for weather events.
    /// </summary>
    private string GetIntensityBar(float intensity)
    {
        const int barLength = 8;
        int filledBars = Mathf.RoundToInt(intensity * barLength);

        var bar = new System.Text.StringBuilder();
        bar.Append("[");

        for (int i = 0; i < barLength; i++)
        {
            bar.Append(i < filledBars ? "■" : "□");
        }

        bar.Append("]");
        return bar.ToString();
    }

    /// <summary>
    /// Gets an icon representing the current weather phase.
    /// </summary>
    private string GetPhaseIcon(WeatherPhase phase)
    {
        return phase switch
        {
            WeatherPhase.BuildUp => "▲",
            WeatherPhase.Active => "●",
            WeatherPhase.Waning => "▼",
            WeatherPhase.Ended => "○",
            _ => "?"
        };
    }

    #endregion

    #region Manual Controls

    /// <summary>
    /// Manually forces an update of all displays.
    /// </summary>
    [Button("Force Update")]
    public void ForceUpdate()
    {
        lastTemperature = float.MinValue;
        lastEventCount = -1;
        lastActiveEvents.Clear();
        UpdateDisplays();
        Debug.Log("[WeatherDebugUI] Forced display update");
    }

    /// <summary>
    /// Toggles display updates on/off.
    /// </summary>
    [Button("Toggle Updates")]
    public void ToggleUpdates()
    {
        enableUpdates = !enableUpdates;
        Debug.Log($"[WeatherDebugUI] Updates {(enableUpdates ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Refreshes text component references.
    /// </summary>
    [Button("Refresh Text References")]
    public void RefreshTextReferences()
    {
        FindTextComponents();
        ForceUpdate();
    }

    /// <summary>
    /// Clears all text displays.
    /// </summary>
    [Button("Clear Displays")]
    public void ClearDisplays()
    {
        if (weatherEventsText != null) weatherEventsText.text = "";
        if (temperatureBreakdownText != null) temperatureBreakdownText.text = "";
        if (weatherTimingText != null) weatherTimingText.text = "";
        if (weatherSystemStatusText != null) weatherSystemStatusText.text = "";

        Debug.Log("[WeatherDebugUI] All displays cleared");
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Sets the update interval for display refreshes.
    /// </summary>
    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.01f, interval);
    }

    /// <summary>
    /// Configures which information to display.
    /// </summary>
    public void ConfigureDisplay(bool events, bool temperature, bool timing, bool status)
    {
        showWeatherEvents = events;
        showTemperatureBreakdown = temperature;
        showWeatherTiming = timing;
        showSystemStatus = status;
        ForceUpdate();
    }

    #endregion

    #region Public Getters

    /// <summary>
    /// Gets the current formatted weather events string.
    /// </summary>
    public string GetCurrentWeatherEventsString()
    {
        if (WeatherManager.Instance != null)
        {
            return GetWeatherEventsString(WeatherManager.Instance.GetActiveWeatherEvents());
        }
        return "No Connection";
    }

    /// <summary>
    /// Gets the current formatted temperature breakdown string.
    /// </summary>
    public string GetCurrentTemperatureBreakdownString()
    {
        if (WeatherManager.Instance != null)
        {
            return GetTemperatureBreakdownString();
        }
        return "No Connection";
    }

    /// <summary>
    /// Checks if the debug UI is connected to the weather manager.
    /// </summary>
    public bool IsConnected() => isConnected;

    #endregion

    private void OnValidate()
    {
        updateInterval = Mathf.Max(0.01f, updateInterval);
    }
}