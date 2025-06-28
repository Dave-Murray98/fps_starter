using UnityEngine;

/// <summary>
/// Data structure for an active weather event with remaining duration.
/// (Keeping this here since it's purely weather-related)
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