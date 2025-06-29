using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;
using System.Collections.Generic;

/// <summary>
/// IMPROVED: Debug UI component for displaying Weather System information in the scene.
/// Now uses event-driven updates for immediate responsiveness to weather changes.
/// Updates immediately when weather events change phase, start, or end.
/// </summary>
public class WeatherDebugUI : MonoBehaviour
{
    // [Header("Text Components")]
    // [SerializeField] private TextMeshProUGUI weatherEventsText;
    // [SerializeField] private TextMeshProUGUI temperatureBreakdownText;
    // [SerializeField] private TextMeshProUGUI weatherTimingText;
    // [SerializeField] private TextMeshProUGUI weatherSystemStatusText;

    // [Header("Display Settings")]
    // [SerializeField] private bool showWeatherEvents = true;
    // [SerializeField] private bool showTemperatureBreakdown = true;
    // [SerializeField] private bool showWeatherTiming = true;
    // [SerializeField] private bool showSystemStatus = true;
    // [SerializeField] private bool showIntensityBars = true;

    // [Header("Update Settings - Event Driven")]
    // [SerializeField] private bool useEventDrivenUpdates = true;
    // [SerializeField] private float fallbackUpdateInterval = 1f; // Fallback polling rate
    // [SerializeField] private bool enableFallbackUpdates = true;
    // [SerializeField] private float intensityUpdateInterval = 0.1f; // For smooth intensity changes

    // [Header("Debug Info")]
    // [ShowInInspector, ReadOnly] private bool isConnected = false;
    // [ShowInInspector, ReadOnly] private float lastUpdateTime = 0f;
    // [ShowInInspector, ReadOnly] private int eventSubscriptionCount = 0;

    // // Cached values to track changes
    // private List<WeatherEventInstance> lastActiveEvents = new List<WeatherEventInstance>();
    // private float lastTemperature = float.MinValue;
    // private int lastEventCount = -1;
    // private Dictionary<WeatherEventType, WeatherPhase> lastEventPhases = new Dictionary<WeatherEventType, WeatherPhase>();
    // private Dictionary<WeatherEventType, float> lastEventIntensities = new Dictionary<WeatherEventType, float>();

    // // Update timing
    // private float lastIntensityUpdateTime = 0f;
    // private bool needsFullUpdate = true;



    // private void Start()
    // {
    //     ConnectToWeatherSystem();
    //     lastUpdateTime = -fallbackUpdateInterval; // Force initial update
    //     lastIntensityUpdateTime = -intensityUpdateInterval;
    // }

    // private void Update()
    // {
    //     // Event-driven updates for immediate responses
    //     if (useEventDrivenUpdates && isConnected)
    //     {
    //         // High-frequency updates for intensity changes (smooth visual feedback)
    //         if (Time.time - lastIntensityUpdateTime >= intensityUpdateInterval)
    //         {
    //             UpdateIntensityDisplays();
    //             lastIntensityUpdateTime = Time.time;
    //         }
    //     }

    //     // Fallback polling updates to catch anything events might miss
    //     if (enableFallbackUpdates && Time.time - lastUpdateTime >= fallbackUpdateInterval)
    //     {
    //         if (needsFullUpdate || !useEventDrivenUpdates)
    //         {
    //             UpdateDisplays();
    //             needsFullUpdate = false;
    //         }
    //         lastUpdateTime = Time.time;
    //     }
    // }

    // private void OnEnable()
    // {
    //     ConnectToWeatherSystem();
    //     //SubscribeToWeatherEvents();
    // }

    // private void OnDisable()
    // {
    //     UnsubscribeFromWeatherEvents();
    // }

    // #region Event Subscription Management

    // /// <summary>
    // /// Subscribe to weather system events for immediate updates
    // /// </summary>
    // private void SubscribeToWeatherEvents()
    // {
    //     if (!useEventDrivenUpdates) return;

    //     // Subscribe to weather manager events
    //     WeatherManager.OnWeatherEventStarted += OnWeatherEventStarted;
    //     WeatherManager.OnWeatherEventEnded += OnWeatherEventEnded;
    //     WeatherManager.OnTemperatureChanged += OnTemperatureChanged;
    //     WeatherManager.OnActiveWeatherChanged += OnActiveWeatherChanged;

    //     // Subscribe to time events for weather timing updates
    //     InGameTimeManager.OnTimeChanged += OnTimeChanged;

    //     eventSubscriptionCount = 5;
    //     // Debug.Log($"[WeatherDebugUI] Subscribed to {eventSubscriptionCount} weather events");
    // }

    // /// <summary>
    // /// Unsubscribe from weather system events
    // /// </summary>
    // private void UnsubscribeFromWeatherEvents()
    // {
    //     WeatherManager.OnWeatherEventStarted -= OnWeatherEventStarted;
    //     WeatherManager.OnWeatherEventEnded -= OnWeatherEventEnded;
    //     WeatherManager.OnTemperatureChanged -= OnTemperatureChanged;
    //     WeatherManager.OnActiveWeatherChanged -= OnActiveWeatherChanged;

    //     InGameTimeManager.OnTimeChanged -= OnTimeChanged;

    //     eventSubscriptionCount = 0;
    //     //Debug.Log("[WeatherDebugUI] Unsubscribed from weather events");
    // }

    // #endregion

    // #region Event Handlers

    // /// <summary>
    // /// Handles weather event started - immediate UI update
    // /// </summary>
    // private void OnWeatherEventStarted(WeatherEventInstance weatherEvent)
    // {
    //     // Debug.Log($"[WeatherDebugUI] Weather event started: {weatherEvent.DisplayName}");
    //     ForceImmediateUpdate();
    // }

    // /// <summary>
    // /// Handles weather event ended - immediate UI update
    // /// </summary>
    // private void OnWeatherEventEnded(WeatherEventInstance weatherEvent)
    // {
    //     //Debug.Log($"[WeatherDebugUI] Weather event ended: {weatherEvent.DisplayName}");
    //     ForceImmediateUpdate();
    // }

    // /// <summary>
    // /// Handles temperature changes - immediate temperature display update
    // /// </summary>
    // private void OnTemperatureChanged(float newTemperature)
    // {
    //     if (Mathf.Abs(newTemperature - lastTemperature) > 0.1f)
    //     {
    //         // Debug.Log($"[WeatherDebugUI] Temperature changed to {newTemperature:F1}°C");
    //         UpdateTemperatureDisplay();
    //         lastTemperature = newTemperature;
    //     }
    // }

    // /// <summary>
    // /// Handles active weather list changes - immediate full update
    // /// </summary>
    // private void OnActiveWeatherChanged(List<WeatherEventInstance> activeEvents)
    // {
    //     //        Debug.Log($"[WeatherDebugUI] Active weather changed - {activeEvents.Count} events");

    //     // Check for phase changes
    //     bool phaseChanged = CheckForPhaseChanges(activeEvents);
    //     bool intensityChanged = CheckForIntensityChanges(activeEvents);

    //     if (phaseChanged || intensityChanged || activeEvents.Count != lastEventCount)
    //     {
    //         ForceImmediateUpdate();
    //     }
    // }

    // /// <summary>
    // /// Handles time changes - update timing displays
    // /// </summary>
    // private void OnTimeChanged(float timeOfDay)
    // {
    //     // Only update timing info, not everything
    //     UpdateTimingDisplays();
    // }

    // #endregion

    // #region Connection Management

    // /// <summary>
    // /// Connects to the WeatherManager and subscribes to events.
    // /// </summary>
    // private void ConnectToWeatherSystem()
    // {
    //     if (WeatherManager.Instance != null)
    //     {
    //         bool wasConnected = isConnected;
    //         isConnected = true;

    //         if (!wasConnected)
    //         {
    //             //  Debug.Log("[WeatherDebugUI] Connected to WeatherManager");
    //             SubscribeToWeatherEvents();
    //             needsFullUpdate = true;
    //         }
    //     }
    //     else
    //     {
    //         isConnected = false;
    //         //            Debug.Log("[WeatherDebugUI] WeatherManager not found - will retry");
    //         Invoke(nameof(ConnectToWeatherSystem), 0.5f);
    //     }
    // }

    // #endregion


    // #region Display Updates

    // /// <summary>
    // /// Forces an immediate complete update of all displays
    // /// </summary>
    // private void ForceImmediateUpdate()
    // {
    //     UpdateDisplays();
    //     needsFullUpdate = false;
    // }

    // /// <summary>
    // /// Updates all text displays with current weather information.
    // /// </summary>
    // private void UpdateDisplays()
    // {
    //     if (!isConnected || WeatherManager.Instance == null)
    //     {
    //         ConnectToWeatherSystem();
    //         return;
    //     }

    //     // Get current values
    //     var currentEvents = WeatherManager.Instance.GetActiveWeatherEvents();
    //     float currentTemp = WeatherManager.Instance.GetCurrentTemperature();

    //     // Update all displays
    //     UpdateWeatherEventsDisplay(currentEvents);
    //     UpdateTemperatureDisplay();
    //     UpdateTimingDisplays();
    //     UpdateSystemStatusDisplay(currentEvents);

    //     // Cache current values
    //     CacheCurrentValues(currentEvents, currentTemp);
    // }

    // /// <summary>
    // /// Updates only the weather events display
    // /// </summary>
    // private void UpdateWeatherEventsDisplay(List<WeatherEventInstance> events = null)
    // {
    //     if (!showWeatherEvents || weatherEventsText == null) return;

    //     if (events == null)
    //         events = WeatherManager.Instance?.GetActiveWeatherEvents() ?? new List<WeatherEventInstance>();

    //     weatherEventsText.text = GetWeatherEventsString(events);
    // }

    // /// <summary>
    // /// Updates only the temperature display
    // /// </summary>
    // private void UpdateTemperatureDisplay()
    // {
    //     if (!showTemperatureBreakdown || temperatureBreakdownText == null) return;

    //     temperatureBreakdownText.text = GetTemperatureBreakdownString();
    // }

    // /// <summary>
    // /// Updates only the timing displays
    // /// </summary>
    // private void UpdateTimingDisplays()
    // {
    //     if (!showWeatherTiming || weatherTimingText == null) return;

    //     var currentEvents = WeatherManager.Instance?.GetActiveWeatherEvents() ?? new List<WeatherEventInstance>();
    //     weatherTimingText.text = GetWeatherTimingString(currentEvents);
    // }

    // /// <summary>
    // /// Updates only the system status display
    // /// </summary>
    // private void UpdateSystemStatusDisplay(List<WeatherEventInstance> events = null)
    // {
    //     if (!showSystemStatus || weatherSystemStatusText == null) return;

    //     if (events == null)
    //         events = WeatherManager.Instance?.GetActiveWeatherEvents() ?? new List<WeatherEventInstance>();

    //     weatherSystemStatusText.text = GetSystemStatusString(events);
    // }

    // /// <summary>
    // /// High-frequency updates for smooth intensity changes
    // /// </summary>
    // private void UpdateIntensityDisplays()
    // {
    //     if (!isConnected || WeatherManager.Instance == null) return;

    //     var currentEvents = WeatherManager.Instance.GetActiveWeatherEvents();

    //     // Check if any intensities have changed significantly
    //     bool intensityChanged = false;
    //     foreach (var weatherEvent in currentEvents)
    //     {
    //         if (lastEventIntensities.TryGetValue(weatherEvent.EventType, out float lastIntensity))
    //         {
    //             if (Mathf.Abs(weatherEvent.CurrentIntensity - lastIntensity) > 0.02f) // 2% change threshold
    //             {
    //                 intensityChanged = true;
    //                 break;
    //             }
    //         }
    //         else
    //         {
    //             intensityChanged = true;
    //             break;
    //         }
    //     }

    //     if (intensityChanged)
    //     {
    //         UpdateWeatherEventsDisplay(currentEvents);
    //         UpdateTimingDisplays();
    //         CacheCurrentValues(currentEvents, WeatherManager.Instance.GetCurrentTemperature());
    //     }
    // }

    // /// <summary>
    // /// Caches current values to detect changes
    // /// </summary>
    // private void CacheCurrentValues(List<WeatherEventInstance> events, float temperature)
    // {
    //     lastActiveEvents = new List<WeatherEventInstance>(events);
    //     lastTemperature = temperature;
    //     lastEventCount = events.Count;

    //     // Cache phases and intensities for change detection
    //     lastEventPhases.Clear();
    //     lastEventIntensities.Clear();

    //     foreach (var weatherEvent in events)
    //     {
    //         lastEventPhases[weatherEvent.EventType] = weatherEvent.CurrentPhase;
    //         lastEventIntensities[weatherEvent.EventType] = weatherEvent.CurrentIntensity;
    //     }
    // }

    // /// <summary>
    // /// Checks if any weather event phases have changed
    // /// </summary>
    // private bool CheckForPhaseChanges(List<WeatherEventInstance> currentEvents)
    // {
    //     foreach (var weatherEvent in currentEvents)
    //     {
    //         if (lastEventPhases.TryGetValue(weatherEvent.EventType, out WeatherPhase lastPhase))
    //         {
    //             if (weatherEvent.CurrentPhase != lastPhase)
    //             {
    //                 //Debug.Log($"[WeatherDebugUI] Phase changed for {weatherEvent.DisplayName}: {lastPhase} -> {weatherEvent.CurrentPhase}");
    //                 return true;
    //             }
    //         }
    //         else
    //         {
    //             // New event
    //             return true;
    //         }
    //     }
    //     return false;
    // }

    // /// <summary>
    // /// Checks if any weather event intensities have changed significantly
    // /// </summary>
    // private bool CheckForIntensityChanges(List<WeatherEventInstance> currentEvents)
    // {
    //     foreach (var weatherEvent in currentEvents)
    //     {
    //         if (lastEventIntensities.TryGetValue(weatherEvent.EventType, out float lastIntensity))
    //         {
    //             if (Mathf.Abs(weatherEvent.CurrentIntensity - lastIntensity) > 0.05f) // 5% change threshold
    //             {
    //                 return true;
    //             }
    //         }
    //         else
    //         {
    //             // New event
    //             return true;
    //         }
    //     }
    //     return false;
    // }

    // #endregion

    // #region String Formatting

    // /// <summary>
    // /// Formats the active weather events string with enhanced phase info.
    // /// </summary>
    // private string GetWeatherEventsString(List<WeatherEventInstance> events)
    // {
    //     if (events.Count == 0)
    //         return "Weather: Clear";

    //     var info = new System.Text.StringBuilder();
    //     info.AppendLine($"Active Weather ({events.Count}):");

    //     foreach (var weatherEvent in events)
    //     {
    //         string intensityBar = showIntensityBars ? GetIntensityBar(weatherEvent.CurrentIntensity) : "";
    //         string phaseIcon = GetPhaseIcon(weatherEvent.CurrentPhase);

    //         info.AppendLine($"{phaseIcon} {weatherEvent.DisplayName} {intensityBar}");
    //         info.AppendLine($"   {weatherEvent.CurrentPhase} ({weatherEvent.PhaseProgress:P0}) - {weatherEvent.RemainingDuration:F1}h left");

    //         // Add extra detail for debug
    //         info.AppendLine($"   Intensity: {weatherEvent.CurrentIntensity:F2}/{weatherEvent.CurrentIntensity:F2}");
    //     }

    //     return info.ToString().TrimEnd();
    // }

    // /// <summary>
    // /// Formats the temperature breakdown string.
    // /// </summary>
    // private string GetTemperatureBreakdownString()
    // {
    //     var info = new System.Text.StringBuilder();
    //     info.AppendLine("Temperature Breakdown:");

    //     float totalTemp = WeatherManager.Instance.GetCurrentTemperature();
    //     float weatherModifier = WeatherManager.Instance.GetWeatherTemperatureModifier();

    //     // Calculate base temperature
    //     float baseTemp = totalTemp - weatherModifier;

    //     info.AppendLine($"Total: {totalTemp:F1}°C");
    //     info.AppendLine($"├ Base: {baseTemp:F1}°C");
    //     info.AppendLine($"└ Weather: {weatherModifier:+0.0;-0.0}°C");

    //     return info.ToString();
    // }

    // /// <summary>
    // /// Enhanced weather timing information with phase details.
    // /// </summary>
    // private string GetWeatherTimingString(List<WeatherEventInstance> events)
    // {
    //     var info = new System.Text.StringBuilder();
    //     info.AppendLine("Weather Timing:");

    //     if (events.Count == 0)
    //     {
    //         info.AppendLine("No active events");
    //         return info.ToString();
    //     }

    //     foreach (var weatherEvent in events)
    //     {
    //         float elapsed = weatherEvent.GetElapsedDuration();
    //         float progress = weatherEvent.GetOverallProgress();

    //         info.AppendLine($"{weatherEvent.DisplayName}:");
    //         info.AppendLine($"  Overall: {progress:P0} ({elapsed:F1}/{weatherEvent.TotalDuration:F1}h)");
    //         info.AppendLine($"  Phase: {weatherEvent.CurrentPhase} ({weatherEvent.PhaseProgress:P0})");

    //         // Add phase timing details
    //         string phaseDetail = GetPhaseTimingDetail(weatherEvent);
    //         if (!string.IsNullOrEmpty(phaseDetail))
    //         {
    //             info.AppendLine($"  {phaseDetail}");
    //         }
    //     }

    //     return info.ToString();
    // }

    // /// <summary>
    // /// Gets detailed timing information for the current phase
    // /// </summary>
    // private string GetPhaseTimingDetail(WeatherEventInstance weatherEvent)
    // {
    //     switch (weatherEvent.CurrentPhase)
    //     {
    //         case WeatherPhase.BuildUp:
    //             // Show build-up progress
    //             return $"Building up... ({weatherEvent.PhaseProgress:P0} complete)";

    //         case WeatherPhase.Active:
    //             return $"At full intensity ({weatherEvent.PhaseProgress:P0} through active phase)";

    //         case WeatherPhase.Waning:
    //             return $"Waning... ({weatherEvent.PhaseProgress:P0} complete)";

    //         case WeatherPhase.Ended:
    //             return "Event completed";

    //         default:
    //             return "";
    //     }
    // }

    // /// <summary>
    // /// Formats the system status string.
    // /// </summary>
    // private string GetSystemStatusString(List<WeatherEventInstance> events)
    // {
    //     var info = new System.Text.StringBuilder();

    //     string dominantWeather = "Clear";
    //     if (events.Count > 0)
    //     {
    //         var dominant = WeatherManager.Instance.GetDominantWeather();
    //         dominantWeather = dominant?.DisplayName ?? "Mixed";
    //     }

    //     info.AppendLine($"Dominant: {dominantWeather}");
    //     info.AppendLine($"Events: {events.Count}");

    //     if (InGameTimeManager.Instance != null)
    //     {
    //         info.AppendLine($"Season: {InGameTimeManager.Instance.GetCurrentSeason()}");
    //         info.AppendLine($"Time: {InGameTimeManager.Instance.GetFormattedTime()}");
    //     }

    //     // Add event-driven update status
    //     info.AppendLine($"Event Updates: {(useEventDrivenUpdates ? "ON" : "OFF")}");
    //     if (useEventDrivenUpdates)
    //     {
    //         info.AppendLine($"Subscriptions: {eventSubscriptionCount}");
    //     }

    //     return info.ToString();
    // }

    // /// <summary>
    // /// Creates a visual intensity bar for weather events.
    // /// </summary>
    // private string GetIntensityBar(float intensity)
    // {
    //     const int barLength = 8;
    //     int filledBars = Mathf.RoundToInt(intensity * barLength);

    //     var bar = new System.Text.StringBuilder();
    //     bar.Append("[");

    //     for (int i = 0; i < barLength; i++)
    //     {
    //         bar.Append(i < filledBars ? "■" : "□");
    //     }

    //     bar.Append("]");
    //     return bar.ToString();
    // }

    // /// <summary>
    // /// Gets an icon representing the current weather phase.
    // /// </summary>
    // private string GetPhaseIcon(WeatherPhase phase)
    // {
    //     return phase switch
    //     {
    //         WeatherPhase.BuildUp => "▲",
    //         WeatherPhase.Active => "●",
    //         WeatherPhase.Waning => "▼",
    //         WeatherPhase.Ended => "○",
    //         _ => "?"
    //     };
    // }

    // #endregion

    // #region Manual Controls

    // /// <summary>
    // /// Manually forces an update of all displays.
    // /// </summary>
    // [Button("Force Update")]
    // public void ForceUpdate()
    // {
    //     lastTemperature = float.MinValue;
    //     lastEventCount = -1;
    //     lastActiveEvents.Clear();
    //     lastEventPhases.Clear();
    //     lastEventIntensities.Clear();
    //     needsFullUpdate = true;
    //     UpdateDisplays();
    //     // Debug.Log("[WeatherDebugUI] Forced display update");
    // }

    // /// <summary>
    // /// Toggles event-driven updates on/off.
    // /// </summary>
    // [Button("Toggle Event Updates")]
    // public void ToggleEventUpdates()
    // {
    //     useEventDrivenUpdates = !useEventDrivenUpdates;

    //     if (useEventDrivenUpdates)
    //     {
    //         SubscribeToWeatherEvents();
    //     }
    //     else
    //     {
    //         UnsubscribeFromWeatherEvents();
    //     }

    //     //Debug.Log($"[WeatherDebugUI] Event-driven updates {(useEventDrivenUpdates ? "enabled" : "disabled")}");
    // }

    // /// <summary>
    // /// Toggles fallback polling updates on/off.
    // /// </summary>
    // [Button("Toggle Fallback Updates")]
    // public void ToggleFallbackUpdates()
    // {
    //     enableFallbackUpdates = !enableFallbackUpdates;
    //     // Debug.Log($"[WeatherDebugUI] Fallback updates {(enableFallbackUpdates ? "enabled" : "disabled")}");
    // }

    // /// <summary>
    // /// Refreshes text component references.
    // /// </summary>
    // [Button("Refresh Text References")]
    // public void RefreshTextReferences()
    // {
    //     ForceUpdate();
    // }

    // /// <summary>
    // /// Clears all text displays.
    // /// </summary>
    // [Button("Clear Displays")]
    // public void ClearDisplays()
    // {
    //     if (weatherEventsText != null) weatherEventsText.text = "";
    //     if (temperatureBreakdownText != null) temperatureBreakdownText.text = "";
    //     if (weatherTimingText != null) weatherTimingText.text = "";
    //     if (weatherSystemStatusText != null) weatherSystemStatusText.text = "";

    //     //   Debug.Log("[WeatherDebugUI] All displays cleared");
    // }

    // #endregion

    // #region Configuration

    // /// <summary>
    // /// Sets the fallback update interval for display refreshes.
    // /// </summary>
    // public void SetFallbackUpdateInterval(float interval)
    // {
    //     fallbackUpdateInterval = Mathf.Max(0.01f, interval);
    // }

    // /// <summary>
    // /// Sets the intensity update interval for smooth visual feedback.
    // /// </summary>
    // public void SetIntensityUpdateInterval(float interval)
    // {
    //     intensityUpdateInterval = Mathf.Max(0.01f, interval);
    // }

    // /// <summary>
    // /// Configures which information to display.
    // /// </summary>
    // public void ConfigureDisplay(bool events, bool temperature, bool timing, bool status)
    // {
    //     showWeatherEvents = events;
    //     showTemperatureBreakdown = temperature;
    //     showWeatherTiming = timing;
    //     showSystemStatus = status;
    //     ForceUpdate();
    // }

    // #endregion

    // #region Public Getters

    // /// <summary>
    // /// Gets the current formatted weather events string.
    // /// </summary>
    // public string GetCurrentWeatherEventsString()
    // {
    //     if (WeatherManager.Instance != null)
    //     {
    //         return GetWeatherEventsString(WeatherManager.Instance.GetActiveWeatherEvents());
    //     }
    //     return "No Connection";
    // }

    // /// <summary>
    // /// Gets the current formatted temperature breakdown string.
    // /// </summary>
    // public string GetCurrentTemperatureBreakdownString()
    // {
    //     if (WeatherManager.Instance != null)
    //     {
    //         return GetTemperatureBreakdownString();
    //     }
    //     return "No Connection";
    // }

    // /// <summary>
    // /// Checks if the debug UI is connected to the weather manager.
    // /// </summary>
    // public bool IsConnected() => isConnected;

    // /// <summary>
    // /// Gets the current event subscription status.
    // /// </summary>
    // public bool IsUsingEventUpdates() => useEventDrivenUpdates;

    // #endregion

    // private void OnValidate()
    // {
    //     fallbackUpdateInterval = Mathf.Max(0.01f, fallbackUpdateInterval);
    //     intensityUpdateInterval = Mathf.Max(0.01f, intensityUpdateInterval);
    // }
}