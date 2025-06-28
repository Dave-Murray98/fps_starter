using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Save data structure specifically for weather systems.
/// Contains only weather-related state information that persists across scene transitions.
/// Time and season data is handled separately by InGameTimeSystemSaveData.
/// </summary>
[System.Serializable]
public class WeatherSaveData
{
    [Header("Weather State")]
    public List<ActiveWeatherEventData> activeWeatherEvents = new List<ActiveWeatherEventData>();
    public float lastWeatherUpdateTime = 0f; // For weather event timing

    [Header("Temperature from Weather")]
    public float currentBaseTemperature = 20f; // Base seasonal temperature (influenced by season from time system)
    public float weatherTemperatureModifier = 0f; // Additional temperature modifier from weather events only

    public WeatherSaveData()
    {
        activeWeatherEvents = new List<ActiveWeatherEventData>();
        currentBaseTemperature = 20f;
        weatherTemperatureModifier = 0f;
        lastWeatherUpdateTime = 0f;
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

        // Copy weather events
        activeWeatherEvents = new List<ActiveWeatherEventData>();
        if (other.activeWeatherEvents != null)
        {
            foreach (var weatherEvent in other.activeWeatherEvents)
            {
                activeWeatherEvents.Add(new ActiveWeatherEventData(weatherEvent));
            }
        }

        Debug.Log($"[WeatherSaveData] Copy constructor: Copied {activeWeatherEvents.Count} weather events");
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

        // Weather events
        info.AppendLine($"Active Weather Events: {activeWeatherEvents?.Count ?? 0}");
        if (activeWeatherEvents != null)
        {
            foreach (var weatherEvent in activeWeatherEvents)
            {
                info.AppendLine($"  - {weatherEvent.eventType}: {weatherEvent.remainingDuration:F1}h remaining, Temp: {weatherEvent.temperatureModifier:+0.0;-0.0}°C");
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

    #endregion
}

