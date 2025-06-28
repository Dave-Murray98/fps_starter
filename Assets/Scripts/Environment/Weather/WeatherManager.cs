using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Main weather system controller that manages weather events and ALL temperature calculations.
/// Handles base temperature, seasonal effects, day/night cycles, and weather event modifiers.
/// Integrates with InGameTimeManager for time-based updates but owns all temperature logic.
/// </summary>
public class WeatherManager : MonoBehaviour, IManager
{
    public static WeatherManager Instance { get; private set; }

    [Header("Weather Events Configuration")]
    [SerializeField] private List<WeatherEvent> availableWeatherEvents = new List<WeatherEvent>();
    [SerializeField] private bool enableWeatherEvents = true;

    [Header("Temperature System - Complete")]
    [SerializeField] private float baseTemperature = 20f; // Base temperature in Celsius
    [SerializeField] private float seasonalTemperatureVariance = 15f; // How much seasons affect temperature
    [SerializeField] private AnimationCurve seasonalTemperatureCurve = AnimationCurve.EaseInOut(0f, -1f, 1f, 1f);
    [SerializeField] private float dayNightTemperatureVariance = 8f; // Day/night temperature variation
    [SerializeField] private AnimationCurve dayNightTemperatureCurve = AnimationCurve.EaseInOut(0f, -1f, 1f, 1f);

    [Header("Weather Timing")]
    [SerializeField] private float weatherCheckInterval = 1f; // Game hours between weather checks
    [SerializeField, Range(0f, 1f)] private float weatherEventChance = 0.15f; // Base chance per check

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Current weather state
    [ShowInInspector, ReadOnly] private List<WeatherEventInstance> activeWeatherEvents = new List<WeatherEventInstance>();
    [ShowInInspector, ReadOnly] private float currentTemperature = 20f;
    [ShowInInspector, ReadOnly] private float lastWeatherCheckTime = 0f;

    // Temperature breakdown for debugging
    [ShowInInspector, ReadOnly] private float calculatedBaseTemp = 20f;
    [ShowInInspector, ReadOnly] private float seasonalModifier = 0f;
    [ShowInInspector, ReadOnly] private float dayNightModifier = 0f;
    [ShowInInspector, ReadOnly] private float weatherModifier = 0f;

    // Cached references
    private InGameTimeManager timeManager;

    // Events for external systems
    public static event System.Action<WeatherEventInstance> OnWeatherEventStarted;
    public static event System.Action<WeatherEventInstance> OnWeatherEventEnded;
    public static event System.Action<float> OnTemperatureChanged;
    public static event System.Action<List<WeatherEventInstance>> OnActiveWeatherChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            //            DebugLog("WeatherManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        RefreshReferences();
        CalculateCurrentTemperature();
    }

    public void RefreshReferences()
    {

        timeManager = InGameTimeManager.Instance;

        Cleanup();

        if (timeManager != null)
        {
            // Subscribe to time events for weather updates
            InGameTimeManager.OnTimeChanged += HandleTimeChanged;
            InGameTimeManager.OnSeasonChanged += HandleSeasonChanged;
        }
        else
        {
            DebugLog("InGameTimeManager not found - weather system may not function properly");
        }
    }

    public void Cleanup()
    {
        if (timeManager != null)
        {
            InGameTimeManager.OnTimeChanged -= HandleTimeChanged;
            InGameTimeManager.OnSeasonChanged -= HandleSeasonChanged;
        }
    }

    #endregion

    #region Complete Temperature System

    /// <summary>
    /// Calculates the current temperature based on ALL factors:
    /// base temperature + seasonal effects + day/night cycle + weather events.
    /// This is the single source of truth for temperature in the game.
    /// </summary>
    private void CalculateCurrentTemperature()
    {
        if (timeManager == null) return;

        float temperature = baseTemperature;
        calculatedBaseTemp = baseTemperature;

        // Add seasonal temperature variation
        SeasonType currentSeason = timeManager.GetCurrentSeason();
        seasonalModifier = GetSeasonalTemperatureModifier(currentSeason);
        temperature += seasonalModifier;

        // Add day/night temperature variation
        dayNightModifier = GetDayNightTemperatureModifier(timeManager.GetCurrentTimeOfDay());
        temperature += dayNightModifier;

        // Add weather event temperature modifiers
        weatherModifier = 0f;
        foreach (var weatherEvent in activeWeatherEvents)
        {
            if (weatherEvent.HasTemperatureEffect)
            {
                float eventModifier = weatherEvent.GetCurrentTemperatureModifier();
                weatherModifier += eventModifier;
            }
        }
        temperature += weatherModifier;

        // Update cached values and fire events
        float previousTemperature = currentTemperature;
        currentTemperature = temperature;

        // Fire temperature change event if significant change
        if (Mathf.Abs(currentTemperature - previousTemperature) > 0.1f)
        {
            OnTemperatureChanged?.Invoke(currentTemperature);
        }
    }

    /// <summary>
    /// Gets the seasonal temperature modifier based on the current season.
    /// </summary>
    private float GetSeasonalTemperatureModifier(SeasonType season)
    {
        float normalizedSeason = season switch
        {
            SeasonType.Spring => 0.25f,
            SeasonType.Summer => 0.75f,
            SeasonType.Fall => 0.5f,
            SeasonType.Winter => 0f,
            _ => 0.5f
        };

        return seasonalTemperatureCurve.Evaluate(normalizedSeason) * seasonalTemperatureVariance;
    }

    /// <summary>
    /// Gets the day/night temperature modifier based on time of day.
    /// Cooler at night (around 3 AM), warmer during day (around 3 PM).
    /// </summary>
    private float GetDayNightTemperatureModifier(float timeOfDay)
    {
        // Convert time to 0-1 range where noon = 1, midnight = 0
        float normalizedTime = Mathf.Sin((timeOfDay - 6f) / 24f * 2f * Mathf.PI) * 0.5f + 0.5f;

        // Apply day/night temperature curve
        float curveValue = dayNightTemperatureCurve.Evaluate(normalizedTime);
        return curveValue * dayNightTemperatureVariance;
    }

    /// <summary>
    /// Gets the current calculated temperature including all modifiers.
    /// This is the main temperature API for other systems.
    /// </summary>
    public float GetCurrentTemperature() => currentTemperature;

    /// <summary>
    /// Gets the temperature modifier from weather events only.
    /// </summary>
    public float GetWeatherTemperatureModifier() => weatherModifier;

    /// <summary>
    /// Gets the seasonal temperature modifier.
    /// </summary>
    public float GetSeasonalTemperatureModifier() => seasonalModifier;

    /// <summary>
    /// Gets the day/night temperature modifier.
    /// </summary>
    public float GetDayNightTemperatureModifier() => dayNightModifier;

    /// <summary>
    /// Gets the base temperature (before any modifiers).
    /// </summary>
    public float GetBaseTemperature() => baseTemperature;

    /// <summary>
    /// Sets the base temperature and recalculates total temperature.
    /// </summary>
    public void SetBaseTemperature(float temperature)
    {
        baseTemperature = temperature;
        CalculateCurrentTemperature();
    }

    /// <summary>
    /// Sets the seasonal temperature variance and recalculates temperature.
    /// </summary>
    public void SetSeasonalTemperatureVariance(float variance)
    {
        seasonalTemperatureVariance = Mathf.Max(0f, variance);
        CalculateCurrentTemperature();
    }

    /// <summary>
    /// Sets the day/night temperature variance and recalculates temperature.
    /// </summary>
    public void SetDayNightTemperatureVariance(float variance)
    {
        dayNightTemperatureVariance = Mathf.Max(0f, variance);
        CalculateCurrentTemperature();
    }

    #endregion

    #region Weather Event Management

    /// <summary>
    /// Updates all active weather events and checks for new weather events.
    /// IMPROVED: Better game time delta calculation and debugging.
    /// </summary>
    private void UpdateWeatherSystem(float currentTimeOfDay)
    {
        // Calculate actual game time delta since last update
        float gameTimeDelta = CalculateGameTimeDelta();

        // Update existing weather events
        UpdateActiveWeatherEvents(gameTimeDelta);

        // Check for new weather events
        if (enableWeatherEvents && ShouldCheckForNewWeather())
        {
            CheckForNewWeatherEvents();
            lastWeatherCheckTime = currentTimeOfDay;
        }

        // Recalculate temperature after weather updates
        CalculateCurrentTemperature();
    }


    private float lastUpdateTime = -1f; // Make this a class field if it isn't already
    /// <summary>
    /// Calculates the actual game time that has passed since the last weather update.
    /// </summary>
    /// 
    private float CalculateGameTimeDelta()
    {
        if (timeManager == null)
        {
            return 0f;
        }

        // Get current time
        float currentTime = timeManager.GetCurrentTimeOfDay();

        if (lastUpdateTime < 0f)
        {
            lastUpdateTime = currentTime;
            return 0f;
        }

        // Calculate delta, handling day rollover
        float delta = currentTime - lastUpdateTime;
        if (delta < 0f)
        {
            delta += 24f; // Handle day boundary crossing
        }

        lastUpdateTime = currentTime;

        // Limit delta to prevent huge jumps (e.g., when loading saves)
        float originalDelta = delta;
        delta = Mathf.Min(delta, 2f); // Max 2 game hours per update

        return delta;
    }
    /// <summary>
    /// Updates all currently active weather events with proper game time progression.
    /// IMPROVED: Better handling of time deltas and event cleanup.
    /// </summary>
    private void UpdateActiveWeatherEvents(float gameTimeDelta)
    {
        if (gameTimeDelta <= 0f)
        {
            return;
        }

        for (int i = activeWeatherEvents.Count - 1; i >= 0; i--)
        {
            var weatherEvent = activeWeatherEvents[i];

            // Update the event
            weatherEvent.UpdateEvent(gameTimeDelta);

            // Remove completed weather events
            if (weatherEvent.HasEnded)
            {
                DebugLog($"Weather event ended: {weatherEvent.DisplayName} (ran for {weatherEvent.GetElapsedDuration():F1} game hours)");
                EndWeatherEvent(weatherEvent);
            }

        }

        // Notify about active weather changes if there were any changes
        OnActiveWeatherChanged?.Invoke(activeWeatherEvents);
    }


    /// <summary>
    /// Determines if it's time to check for new weather events.
    /// </summary>
    private bool ShouldCheckForNewWeather()
    {
        if (timeManager == null) return false;

        float currentTime = timeManager.GetCurrentTimeOfDay();
        float timeSinceLastCheck = currentTime - lastWeatherCheckTime;

        // Handle day rollover
        if (timeSinceLastCheck < 0)
            timeSinceLastCheck += 24f;

        return timeSinceLastCheck >= weatherCheckInterval;
    }

    /// <summary>
    /// Checks for new weather events based on current conditions and probabilities.
    /// </summary>
    private void CheckForNewWeatherEvents()
    {
        if (timeManager == null || availableWeatherEvents.Count == 0) return;

        // Check if we should spawn a weather event
        if (Random.value > weatherEventChance) return;

        // Get eligible weather events for current conditions
        var eligibleEvents = GetEligibleWeatherEvents();
        if (eligibleEvents.Count == 0) return;

        // Select weather event based on weighted probability
        var selectedEvent = SelectWeatherEventByWeight(eligibleEvents);
        if (selectedEvent != null)
        {
            StartWeatherEvent(selectedEvent);
        }
    }

    /// <summary>
    /// Gets weather events that can occur under current conditions.
    /// </summary>
    private List<WeatherEvent> GetEligibleWeatherEvents()
    {
        if (timeManager == null) return new List<WeatherEvent>();

        SeasonType currentSeason = timeManager.GetCurrentSeason();
        float currentTime = timeManager.GetCurrentTimeOfDay();

        return availableWeatherEvents.Where(weatherEvent =>
        {
            // Check season eligibility
            if (!weatherEvent.CanOccurInSeason(currentSeason))
                return false;

            // Check for incompatible active weather
            foreach (var activeEvent in activeWeatherEvents)
            {
                if (!weatherEvent.IsCompatibleWith(activeEvent.EventType))
                    return false;
            }

            // Check if we've reached the maximum simultaneous events
            var sameTypeEvents = activeWeatherEvents.Count(e => e.EventType == weatherEvent.EventType);
            if (sameTypeEvents >= weatherEvent.MaxSimultaneousEvents)
                return false;

            return true;
        }).ToList();
    }

    /// <summary>
    /// Selects a weather event from eligible events using weighted random selection.
    /// </summary>
    private WeatherEvent SelectWeatherEventByWeight(List<WeatherEvent> eligibleEvents)
    {
        if (eligibleEvents.Count == 0) return null;

        float currentTime = timeManager.GetCurrentTimeOfDay();
        float totalWeight = 0f;

        // Calculate total weight including time preferences
        var weightedEvents = eligibleEvents.Select(e => new
        {
            Event = e,
            Weight = e.RarityWeight * e.GetTimePreferenceMultiplier(currentTime)
        }).ToList();

        totalWeight = weightedEvents.Sum(w => w.Weight);

        // Select random event based on weight
        float randomValue = Random.value * totalWeight;
        float currentWeight = 0f;

        foreach (var weightedEvent in weightedEvents)
        {
            currentWeight += weightedEvent.Weight;
            if (randomValue <= currentWeight)
            {
                return weightedEvent.Event;
            }
        }

        // Fallback to first event
        return eligibleEvents[0];
    }

    /// <summary>
    /// Starts a new weather event instance.
    /// </summary>
    private void StartWeatherEvent(WeatherEvent weatherEvent)
    {
        var instance = new WeatherEventInstance(weatherEvent);
        activeWeatherEvents.Add(instance);

        DebugLog($"Started weather event: {weatherEvent.DisplayName} (Duration: {instance.TotalDuration:F1}h)");
        OnWeatherEventStarted?.Invoke(instance);

        // Recalculate temperature with new weather event
        CalculateCurrentTemperature();
    }

    /// <summary>
    /// Ends and removes a weather event.
    /// </summary>
    private void EndWeatherEvent(WeatherEventInstance weatherEvent)
    {
        activeWeatherEvents.Remove(weatherEvent);
        DebugLog($"Ended weather event: {weatherEvent.DisplayName}");
        OnWeatherEventEnded?.Invoke(weatherEvent);

        // Recalculate temperature without this weather event
        CalculateCurrentTemperature();
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles time change events from InGameTimeManager.
    /// </summary>
    private void HandleTimeChanged(float timeOfDay)
    {
        // DebugLog("WeatherManager.HandleTimeChanged() called - checking for new weather events");
        UpdateWeatherSystem(timeOfDay);
    }

    /// <summary>
    /// Handles season change events from InGameTimeManager.
    /// </summary>
    private void HandleSeasonChanged(SeasonType newSeason)
    {
        DebugLog($"Season changed to {newSeason} - recalculating temperature");
        CalculateCurrentTemperature();

        // End weather events that are not compatible with the new season
        for (int i = activeWeatherEvents.Count - 1; i >= 0; i--)
        {
            var weatherEvent = activeWeatherEvents[i];
            if (!weatherEvent.CanOccurInSeason(newSeason))
            {
                DebugLog($"Ending {weatherEvent.DisplayName} due to season change");
                EndWeatherEvent(weatherEvent);
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets all currently active weather events.
    /// </summary>
    public List<WeatherEventInstance> GetActiveWeatherEvents()
    {
        return new List<WeatherEventInstance>(activeWeatherEvents);
    }

    /// <summary>
    /// Checks if a specific weather type is currently active.
    /// </summary>
    public bool IsWeatherActive(WeatherEventType weatherType)
    {
        return activeWeatherEvents.Any(e => e.EventType == weatherType);
    }

    /// <summary>
    /// Gets the most intense active weather event (highest intensity).
    /// </summary>
    public WeatherEventInstance GetDominantWeather()
    {
        return activeWeatherEvents.OrderByDescending(e => e.CurrentIntensity).FirstOrDefault();
    }

    /// <summary>
    /// Manually starts a weather event (for testing or scripted events).
    /// </summary>
    [Button("Start Weather Event"), ShowIf("enableManualWeatherControl")]
    public void ManuallyStartWeatherEvent(WeatherEvent weatherEvent)
    {
        if (weatherEvent != null)
        {
            StartWeatherEvent(weatherEvent);
        }
    }

    /// <summary>
    /// Clears all active weather events. Used by save system and for testing.
    /// </summary>
    public void ClearAllWeather()
    {
        if (activeWeatherEvents.Count > 0)
        {
            // Fire end events for each weather event
            var eventsToEnd = new List<WeatherEventInstance>(activeWeatherEvents);
            foreach (var weatherEvent in eventsToEnd)
            {
                OnWeatherEventEnded?.Invoke(weatherEvent);
            }

            activeWeatherEvents.Clear();

            // Recalculate temperature without weather events
            CalculateCurrentTemperature();

            // Notify about weather changes
            OnActiveWeatherChanged?.Invoke(activeWeatherEvents);
        }
    }


    /// <summary>
    /// Restores a weather event instance from save data (used by save system).
    /// </summary>
    public void RestoreWeatherEvent(WeatherEventInstance weatherEventInstance)
    {
        if (weatherEventInstance != null && !weatherEventInstance.HasEnded)
        {
            activeWeatherEvents.Add(weatherEventInstance);
            DebugLog($"Restored weather event: {weatherEventInstance.DisplayName} ({weatherEventInstance.CurrentPhase}, {weatherEventInstance.RemainingDuration:F1}h remaining)");

            // Fire event for UI updates
            OnWeatherEventStarted?.Invoke(weatherEventInstance);

            // Recalculate temperature with restored weather event
            CalculateCurrentTemperature();

            // Notify about active weather changes
            OnActiveWeatherChanged?.Invoke(activeWeatherEvents);
        }
    }

    /// <summary>
    /// Restores multiple weather events from save data.
    /// </summary>
    public void RestoreWeatherEvents(List<WeatherEventInstance> weatherEvents)
    {
        if (weatherEvents == null || weatherEvents.Count == 0)
        {
            DebugLog("No weather events to restore");
            return;
        }

        foreach (var weatherEvent in weatherEvents)
        {
            RestoreWeatherEvent(weatherEvent);
        }

    }

    /// <summary>
    /// Forces an immediate temperature recalculation (useful after loading saves).
    /// </summary>
    public void ForceTemperatureUpdate()
    {
        CalculateCurrentTemperature();
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[WeatherManager] {message}");
        }
    }

    private void OnValidate()
    {
        // Validate configuration
        weatherCheckInterval = Mathf.Max(0.1f, weatherCheckInterval);
        weatherEventChance = Mathf.Clamp01(weatherEventChance);
        seasonalTemperatureVariance = Mathf.Max(0f, seasonalTemperatureVariance);
        dayNightTemperatureVariance = Mathf.Max(0f, dayNightTemperatureVariance);
        baseTemperature = Mathf.Clamp(baseTemperature, -50f, 80f); // Reasonable temperature range
    }
}