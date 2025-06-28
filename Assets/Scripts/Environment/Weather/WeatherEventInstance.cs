using UnityEngine;
using System;

/// <summary>
/// SIMPLIFIED: Runtime instance of a weather event using game time throughout.
/// Much cleaner without real-time to game-time conversion complexity.
/// </summary>
[System.Serializable]
public class WeatherEventInstance
{
    [Header("Event Configuration")]
    [SerializeField] private WeatherEventType eventType;
    [SerializeField] private string displayName;
    [SerializeField] private float totalDuration; // Total duration in game hours
    [SerializeField] private float buildUpDuration; // Build-up time in game hours
    [SerializeField] private float waningDuration; // Waning time in game hours

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
    [SerializeField] private float transitionSmoothness;

    // Calculated durations
    private float activeDuration; // Active phase duration in game hours

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
    /// SIMPLIFIED: Constructor that works entirely in game time
    /// </summary>
    public WeatherEventInstance(WeatherEvent weatherEvent)
    {
        eventType = weatherEvent.EventType;
        displayName = weatherEvent.DisplayName;
        totalDuration = weatherEvent.GetRandomDuration();
        buildUpDuration = weatherEvent.BuildUpTime; // Already in game hours
        waningDuration = weatherEvent.WaningTime;   // Already in game hours
        baseIntensity = weatherEvent.BaseIntensity;
        temperatureModifier = weatherEvent.TemperatureModifier;
        hasTemperatureEffect = weatherEvent.HasTemperatureEffect;
        transitionSmoothness = weatherEvent.TransitionSmoothness;

        // Validate and adjust durations if necessary
        ValidateAndAdjustDurations();

        // Initialize state
        currentPhase = WeatherPhase.BuildUp;
        remainingDuration = totalDuration;
        currentIntensity = 0f;
        phaseProgress = 0f;
        startTime = DateTime.Now;

        // Calculate active phase duration
        activeDuration = totalDuration - buildUpDuration - waningDuration;
    }

    /// <summary>
    /// Constructor for loading from saved data
    /// </summary>
    public WeatherEventInstance(ActiveWeatherEventData saveData)
    {
        eventType = saveData.eventType;
        displayName = eventType.ToString();
        remainingDuration = saveData.remainingDuration;
        currentIntensity = saveData.intensity;
        temperatureModifier = saveData.temperatureModifier;
        hasTemperatureEffect = temperatureModifier != 0f;
        startTime = saveData.startTime;

        // Set reasonable defaults for missing data
        baseIntensity = 1f;
        transitionSmoothness = 0.5f;
        buildUpDuration = 0.3f; // Default 0.3 game hours
        waningDuration = 0.2f;  // Default 0.2 game hours

        // Estimate total duration based on remaining duration
        totalDuration = remainingDuration / 0.7f; // Assume we're roughly in the middle

        ValidateAndAdjustDurations();
        activeDuration = totalDuration - buildUpDuration - waningDuration;

        // Determine current phase based on intensity and remaining duration
        DeterminePhaseFromSaveData();
    }
    public void UpdateEvent(float gameTimeDelta)
    {
        if (currentPhase == WeatherPhase.Ended)
        {
            return;
        }

        if (gameTimeDelta <= 0f)
        {
            return;
        }

        // Update remaining duration
        float oldRemaining = remainingDuration;
        remainingDuration -= gameTimeDelta;

        // Update phase and intensity
        UpdateCurrentPhase();
        UpdateIntensity();

        // Check if event should end
        if (remainingDuration <= 0f)
        {
            currentPhase = WeatherPhase.Ended;
            currentIntensity = 0f;
            phaseProgress = 1f;
        }

    }

    /// <summary>
    /// DEBUGGING: Enhanced UpdateCurrentPhase with detailed logging
    /// Replace your existing UpdateCurrentPhase method with this temporarily
    /// </summary>
    private void UpdateCurrentPhase()
    {
        WeatherPhase previousPhase = currentPhase;
        float elapsedTime = totalDuration - remainingDuration;

        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                {
                    if (elapsedTime >= buildUpDuration)
                        currentPhase = WeatherPhase.Active;
                    break;
                }
            case WeatherPhase.Active:
                {
                    if (remainingDuration <= waningDuration)
                        currentPhase = WeatherPhase.Waning;
                }
                break;
            case WeatherPhase.Waning:
                break;
        }

        // Update phase progress
        UpdatePhaseProgress();
    }

    /// <summary>
    /// SIMPLIFIED: Phase progress calculation
    /// </summary>
    private void UpdatePhaseProgress()
    {
        float elapsedTime = totalDuration - remainingDuration;

        switch (currentPhase)
        {
            case WeatherPhase.BuildUp:
                // Progress from 0 to 1 during build-up period
                if (buildUpDuration > 0f)
                    phaseProgress = Mathf.Clamp01(elapsedTime / buildUpDuration);
                else
                    phaseProgress = 1f;
                break;

            case WeatherPhase.Active:
                // Progress through the active phase
                if (activeDuration > 0f)
                {
                    float activeElapsed = elapsedTime - buildUpDuration;
                    phaseProgress = Mathf.Clamp01(activeElapsed / activeDuration);
                }
                else
                    phaseProgress = 1f;
                break;

            case WeatherPhase.Waning:
                // Progress through waning phase (based on remaining time)
                if (waningDuration > 0f)
                {
                    float waningElapsed = waningDuration - remainingDuration;
                    phaseProgress = Mathf.Clamp01(waningElapsed / waningDuration);
                }
                else
                    phaseProgress = 1f;
                break;

            case WeatherPhase.Ended:
                phaseProgress = 1f;
                break;
        }

        phaseProgress = Mathf.Clamp01(phaseProgress);
    }

    /// <summary>
    /// Updates the current intensity based on phase and progress
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
    /// Applies a transition curve for smooth intensity changes
    /// </summary>
    private float ApplyTransitionCurve(float t)
    {
        // Use transition smoothness to blend between linear and smooth curves
        float linear = t;
        float smooth = Mathf.SmoothStep(0f, 1f, t);
        return Mathf.Lerp(linear, smooth, transitionSmoothness);
    }

    /// <summary>
    /// SIMPLIFIED: Validates durations and adjusts if necessary
    /// </summary>
    private void ValidateAndAdjustDurations()
    {
        // Ensure minimum values
        buildUpDuration = Mathf.Max(0.01f, buildUpDuration);
        waningDuration = Mathf.Max(0.01f, waningDuration);
        totalDuration = Mathf.Max(0.1f, totalDuration);

        // Check if transition times exceed total duration
        float totalTransitionTime = buildUpDuration + waningDuration;

        if (totalTransitionTime >= totalDuration)
        {
            DebugLog($"Transition times ({totalTransitionTime:F2}h) exceed total duration ({totalDuration:F2}h)! Adjusting...");

            // Scale down transition times to use max 60% of total duration
            float maxTransitionTime = totalDuration * 0.6f;
            float scale = maxTransitionTime / totalTransitionTime;
            buildUpDuration *= scale;
            waningDuration *= scale;

            DebugLog($"Adjusted - BuildUp: {buildUpDuration:F2}h, Waning: {waningDuration:F2}h");
        }

        // Calculate and validate active duration
        activeDuration = totalDuration - buildUpDuration - waningDuration;

        if (activeDuration < 0.1f)
        {
            DebugLog($"Active phase too short ({activeDuration:F2}h)! Adjusting total duration.");
            totalDuration = buildUpDuration + waningDuration + 0.1f;
            activeDuration = 0.1f;
            remainingDuration = totalDuration; // Update remaining duration too
        }
    }

    /// <summary>
    /// Determines the current phase when loading from save data
    /// </summary>
    private void DeterminePhaseFromSaveData()
    {
        float durationProgress = 1f - (remainingDuration / totalDuration);
        float buildUpProgress = buildUpDuration / totalDuration;
        float waningProgress = waningDuration / totalDuration;

        if (durationProgress <= buildUpProgress)
        {
            currentPhase = WeatherPhase.BuildUp;
            phaseProgress = durationProgress / buildUpProgress;
        }
        else if (durationProgress <= (1f - waningProgress))
        {
            currentPhase = WeatherPhase.Active;
            float activeStart = buildUpProgress;
            float activeLength = 1f - buildUpProgress - waningProgress;
            phaseProgress = (durationProgress - activeStart) / activeLength;
        }
        else
        {
            currentPhase = WeatherPhase.Waning;
            float waningStart = 1f - waningProgress;
            phaseProgress = (durationProgress - waningStart) / waningProgress;
        }

        phaseProgress = Mathf.Clamp01(phaseProgress);
    }

    /// <summary>
    /// Checks if this weather event can occur in the specified season
    /// </summary>
    public bool CanOccurInSeason(SeasonType season)
    {
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
    /// Gets the current temperature modifier based on intensity and base temperature effect
    /// </summary>
    public float GetCurrentTemperatureModifier()
    {
        if (!hasTemperatureEffect) return 0f;
        return temperatureModifier * currentIntensity;
    }

    /// <summary>
    /// Gets a formatted string with current weather event information
    /// </summary>
    public string GetDebugInfo()
    {
        return $"{displayName}: {currentPhase} ({phaseProgress:P0}) - Intensity: {currentIntensity:F2} - Remaining: {remainingDuration:F1}h";
    }

    /// <summary>
    /// Converts this weather event instance to save data format
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
    /// Forces the weather event to end immediately with a smooth transition
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
            remainingDuration = waningDuration;
        }
    }

    /// <summary>
    /// Gets the total elapsed time since the weather event started
    /// </summary>
    public float GetElapsedDuration()
    {
        return totalDuration - remainingDuration;
    }

    /// <summary>
    /// Gets the progress of the entire weather event (0-1)
    /// </summary>
    public float GetOverallProgress()
    {
        return Mathf.Clamp01(GetElapsedDuration() / totalDuration);
    }

    /// <summary>
    /// Checks if the weather event is in its most intense phase
    /// </summary>
    public bool IsAtPeakIntensity()
    {
        return currentPhase == WeatherPhase.Active && currentIntensity >= baseIntensity * 0.9f;
    }

    /// <summary>
    /// Debug logging method for weather events
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
/// Enum representing the different phases of a weather event
/// </summary>
public enum WeatherPhase
{
    BuildUp,    // Weather is gradually building up intensity
    Active,     // Weather is at full intensity
    Waning,     // Weather is gradually decreasing in intensity
    Ended       // Weather event has completely ended
}