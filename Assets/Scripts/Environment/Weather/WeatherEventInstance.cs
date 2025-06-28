using UnityEngine;
using System;

/// <summary>
/// Runtime instance of a weather event that tracks its current state, phase transitions,
/// and intensity progression. Handles the build-up, active, and waning phases of weather events.
/// </summary>
[System.Serializable]
public class WeatherEventInstance
{
    [Header("Event Configuration")]
    [SerializeField] private WeatherEventType eventType;
    [SerializeField] private string displayName;
    [SerializeField] private float totalDuration; // Total duration in game hours
    [SerializeField] private float buildUpTime; // Build-up time in real-time minutes
    [SerializeField] private float waningTime; // Waning time in real-time minutes

    [Header("Current State")]
    [SerializeField] private WeatherPhase currentPhase;
    [SerializeField] private float remainingDuration; // Remaining duration in game hours
    [SerializeField] private float currentIntensity; // Current intensity (0-1)
    [SerializeField] private float phaseProgress; // Progress within current phase (0-1)

    [Header("Effects")]
    [SerializeField] private float baseIntensity;
    [SerializeField] private float temperatureModifier;
    [SerializeField] private bool hasTemperatureEffect;

    [Header("Timing")]
    [SerializeField] private DateTime startTime;
    [SerializeField] private float phaseStartTime; // Real-time when current phase started
    [SerializeField] private float transitionSmoothness;

    // Cached transition durations converted to game time
    private float buildUpDurationGameTime;
    private float waningDurationGameTime;

    // Public properties for easy access
    public WeatherEventType EventType => eventType;
    public string DisplayName => displayName;
    public float TotalDuration => totalDuration;
    public float RemainingDuration => remainingDuration;
    public float CurrentIntensity => currentIntensity;
    public WeatherPhase CurrentPhase => currentPhase;
    public float PhaseProgress => phaseProgress;
    public bool HasTemperatureEffect => hasTemperatureEffect;
    public bool HasEnded => currentPhase == WeatherPhase.Ended;
    public DateTime StartTime => startTime;

    /// <summary>
    /// Constructor for creating a new weather event instance from a WeatherEvent configuration.
    /// IMPROVED: Better initialization and debug logging.
    /// </summary>
    public WeatherEventInstance(WeatherEvent weatherEvent)
    {
        eventType = weatherEvent.EventType;
        displayName = weatherEvent.DisplayName;
        totalDuration = weatherEvent.GetRandomDuration();
        buildUpTime = weatherEvent.BuildUpTime;
        waningTime = weatherEvent.WaningTime;
        baseIntensity = weatherEvent.BaseIntensity;
        temperatureModifier = weatherEvent.TemperatureModifier;
        hasTemperatureEffect = weatherEvent.HasTemperatureEffect;
        transitionSmoothness = weatherEvent.TransitionSmoothness;

        // Initialize state
        currentPhase = WeatherPhase.BuildUp;
        remainingDuration = totalDuration;
        currentIntensity = 0f;
        phaseProgress = 0f;
        startTime = DateTime.Now;
        phaseStartTime = Time.realtimeSinceStartup;

        // Convert transition times from real-time minutes to game time
        ConvertTransitionTimesToGameTime();

        // Debug log the created event
        DebugLog($"Created weather event:");
        DebugLog($"  Total Duration: {totalDuration:F2} game hours");
        DebugLog($"  Build-up: {buildUpDurationGameTime:F2} game hours");
        DebugLog($"  Active: {(totalDuration - buildUpDurationGameTime - waningDurationGameTime):F2} game hours");
        DebugLog($"  Waning: {waningDurationGameTime:F2} game hours");
    }

    /// <summary>
    /// Constructor for loading from saved data.
    /// </summary>
    public WeatherEventInstance(ActiveWeatherEventData saveData)
    {
        eventType = saveData.eventType;
        displayName = eventType.ToString(); // Will be overridden if we have the original WeatherEvent
        remainingDuration = saveData.remainingDuration;
        currentIntensity = saveData.intensity;
        temperatureModifier = saveData.temperatureModifier;
        hasTemperatureEffect = temperatureModifier != 0f;
        startTime = saveData.startTime;

        // Set reasonable defaults for missing data
        baseIntensity = 1f;
        transitionSmoothness = 0.5f;
        buildUpTime = 2f;
        waningTime = 1.5f;

        // Estimate total duration based on remaining duration
        totalDuration = remainingDuration / 0.7f; // Assume we're roughly in the middle

        // Determine current phase based on intensity and remaining duration
        DeterminePhaseFromSaveData();

        phaseStartTime = Time.realtimeSinceStartup;
        ConvertTransitionTimesToGameTime();
    }

    // Also update the UpdateEvent method to better handle game time deltas:
    /// <summary>
    /// Updates the weather event instance, handling phase transitions and intensity calculations.
    /// IMPROVED: Better handling of game time progression and phase transitions.
    /// </summary>
    /// <param name="gameTimeDelta">Time progression in game hours since last update</param>
    public void UpdateEvent(float gameTimeDelta)
    {
        if (currentPhase == WeatherPhase.Ended) return;

        // Ensure we're working with the correct time conversion
        if (buildUpDurationGameTime == 0f || waningDurationGameTime == 0f)
        {
            ConvertTransitionTimesToGameTime();
        }

        // Update remaining duration
        remainingDuration -= gameTimeDelta;

        // Update phase and intensity based on current state
        UpdateCurrentPhase(gameTimeDelta);
        UpdateIntensity();

        // Check if event should end
        if (remainingDuration <= 0f)
        {
            if (currentPhase == WeatherPhase.Waning || currentPhase == WeatherPhase.Active)
            {
                currentPhase = WeatherPhase.Ended;
                currentIntensity = 0f;
            }
        }
    }

    /// <summary>
    /// Updates the current phase based on elapsed time and remaining duration.
    /// FIXED: Now properly handles decreasing remainingDuration for phase transitions.
    /// </summary>
    private void UpdateCurrentPhase(float gameTimeDelta)
    {
        WeatherPhase previousPhase = currentPhase;

        // Calculate how much time has elapsed since the event started
        float elapsedTime = totalDuration - remainingDuration;

        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                // Transition to Active phase when build-up time is complete
                if (elapsedTime >= buildUpDurationGameTime)
                {
                    currentPhase = WeatherPhase.Active;
                    phaseStartTime = Time.realtimeSinceStartup;
                    DebugLog($"Transitioned to Active phase after {elapsedTime:F2} game hours");
                }
                break;

            case WeatherPhase.Active:
                // Transition to Waning phase when we reach the waning period
                if (remainingDuration <= waningDurationGameTime)
                {
                    currentPhase = WeatherPhase.Waning;
                    phaseStartTime = Time.realtimeSinceStartup;
                    DebugLog($"Transitioned to Waning phase with {remainingDuration:F2} game hours remaining");
                }
                break;

            case WeatherPhase.Waning:
                // Phase end is handled in the main UpdateEvent method
                break;
        }

        // Log phase transitions for debugging
        if (previousPhase != currentPhase)
        {
            DebugLog($"Phase transition: {previousPhase} → {currentPhase} (Elapsed: {elapsedTime:F2}h, Remaining: {remainingDuration:F2}h)");
        }

        // Update phase progress
        UpdatePhaseProgress();
    }

    /// <summary>
    /// Updates the progress within the current phase (0-1).
    /// FIXED: Now calculates progress correctly based on elapsed time.
    /// </summary>
    private void UpdatePhaseProgress()
    {
        float elapsedTime = totalDuration - remainingDuration;

        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                // Progress from 0 to 1 during build-up period
                phaseProgress = Mathf.Clamp01(elapsedTime / buildUpDurationGameTime);
                break;

            case WeatherPhase.Active:
                // Calculate active phase duration and progress
                float activeDuration = totalDuration - buildUpDurationGameTime - waningDurationGameTime;
                float activeElapsed = elapsedTime - buildUpDurationGameTime;
                phaseProgress = Mathf.Clamp01(activeElapsed / activeDuration);
                break;

            case WeatherPhase.Waning:
                // Progress from 0 to 1 during waning period
                float waningElapsed = waningDurationGameTime - remainingDuration;
                phaseProgress = Mathf.Clamp01(waningElapsed / waningDurationGameTime);
                break;

            case WeatherPhase.Ended:
                phaseProgress = 1f;
                break;
        }

        phaseProgress = Mathf.Clamp01(phaseProgress);
    }

    /// <summary>
    /// Updates the current intensity based on phase and progress.
    /// </summary>
    private void UpdateIntensity()
    {
        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                // Gradual increase from 0 to base intensity
                float buildUpCurve = ApplyTransitionCurve(phaseProgress);
                currentIntensity = buildUpCurve * baseIntensity;
                break;

            case WeatherPhase.Active:
                // Full intensity with optional slight variation
                currentIntensity = baseIntensity * (0.9f + 0.1f * Mathf.Sin(Time.time * 0.5f));
                break;

            case WeatherPhase.Waning:
                // Gradual decrease from base intensity to 0
                float waningCurve = ApplyTransitionCurve(1f - phaseProgress);
                currentIntensity = waningCurve * baseIntensity;
                break;

            case WeatherPhase.Ended:
                currentIntensity = 0f;
                break;
        }

        currentIntensity = Mathf.Clamp01(currentIntensity);
    }

    /// <summary>
    /// Applies a transition curve for smooth intensity changes.
    /// </summary>
    private float ApplyTransitionCurve(float t)
    {
        // Use transition smoothness to blend between linear and smooth curves
        float linear = t;
        float smooth = Mathf.SmoothStep(0f, 1f, t);
        return Mathf.Lerp(linear, smooth, transitionSmoothness);
    }

    /// <summary>
    /// Converts real-time transition durations to game time with validation.
    /// IMPROVED: Now includes validation to prevent events getting stuck.
    /// </summary>
    private void ConvertTransitionTimesToGameTime()
    {
        if (InGameTimeManager.Instance != null)
        {
            // Get the time progression rate (game hours per real second)
            float timeProgressionRate = InGameTimeManager.Instance.GetTimeProgressionRate();

            // Convert real-time minutes to game-time hours
            float buildUpTimeSeconds = buildUpTime * 60f;
            float waningTimeSeconds = waningTime * 60f;

            buildUpDurationGameTime = buildUpTimeSeconds * timeProgressionRate;
            waningDurationGameTime = waningTimeSeconds * timeProgressionRate;

            // VALIDATION: Ensure transition times don't exceed total duration
            float totalTransitionTime = buildUpDurationGameTime + waningDurationGameTime;
            if (totalTransitionTime >= totalDuration)
            {
                Debug.LogWarning($"[WeatherEvent:{displayName}] Transition times ({totalTransitionTime:F2}h) exceed total duration ({totalDuration:F2}h)! Adjusting...");

                // Scale down transition times proportionally
                float scale = (totalDuration * 0.8f) / totalTransitionTime; // Use 80% of total duration max
                buildUpDurationGameTime *= scale;
                waningDurationGameTime *= scale;

                Debug.LogWarning($"[WeatherEvent:{displayName}] Adjusted - BuildUp: {buildUpDurationGameTime:F2}h, Waning: {waningDurationGameTime:F2}h");
            }

            // Ensure we have a reasonable active phase duration
            float activeDuration = totalDuration - buildUpDurationGameTime - waningDurationGameTime;
            if (activeDuration < 0.1f)
            {
                Debug.LogWarning($"[WeatherEvent:{displayName}] Active phase too short ({activeDuration:F2}h)! Weather event may not work properly.");
            }

            // Debug logging
            DebugLog($"Time conversion complete:");
            DebugLog($"  Real-time build-up: {buildUpTime} min = {buildUpDurationGameTime:F2} game hours");
            DebugLog($"  Real-time waning: {waningTime} min = {waningDurationGameTime:F2} game hours");
            DebugLog($"  Active phase duration: {activeDuration:F2} game hours");
            DebugLog($"  Time progression rate: {timeProgressionRate:F4} game hours/real second");
        }
        else
        {
            // Fallback values - ensure they're reasonable
            buildUpDurationGameTime = Mathf.Min(0.5f, totalDuration * 0.2f);
            waningDurationGameTime = Mathf.Min(0.25f, totalDuration * 0.1f);

            Debug.LogWarning($"[WeatherEvent:{displayName}] TimeManager not found, using fallback durations");
        }
    }

    /// <summary>
    /// Determines the current phase when loading from save data.
    /// </summary>
    private void DeterminePhaseFromSaveData()
    {
        float durationProgress = 1f - (remainingDuration / totalDuration);

        if (currentIntensity < 0.1f && durationProgress < 0.3f)
        {
            currentPhase = WeatherPhase.BuildUp;
            phaseProgress = durationProgress / 0.3f; // Assume build-up is 30% of total
        }
        else if (currentIntensity > 0.8f && durationProgress < 0.8f)
        {
            currentPhase = WeatherPhase.Active;
            phaseProgress = (durationProgress - 0.3f) / 0.5f; // Active phase
        }
        else
        {
            currentPhase = WeatherPhase.Waning;
            phaseProgress = (durationProgress - 0.8f) / 0.2f; // Waning phase
        }

        phaseProgress = Mathf.Clamp01(phaseProgress);
    }

    /// <summary>
    /// Checks if this weather event can occur in the specified season.
    /// </summary>
    public bool CanOccurInSeason(SeasonType season)
    {
        // This is a simplified check - in a full implementation, you'd store season data
        // For now, we'll use basic logic based on weather type
        switch (eventType)
        {
            case WeatherEventType.Snow:
            case WeatherEventType.Blizzard:
            case WeatherEventType.ColdSnap:
                return season == SeasonType.Winter || season == SeasonType.Fall;

            case WeatherEventType.HeatWave:
                return season == SeasonType.Summer || season == SeasonType.Spring;

            default:
                return true; // Rain, thunderstorms, etc. can occur in any season
        }
    }

    /// <summary>
    /// Gets the current temperature modifier based on intensity and base temperature effect.
    /// </summary>
    public float GetCurrentTemperatureModifier()
    {
        if (!hasTemperatureEffect) return 0f;
        return temperatureModifier * currentIntensity;
    }

    /// <summary>
    /// Gets a formatted string with current weather event information.
    /// </summary>
    public string GetDebugInfo()
    {
        return $"{displayName}: {currentPhase} ({phaseProgress:P0}) - Intensity: {currentIntensity:F2} - Remaining: {remainingDuration:F1}h";
    }

    /// <summary>
    /// Converts this weather event instance to save data format.
    /// </summary>
    public ActiveWeatherEventData ToSaveData()
    {
        return new ActiveWeatherEventData
        {
            eventType = eventType,
            remainingDuration = remainingDuration,
            intensity = currentIntensity,
            temperatureModifier = GetCurrentTemperatureModifier(),
            startTime = startTime
        };
    }

    /// <summary>
    /// Forces the weather event to end immediately with a smooth transition.
    /// </summary>
    public void ForceEnd(bool immediate = false)
    {
        if (immediate)
        {
            currentPhase = WeatherPhase.Ended;
            currentIntensity = 0f;
            remainingDuration = 0f;
        }
        else
        {
            // Transition to waning phase
            currentPhase = WeatherPhase.Waning;
            remainingDuration = waningDurationGameTime;
            phaseStartTime = Time.realtimeSinceStartup;
        }
    }

    /// <summary>
    /// Gets the total elapsed time since the weather event started.
    /// </summary>
    public float GetElapsedDuration()
    {
        return totalDuration - remainingDuration;
    }

    /// <summary>
    /// Gets the progress of the entire weather event (0-1).
    /// </summary>
    public float GetOverallProgress()
    {
        return Mathf.Clamp01(GetElapsedDuration() / totalDuration);
    }

    /// <summary>
    /// Checks if the weather event is in its most intense phase.
    /// </summary>
    public bool IsAtPeakIntensity()
    {
        return currentPhase == WeatherPhase.Active && currentIntensity >= baseIntensity * 0.9f;
    }

    /// <summary>
    /// Debug logging method for weather events.
    /// </summary>
    private void DebugLog(string message)
    {
        if (InGameTimeManager.Instance != null && InGameTimeManager.Instance.showDebugLogs)
        {
            Debug.Log($"[WeatherEvent:{displayName}] {message}");
        }
    }
}

/// <summary>
/// Enum representing the different phases of a weather event.
/// </summary>
public enum WeatherPhase
{
    BuildUp,    // Weather is gradually building up intensity
    Active,     // Weather is at full intensity
    Waning,     // Weather is gradually decreasing in intensity
    Ended       // Weather event has completely ended
}