using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// FIXED: Scriptable Object that defines a configurable weather event type.
/// All timing values are now in game time for consistency and simplicity.
/// </summary>
[CreateAssetMenu(fileName = "New Weather Event", menuName = "Environment/Weather/Weather Event")]
public class WeatherEvent : ScriptableObject
{
    [Header("Basic Information")]
    [SerializeField] private WeatherEventType eventType = WeatherEventType.Clear;
    [SerializeField] private string displayName = "";
    [SerializeField, TextArea(2, 4)] private string description = "";

    [Header("Season Configuration")]
    [SerializeField] private List<SeasonType> allowedSeasons = new List<SeasonType>();
    [SerializeField] private bool canOccurInAllSeasons = false;

    [Header("Duration Settings (ALL IN GAME TIME)")]
    [SerializeField, MinValue(0.1f)] private float minDuration = 1f; // Game hours
    [SerializeField, MinValue(0.1f)] private float maxDuration = 4f; // Game hours

    [Header("Transition Timing (IN GAME TIME)")]
    [SerializeField, MinValue(0.01f)] private float buildUpTime = 0.3f; // Game hours (was real minutes)
    [SerializeField, MinValue(0.01f)] private float waningTime = 0.2f; // Game hours (was real minutes)

    [Header("Helpful Time Reference")]
    [SerializeField, ReadOnly] private string timeReference = "With 20min real day: 0.1 game hours = 2 real minutes";

    [Header("Temperature Effects")]
    [SerializeField] private float temperatureModifier = 0f;
    [SerializeField] private bool hasTemperatureEffect = true;

    [Header("Intensity & Rarity")]
    [SerializeField, Range(0f, 1f)] private float baseIntensity = 1f;
    [SerializeField, MinValue(0.1f)] private float rarityWeight = 1f;

    [Header("Time Preferences")]
    [SerializeField] private bool hasTimePreferences = false;
    [SerializeField, ShowIf("hasTimePreferences")] private float preferredStartHour = 0f;
    [SerializeField, ShowIf("hasTimePreferences")] private float preferredEndHour = 24f;

    [Header("Weather Combinations")]
    [SerializeField] private List<WeatherEventType> incompatibleWeatherTypes = new List<WeatherEventType>();
    [SerializeField] private List<WeatherEventType> compatibleWeatherTypes = new List<WeatherEventType>();

    [Header("Advanced Settings")]
    [SerializeField] private bool canStackWithOtherWeather = false;
    [SerializeField] private int maxSimultaneousEvents = 1;
    [SerializeField, Range(0f, 1f)] private float transitionSmoothness = 0.5f;

    // Public properties for easy access
    public WeatherEventType EventType => eventType;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? eventType.ToString() : displayName;
    public string Description => description;
    public List<SeasonType> AllowedSeasons => allowedSeasons;
    public bool CanOccurInAllSeasons => canOccurInAllSeasons;
    public float MinDuration => minDuration;
    public float MaxDuration => maxDuration;
    public float BuildUpTime => buildUpTime; // Now in game hours
    public float WaningTime => waningTime; // Now in game hours
    public float TemperatureModifier => temperatureModifier;
    public bool HasTemperatureEffect => hasTemperatureEffect;
    public float BaseIntensity => baseIntensity;
    public float RarityWeight => rarityWeight;
    public bool HasTimePreferences => hasTimePreferences;
    public float PreferredStartHour => preferredStartHour;
    public float PreferredEndHour => preferredEndHour;
    public List<WeatherEventType> IncompatibleWeatherTypes => incompatibleWeatherTypes;
    public List<WeatherEventType> CompatibleWeatherTypes => compatibleWeatherTypes;
    public bool CanStackWithOtherWeather => canStackWithOtherWeather;
    public int MaxSimultaneousEvents => maxSimultaneousEvents;
    public float TransitionSmoothness => transitionSmoothness;

    /// <summary>
    /// Checks if this weather event can occur during the specified season.
    /// </summary>
    public bool CanOccurInSeason(SeasonType season)
    {
        if (canOccurInAllSeasons)
            return true;

        return allowedSeasons.Contains(season);
    }

    /// <summary>
    /// Checks if this weather event prefers to occur at the specified time.
    /// Returns true if no time preferences are set, or if the time falls within preferred range.
    /// </summary>
    public bool IsPreferredTime(float timeOfDay)
    {
        if (!hasTimePreferences)
            return true;

        // Handle cases where preferred time spans midnight (e.g., 22:00 - 6:00)
        if (preferredEndHour < preferredStartHour)
        {
            return timeOfDay >= preferredStartHour || timeOfDay <= preferredEndHour;
        }
        else
        {
            return timeOfDay >= preferredStartHour && timeOfDay <= preferredEndHour;
        }
    }

    /// <summary>
    /// Checks if this weather event is compatible with another weather type.
    /// </summary>
    public bool IsCompatibleWith(WeatherEventType otherType)
    {
        if (otherType == eventType)
            return false; // Can't be compatible with itself

        if (incompatibleWeatherTypes.Contains(otherType))
            return false;

        if (compatibleWeatherTypes.Count > 0)
            return compatibleWeatherTypes.Contains(otherType);

        return canStackWithOtherWeather;
    }

    /// <summary>
    /// Generates a random duration within the specified range.
    /// </summary>
    public float GetRandomDuration()
    {
        return Random.Range(minDuration, maxDuration);
    }

    /// <summary>
    /// Calculates the time preference multiplier for weather event probability.
    /// Returns 1.0 if no preferences, or a value between 0.1-1.0 based on how preferred the time is.
    /// </summary>
    public float GetTimePreferenceMultiplier(float timeOfDay)
    {
        if (!hasTimePreferences)
            return 1f;

        if (!IsPreferredTime(timeOfDay))
            return 0.1f; // Still possible, but much less likely

        // Calculate how close we are to the optimal time (middle of preferred range)
        float optimalTime;
        if (preferredEndHour < preferredStartHour)
        {
            // Handle midnight crossing
            float midPoint = (preferredStartHour + preferredEndHour + 24f) / 2f;
            if (midPoint >= 24f) midPoint -= 24f;
            optimalTime = midPoint;
        }
        else
        {
            optimalTime = (preferredStartHour + preferredEndHour) / 2f;
        }

        float timeDifference = Mathf.Abs(timeOfDay - optimalTime);
        if (timeDifference > 12f) timeDifference = 24f - timeDifference; // Handle wrap-around

        float preferredRange = Mathf.Abs(preferredEndHour - preferredStartHour);
        if (preferredEndHour < preferredStartHour) preferredRange = 24f - preferredRange;

        float normalizedDistance = timeDifference / (preferredRange / 2f);
        return Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(normalizedDistance));
    }

    /// <summary>
    /// FIXED: Validates the weather event configuration with game time considerations.
    /// </summary>
    public List<string> ValidateConfiguration()
    {
        var issues = new List<string>();

        if (string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(name))
            issues.Add("Weather event needs a name");

        if (minDuration > maxDuration)
            issues.Add("Minimum duration cannot be greater than maximum duration");

        if (buildUpTime <= 0f)
            issues.Add("Build-up time must be greater than 0");

        if (waningTime <= 0f)
            issues.Add("Waning time must be greater than 0");

        // FIXED: Check that transition times don't exceed total duration
        float totalTransitionTime = buildUpTime + waningTime;
        if (totalTransitionTime >= minDuration)
            issues.Add($"Transition times ({totalTransitionTime:F1}h) exceed minimum duration ({minDuration:F1}h)");

        if (totalTransitionTime >= maxDuration * 0.8f)
            issues.Add($"Transition times ({totalTransitionTime:F1}h) use more than 80% of maximum duration ({maxDuration:F1}h)");

        if (!canOccurInAllSeasons && allowedSeasons.Count == 0)
            issues.Add("Weather event must be allowed in at least one season or all seasons");

        if (hasTimePreferences && preferredStartHour == preferredEndHour)
            issues.Add("Preferred start and end hours cannot be the same");

        if (rarityWeight <= 0f)
            issues.Add("Rarity weight must be greater than 0");

        return issues;
    }

    /// <summary>
    /// FIXED: Returns estimated real-time duration for this weather event.
    /// Helpful for designers to understand how long events will feel to players.
    /// </summary>
    public string GetEstimatedRealTime()
    {
        if (InGameTimeManager.Instance == null)
            return "Unknown (TimeManager not available)";

        float timeRate = InGameTimeManager.Instance.GetTimeProgressionRate();
        if (timeRate <= 0f)
            return "Unknown (Invalid time rate)";

        // Calculate real-time durations
        float avgDuration = (minDuration + maxDuration) / 2f;
        float realSeconds = avgDuration / timeRate;
        float realMinutes = realSeconds / 60f;

        float buildUpRealMinutes = buildUpTime / timeRate / 60f;
        float waningRealMinutes = waningTime / timeRate / 60f;

        return $"Avg Duration: {realMinutes:F1} real min (BuildUp: {buildUpRealMinutes:F1}min, Waning: {waningRealMinutes:F1}min)";
    }

    /// <summary>
    /// Returns a formatted string with weather event information for debugging.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"=== {DisplayName} Weather Event ===");
        info.AppendLine($"Type: {eventType}");
        info.AppendLine($"Duration: {minDuration:F1} - {maxDuration:F1} game hours");
        info.AppendLine($"Build-up: {buildUpTime:F1} game hours, Waning: {waningTime:F1} game hours");
        info.AppendLine($"Temperature Effect: {(hasTemperatureEffect ? $"{temperatureModifier:+0.0;-0.0}°C" : "None")}");
        info.AppendLine($"Base Intensity: {baseIntensity:F1}");
        info.AppendLine($"Rarity Weight: {rarityWeight:F1}");

        if (canOccurInAllSeasons)
            info.AppendLine("Seasons: All");
        else
            info.AppendLine($"Seasons: {string.Join(", ", allowedSeasons)}");

        if (hasTimePreferences)
            info.AppendLine($"Preferred Time: {preferredStartHour:F0}:00 - {preferredEndHour:F0}:00");

        if (incompatibleWeatherTypes.Count > 0)
            info.AppendLine($"Incompatible with: {string.Join(", ", incompatibleWeatherTypes)}");

        // Add real-time estimation
        info.AppendLine($"Real-time estimation: {GetEstimatedRealTime()}");

        return info.ToString();
    }

    private void OnValidate()
    {
        // Auto-fix common configuration issues
        if (minDuration > maxDuration)
        {
            maxDuration = minDuration + 1f;
        }

        if (string.IsNullOrEmpty(displayName))
        {
            displayName = eventType.ToString();
        }

        // FIXED: Ensure transition times are reasonable relative to duration
        buildUpTime = Mathf.Max(0.01f, buildUpTime);
        waningTime = Mathf.Max(0.01f, waningTime);
        rarityWeight = Mathf.Max(0.1f, rarityWeight);

        // Auto-adjust transition times if they're too large
        float totalTransitionTime = buildUpTime + waningTime;
        if (totalTransitionTime >= minDuration * 0.8f)
        {
            float scale = (minDuration * 0.6f) / totalTransitionTime;
            buildUpTime *= scale;
            waningTime *= scale;
            Debug.LogWarning($"[{name}] Auto-adjusted transition times to fit within duration");
        }

        // Update time reference for designer convenience
        if (Application.isPlaying && InGameTimeManager.Instance != null)
        {
            float dayDuration = InGameTimeManager.Instance.dayDurationMinutes;
            float gameHoursPerRealMinute = 24f / dayDuration;
            float realMinutesPerGameHour = dayDuration / 24f;
            timeReference = $"With {dayDuration}min real day: 1 game hour = {realMinutesPerGameHour:F1} real minutes";
        }
    }
}