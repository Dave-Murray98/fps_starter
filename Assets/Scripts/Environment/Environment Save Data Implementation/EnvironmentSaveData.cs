using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Save data structure for environmental systems (day/night cycle and weather).
/// Contains time, date, season, and weather state information that persists
/// across scene transitions and save/load operations.
/// </summary>
[System.Serializable]
public class EnvironmentSaveData
{
    [Header("Time & Date")]
    public float currentTimeOfDay = 6f; // 0-24 hours
    public SeasonType currentSeason = SeasonType.Spring;
    public int currentDayOfSeason = 1;
    public int totalDaysElapsed = 0;
    public float dayDurationMinutes = 20f; // Real-time minutes per game day

    [Header("Temperature")]
    public float currentTemperatureModifier = 0f; // Temperature modifier from time of day
    public float currentBaseTemperature = 20f; // Base seasonal temperature
    public float currentTotalTemperature = 20f; // Final calculated temperature

    [Header("Weather State")]
    public List<ActiveWeatherEventData> activeWeatherEvents = new List<ActiveWeatherEventData>();
    public float lastWeatherUpdateTime = 0f; // For weather event timing

    public EnvironmentSaveData()
    {
        activeWeatherEvents = new List<ActiveWeatherEventData>();
    }

    /// <summary>
    /// Copy constructor for creating independent copies.
    /// </summary>
    public EnvironmentSaveData(EnvironmentSaveData other)
    {
        if (other == null) return;

        // Copy time & date
        currentTimeOfDay = other.currentTimeOfDay;
        currentSeason = other.currentSeason;
        currentDayOfSeason = other.currentDayOfSeason;
        totalDaysElapsed = other.totalDaysElapsed;
        dayDurationMinutes = other.dayDurationMinutes;

        // Copy temperature
        currentTemperatureModifier = other.currentTemperatureModifier;
        currentBaseTemperature = other.currentBaseTemperature;
        currentTotalTemperature = other.currentTotalTemperature;

        // Copy weather events
        activeWeatherEvents = new List<ActiveWeatherEventData>();
        if (other.activeWeatherEvents != null)
        {
            foreach (var weatherEvent in other.activeWeatherEvents)
            {
                activeWeatherEvents.Add(new ActiveWeatherEventData(weatherEvent));
            }
        }

        lastWeatherUpdateTime = other.lastWeatherUpdateTime;
    }

    #region Validation and Debugging

    /// <summary>
    /// Validates the integrity of the environment data.
    /// </summary>
    public bool IsValid()
    {
        // Check time bounds
        if (currentTimeOfDay < 0f || currentTimeOfDay >= 24f)
            return false;

        // Check day bounds
        if (currentDayOfSeason < 1)
            return false;

        // Check total days
        if (totalDaysElapsed < 0)
            return false;

        // Check day duration
        if (dayDurationMinutes <= 0f)
            return false;

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
    /// Gets detailed debug information about the environment state.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Environment Save Data Debug Info ===");

        // Time & Date
        info.AppendLine($"Time: {GetFormattedTime()} ({currentTimeOfDay:F2})");
        info.AppendLine($"Date: Day {currentDayOfSeason} of {currentSeason}");
        info.AppendLine($"Total Days: {totalDaysElapsed}");
        info.AppendLine($"Day Duration: {dayDurationMinutes:F1} minutes");

        // Temperature
        info.AppendLine($"Temperature Modifier: {currentTemperatureModifier:F1}°C");
        info.AppendLine($"Base Temperature: {currentBaseTemperature:F1}°C");
        info.AppendLine($"Total Temperature: {currentTotalTemperature:F1}°C");

        // Weather
        info.AppendLine($"Active Weather Events: {activeWeatherEvents?.Count ?? 0}");
        if (activeWeatherEvents != null)
        {
            foreach (var weatherEvent in activeWeatherEvents)
            {
                info.AppendLine($"  - {weatherEvent.eventType}: {weatherEvent.remainingDuration:F1}h remaining");
            }
        }

        info.AppendLine($"Last Weather Update: {lastWeatherUpdateTime:F2}");

        return info.ToString();
    }

    /// <summary>
    /// Gets a formatted time string (HH:MM format).
    /// </summary>
    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentTimeOfDay);
        int minutes = Mathf.FloorToInt((currentTimeOfDay - hours) * 60f);
        return $"{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// Gets a formatted date string.
    /// </summary>
    public string GetFormattedDate()
    {
        return $"Day {currentDayOfSeason} of {currentSeason}";
    }

    /// <summary>
    /// Gets a formatted date and time string.
    /// </summary>
    public string GetFormattedDateTime()
    {
        return $"{GetFormattedDate()} at {GetFormattedTime()}";
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

    #endregion
}

/// <summary>
/// Data structure for an active weather event with remaining duration.
/// </summary>
[System.Serializable]
public class ActiveWeatherEventData
{
    public WeatherEventType eventType;
    public float remainingDuration; // Hours remaining
    public float intensity = 1f; // 0-1 intensity level
    public float temperatureModifier = 0f; // Temperature impact in Celsius
    public System.DateTime startTime; // When the event started (for debugging)

    public ActiveWeatherEventData()
    {
        startTime = System.DateTime.Now;
    }

    public ActiveWeatherEventData(WeatherEventType type, float duration, float intensity = 1f, float tempModifier = 0f)
    {
        eventType = type;
        remainingDuration = duration;
        this.intensity = Mathf.Clamp01(intensity);
        temperatureModifier = tempModifier;
        startTime = System.DateTime.Now;
    }

    /// <summary>
    /// Copy constructor for creating independent copies.
    /// </summary>
    public ActiveWeatherEventData(ActiveWeatherEventData other)
    {
        if (other == null) return;

        eventType = other.eventType;
        remainingDuration = other.remainingDuration;
        intensity = other.intensity;
        temperatureModifier = other.temperatureModifier;
        startTime = other.startTime;
    }

    /// <summary>
    /// Validates the weather event data.
    /// </summary>
    public bool IsValid()
    {
        return remainingDuration >= 0f &&
               intensity >= 0f && intensity <= 1f;
    }

    /// <summary>
    /// Checks if this weather event has expired.
    /// </summary>
    public bool HasExpired()
    {
        return remainingDuration <= 0f;
    }

    /// <summary>
    /// Reduces the remaining duration by the specified amount.
    /// </summary>
    public void ReduceDuration(float hours)
    {
        remainingDuration = Mathf.Max(0f, remainingDuration - hours);
    }

    /// <summary>
    /// Gets a formatted string representation of this weather event.
    /// </summary>
    public override string ToString()
    {
        return $"{eventType} (Duration: {remainingDuration:F1}h, Intensity: {intensity:F1}, Temp: {temperatureModifier:+0.0;-0.0}°C)";
    }
}

/// <summary>
/// Enum for different types of weather events.
/// </summary>
[System.Serializable]
public enum WeatherEventType
{
    Clear,
    Rain,
    Snow,
    Thunderstorm,
    Blizzard,
    Fog,
    HeatWave,
    ColdSnap,
    Overcast,
    LightRain,
    HeavyRain
}