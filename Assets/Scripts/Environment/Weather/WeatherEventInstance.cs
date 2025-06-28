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

    /// <summary>
    /// Updates the weather event instance, handling phase transitions and intensity calculations.
    /// </summary>
    /// <param name="gameTimeDelta">Time progression in game hours since last update</param>
    public void UpdateEvent(float gameTimeDelta)
    {
        if (currentPhase == WeatherPhase.Ended) return;

        // Update remaining duration
        remainingDuration -= gameTimeDelta;

        // Update phase and intensity based on current state
        UpdateCurrentPhase(gameTimeDelta);
        UpdateIntensity();

        // Check if event should end
        if (remainingDuration <= 0f && currentPhase == WeatherPhase.Waning)
        {
            currentPhase = WeatherPhase.Ended;
            currentIntensity = 0f;
        }
    }

    /// <summary>
    /// Updates the current phase based on remaining duration and elapsed time.
    /// </summary>
    private void UpdateCurrentPhase(float gameTimeDelta)
    {
        WeatherPhase previousPhase = currentPhase;

        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                if (remainingDuration <= totalDuration - buildUpDurationGameTime)
                {
                    currentPhase = WeatherPhase.Active;
                    phaseStartTime = Time.realtimeSinceStartup;
                }
                break;

            case WeatherPhase.Active:
                if (remainingDuration <= waningDurationGameTime)
                {
                    currentPhase = WeatherPhase.Waning;
                    phaseStartTime = Time.realtimeSinceStartup;
                }
                break;

            case WeatherPhase.Waning:
                // Phase transition handled in main update loop when duration expires
                break;
        }

        // Update phase progress
        UpdatePhaseProgress(gameTimeDelta);
    }

    /// <summary>
    /// Updates the progress within the current phase (0-1).
    /// </summary>
    private void UpdatePhaseProgress(float gameTimeDelta)
    {
        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                float buildUpElapsed = totalDuration - remainingDuration;
                phaseProgress = Mathf.Clamp01(buildUpElapsed / buildUpDurationGameTime);
                break;

            case WeatherPhase.Active:
                float activeDuration = totalDuration - buildUpDurationGameTime - waningDurationGameTime;
                float activeElapsed = (totalDuration - buildUpDurationGameTime) - remainingDuration;
                phaseProgress = Mathf.Clamp01(activeElapsed / activeDuration);
                break;

            case WeatherPhase.Waning:
                phaseProgress = Mathf.Clamp01((waningDurationGameTime - remainingDuration) / waningDurationGameTime);
                break;

            case WeatherPhase.Ended:
                phaseProgress = 1f;
                break;
        }
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
    /// Converts real-time transition durations to game time based on current time progression rate.
    /// </summary>
    private void ConvertTransitionTimesToGameTime()
    {
        if (InGameTimeManager.Instance != null)
        {
            // Get the time progression rate (game hours per real second)
            float timeRate = 24f / (InGameTimeManager.Instance.dayDurationMinutes * 60f);

            buildUpDurationGameTime = (buildUpTime * 60f) * timeRate; // Convert minutes to seconds, then to game hours
            waningDurationGameTime = (waningTime * 60f) * timeRate;
        }
        else
        {
            // Fallback values if TimeManager not available
            buildUpDurationGameTime = 0.5f; // 30 minutes of game time
            waningDurationGameTime = 0.25f; // 15 minutes of game time
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