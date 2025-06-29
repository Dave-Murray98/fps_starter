using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Save data structure specifically for weather systems with Cozy Weather 3 integration.
/// Contains weather-related state information that persists across scene transitions.
/// Now includes Cozy integration state and weather synchronization data.
/// </summary>
[System.Serializable]
public class WeatherSaveData
{
    [Header("Weather State")]
    public List<ActiveWeatherEventData> activeWeatherEvents = new List<ActiveWeatherEventData>();
    public float lastWeatherUpdateTime = 0f; // For weather event timing

    [Header("Temperature from Weather")]
    public float currentBaseTemperature = 20f; // Base seasonal temperature
    public float weatherTemperatureModifier = 0f; // Additional temperature modifier from weather events only

    [Header("Cozy Weather Integration")]
    public bool cozyWeatherEnabled = false; // Whether Cozy integration was active when saved
    public WeatherEventType currentCozyWeatherType = WeatherEventType.Clear; // What weather Cozy should display
    public float cozyTransitionTime = 5f; // How long weather transitions should take in Cozy
    public string lastCozyWeatherProfile = ""; // Name of the last active Cozy weather profile

    public WeatherSaveData()
    {
        activeWeatherEvents = new List<ActiveWeatherEventData>();
        currentBaseTemperature = 20f;
        weatherTemperatureModifier = 0f;
        lastWeatherUpdateTime = 0f;
        cozyWeatherEnabled = false;
        currentCozyWeatherType = WeatherEventType.Clear;
        cozyTransitionTime = 5f;
        lastCozyWeatherProfile = "";
    }

    /// <summary>
    /// Copy constructor for creating independent copies.
    /// </summary>
    public WeatherSaveData(WeatherSaveData other)
    {
        if (other == null) return;

        // Copy temperature data
        currentBaseTemperature = other.currentBaseTemperature;
        weatherTemperatureModifier = other.weatherTemperatureModifier;
        lastWeatherUpdateTime = other.lastWeatherUpdateTime;

        // Copy Cozy integration data
        cozyWeatherEnabled = other.cozyWeatherEnabled;
        currentCozyWeatherType = other.currentCozyWeatherType;
        cozyTransitionTime = other.cozyTransitionTime;
        lastCozyWeatherProfile = other.lastCozyWeatherProfile;

        // Copy weather events
        activeWeatherEvents = new List<ActiveWeatherEventData>();
        if (other.activeWeatherEvents != null)
        {
            foreach (var weatherEvent in other.activeWeatherEvents)
            {
                activeWeatherEvents.Add(new ActiveWeatherEventData(weatherEvent));
            }
        }

        Debug.Log($"[WeatherSaveData] Copy constructor: Copied {activeWeatherEvents.Count} weather events, Cozy: {cozyWeatherEnabled}");
    }

    #region Validation and Debugging

    /// <summary>
    /// Validates the integrity of the weather data.
    /// </summary>
    public bool IsValid()
    {
        // Validate weather events
        if (activeWeatherEvents != null)
        {
            foreach (var weatherEvent in activeWeatherEvents)
            {
                if (!weatherEvent.IsValid())
                    return false;
            }
        }

        // Validate temperature ranges
        if (currentBaseTemperature < -100f || currentBaseTemperature > 100f)
            return false;

        // Validate Cozy integration data
        if (cozyTransitionTime < 0f)
            cozyTransitionTime = 5f; // Auto-correct

        return true;
    }

    /// <summary>
    /// Gets detailed debug information about the weather state.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Weather Save Data Debug Info ===");

        // Temperature
        info.AppendLine($"Base Temperature: {currentBaseTemperature:F1}°C");
        info.AppendLine($"Weather Temperature Modifier: {weatherTemperatureModifier:F1}°C");
        info.AppendLine($"Total Temperature: {(currentBaseTemperature + weatherTemperatureModifier):F1}°C");

        // Cozy integration
        info.AppendLine($"Cozy Integration:");
        info.AppendLine($"  Enabled: {cozyWeatherEnabled}");
        info.AppendLine($"  Current Weather Type: {currentCozyWeatherType}");
        info.AppendLine($"  Transition Time: {cozyTransitionTime:F1}s");
        info.AppendLine($"  Last Profile: {lastCozyWeatherProfile}");

        // Weather events
        info.AppendLine($"Active Weather Events: {activeWeatherEvents?.Count ?? 0}");
        if (activeWeatherEvents != null)
        {
            foreach (var weatherEvent in activeWeatherEvents)
            {
                info.AppendLine($"  - {weatherEvent.eventType}: {weatherEvent.remainingDuration:F1}h remaining");
                info.AppendLine($"    Intensity: {weatherEvent.intensity:F2}, Temp: {weatherEvent.temperatureModifier:+0.0;-0.0}°C");
            }
        }

        info.AppendLine($"Last Weather Update: {lastWeatherUpdateTime:F2}");
        info.AppendLine($"Data Valid: {IsValid()}");

        return info.ToString();
    }

    #endregion

    #region Weather Event Management

    /// <summary>
    /// Adds a new active weather event.
    /// </summary>
    public void AddWeatherEvent(ActiveWeatherEventData weatherEvent)
    {
        if (weatherEvent == null) return;

        if (activeWeatherEvents == null)
            activeWeatherEvents = new List<ActiveWeatherEventData>();

        activeWeatherEvents.Add(weatherEvent);
    }

    /// <summary>
    /// Removes a weather event by type.
    /// </summary>
    public bool RemoveWeatherEvent(WeatherEventType eventType)
    {
        if (activeWeatherEvents == null) return false;

        for (int i = activeWeatherEvents.Count - 1; i >= 0; i--)
        {
            if (activeWeatherEvents[i].eventType == eventType)
            {
                activeWeatherEvents.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a specific weather event type is currently active.
    /// </summary>
    public bool HasWeatherEvent(WeatherEventType eventType)
    {
        if (activeWeatherEvents == null) return false;

        foreach (var weatherEvent in activeWeatherEvents)
        {
            if (weatherEvent.eventType == eventType)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets an active weather event by type, or null if not found.
    /// </summary>
    public ActiveWeatherEventData GetWeatherEvent(WeatherEventType eventType)
    {
        if (activeWeatherEvents == null) return null;

        foreach (var weatherEvent in activeWeatherEvents)
        {
            if (weatherEvent.eventType == eventType)
                return weatherEvent;
        }

        return null;
    }

    /// <summary>
    /// Clears all active weather events.
    /// </summary>
    public void ClearAllWeatherEvents()
    {
        if (activeWeatherEvents == null)
            activeWeatherEvents = new List<ActiveWeatherEventData>();
        else
            activeWeatherEvents.Clear();
    }

    /// <summary>
    /// Gets the count of active weather events.
    /// </summary>
    public int GetActiveWeatherEventCount()
    {
        return activeWeatherEvents?.Count ?? 0;
    }

    /// <summary>
    /// Calculates the total temperature modifier from all active weather events.
    /// </summary>
    public float CalculateWeatherTemperatureModifier()
    {
        float totalModifier = weatherTemperatureModifier;

        if (activeWeatherEvents != null)
        {
            foreach (var weatherEvent in activeWeatherEvents)
            {
                totalModifier += weatherEvent.temperatureModifier * weatherEvent.intensity;
            }
        }

        return totalModifier;
    }

    /// <summary>
    /// Gets the most dominant weather event (highest intensity).
    /// </summary>
    public ActiveWeatherEventData GetDominantWeatherEvent()
    {
        if (activeWeatherEvents == null || activeWeatherEvents.Count == 0)
            return null;

        ActiveWeatherEventData dominant = null;
        float highestIntensity = 0f;

        foreach (var weatherEvent in activeWeatherEvents)
        {
            if (weatherEvent.intensity > highestIntensity)
            {
                highestIntensity = weatherEvent.intensity;
                dominant = weatherEvent;
            }
        }

        return dominant;
    }

    #endregion

    #region Cozy Integration Helpers

    /// <summary>
    /// Sets Cozy integration state and weather type.
    /// </summary>
    public void SetCozyWeatherState(bool enabled, WeatherEventType weatherType, float transitionTime = 5f)
    {
        cozyWeatherEnabled = enabled;
        currentCozyWeatherType = weatherType;
        cozyTransitionTime = Mathf.Max(0.1f, transitionTime);
    }

    /// <summary>
    /// Updates the last used Cozy weather profile name.
    /// </summary>
    public void SetLastCozyProfile(string profileName)
    {
        lastCozyWeatherProfile = profileName ?? "";
    }

    /// <summary>
    /// Gets information needed to restore Cozy weather state.
    /// </summary>
    public object GetCozyRestoreInfo()
    {
        return new
        {
            enabled = cozyWeatherEnabled,
            weatherType = currentCozyWeatherType,
            transitionTime = cozyTransitionTime,
            profileName = lastCozyWeatherProfile,
            dominantWeather = GetDominantWeatherEvent()?.eventType ?? WeatherEventType.Clear
        };
    }

    /// <summary>
    /// Determines if Cozy should be showing clear weather.
    /// </summary>
    public bool ShouldShowClearWeather()
    {
        return !cozyWeatherEnabled ||
               currentCozyWeatherType == WeatherEventType.Clear ||
               GetActiveWeatherEventCount() == 0;
    }

    /// <summary>
    /// Gets the weather type that Cozy should display based on active events.
    /// </summary>
    public WeatherEventType GetRecommendedCozyWeatherType()
    {
        if (!cozyWeatherEnabled || GetActiveWeatherEventCount() == 0)
            return WeatherEventType.Clear;

        var dominantEvent = GetDominantWeatherEvent();
        if (dominantEvent != null && dominantEvent.intensity > 0.1f)
            return dominantEvent.eventType;

        return currentCozyWeatherType;
    }

    #endregion

    #region Temperature Helpers

    /// <summary>
    /// Gets the total calculated temperature including base and weather modifiers.
    /// </summary>
    public float GetTotalTemperature()
    {
        return currentBaseTemperature + CalculateWeatherTemperatureModifier();
    }

    /// <summary>
    /// Sets the base temperature and ensures it's within reasonable bounds.
    /// </summary>
    public void SetBaseTemperature(float temperature)
    {
        currentBaseTemperature = Mathf.Clamp(temperature, -100f, 100f);
    }

    /// <summary>
    /// Updates the weather temperature modifier.
    /// </summary>
    public void SetWeatherTemperatureModifier(float modifier)
    {
        weatherTemperatureModifier = modifier;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Removes expired weather events based on remaining duration.
    /// </summary>
    public void RemoveExpiredWeatherEvents()
    {
        if (activeWeatherEvents == null) return;

        for (int i = activeWeatherEvents.Count - 1; i >= 0; i--)
        {
            if (activeWeatherEvents[i].HasExpired())
            {
                activeWeatherEvents.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Updates all weather events by reducing their remaining duration.
    /// </summary>
    public void UpdateWeatherEvents(float gameTimeDelta)
    {
        if (activeWeatherEvents == null || gameTimeDelta <= 0f) return;

        foreach (var weatherEvent in activeWeatherEvents)
        {
            weatherEvent.ReduceDuration(gameTimeDelta);
        }

        // Remove expired events
        RemoveExpiredWeatherEvents();
    }

    /// <summary>
    /// Gets a summary string of current weather state.
    /// </summary>
    public string GetWeatherSummary()
    {
        if (GetActiveWeatherEventCount() == 0)
            return "Clear";

        var dominant = GetDominantWeatherEvent();
        if (dominant != null)
        {
            return $"{dominant.eventType} ({dominant.intensity:P0} intensity, {dominant.remainingDuration:F1}h remaining)";
        }

        return $"{GetActiveWeatherEventCount()} active weather events";
    }

    /// <summary>
    /// Merges weather data from another WeatherSaveData instance.
    /// Useful for combining data from different sources.
    /// </summary>
    public void MergeFrom(WeatherSaveData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        // Merge temperature data
        if (overwriteExisting || currentBaseTemperature == 20f) // 20f is default
        {
            currentBaseTemperature = other.currentBaseTemperature;
            weatherTemperatureModifier = other.weatherTemperatureModifier;
        }

        // Merge Cozy integration data
        if (overwriteExisting || !cozyWeatherEnabled)
        {
            cozyWeatherEnabled = other.cozyWeatherEnabled;
            currentCozyWeatherType = other.currentCozyWeatherType;
            cozyTransitionTime = other.cozyTransitionTime;
            lastCozyWeatherProfile = other.lastCozyWeatherProfile;
        }

        // Merge weather events (avoid duplicates)
        if (other.activeWeatherEvents != null)
        {
            foreach (var otherEvent in other.activeWeatherEvents)
            {
                bool hasEvent = activeWeatherEvents?.Any(e => e.eventType == otherEvent.eventType) ?? false;
                if (!hasEvent || overwriteExisting)
                {
                    if (activeWeatherEvents == null)
                        activeWeatherEvents = new List<ActiveWeatherEventData>();

                    if (hasEvent && overwriteExisting)
                    {
                        RemoveWeatherEvent(otherEvent.eventType);
                    }

                    activeWeatherEvents.Add(new ActiveWeatherEventData(otherEvent));
                }
            }
        }

        // Update timing
        if (overwriteExisting || lastWeatherUpdateTime == 0f)
        {
            lastWeatherUpdateTime = other.lastWeatherUpdateTime;
        }

        Debug.Log($"[WeatherSaveData] Merged data from other instance: {other.GetActiveWeatherEventCount()} events, Cozy: {other.cozyWeatherEnabled}");
    }

    /// <summary>
    /// Creates a lightweight copy with only essential data for quick operations.
    /// </summary>
    public WeatherSaveData CreateLightweightCopy()
    {
        var copy = new WeatherSaveData
        {
            currentBaseTemperature = this.currentBaseTemperature,
            weatherTemperatureModifier = this.weatherTemperatureModifier,
            cozyWeatherEnabled = this.cozyWeatherEnabled,
            currentCozyWeatherType = this.currentCozyWeatherType,
            lastWeatherUpdateTime = this.lastWeatherUpdateTime
        };

        // Only copy active (non-expired) weather events
        if (activeWeatherEvents != null)
        {
            foreach (var weatherEvent in activeWeatherEvents)
            {
                if (!weatherEvent.HasExpired())
                {
                    copy.activeWeatherEvents.Add(new ActiveWeatherEventData(weatherEvent));
                }
            }
        }

        return copy;
    }

    /// <summary>
    /// Validates and auto-corrects any inconsistent data.
    /// </summary>
    public void ValidateAndCorrect()
    {
        // Ensure lists are initialized
        if (activeWeatherEvents == null)
            activeWeatherEvents = new List<ActiveWeatherEventData>();

        // Remove any null or invalid weather events
        for (int i = activeWeatherEvents.Count - 1; i >= 0; i--)
        {
            if (activeWeatherEvents[i] == null || !activeWeatherEvents[i].IsValid())
            {
                activeWeatherEvents.RemoveAt(i);
            }
        }

        // Clamp temperature to reasonable ranges
        currentBaseTemperature = Mathf.Clamp(currentBaseTemperature, -100f, 100f);
        weatherTemperatureModifier = Mathf.Clamp(weatherTemperatureModifier, -50f, 50f);

        // Ensure Cozy settings are valid
        cozyTransitionTime = Mathf.Max(0.1f, cozyTransitionTime);
        if (string.IsNullOrEmpty(lastCozyWeatherProfile))
            lastCozyWeatherProfile = "";

        // Ensure time values are non-negative
        lastWeatherUpdateTime = Mathf.Max(0f, lastWeatherUpdateTime);
    }

    #endregion
}