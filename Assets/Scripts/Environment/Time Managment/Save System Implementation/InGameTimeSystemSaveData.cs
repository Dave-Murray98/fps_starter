using UnityEngine;

/// <summary>
/// Save data structure specifically for the in-game time system.
/// Contains only time, date, season, and temperature data related to time progression.
/// Weather-specific data is handled separately by WeatherSaveData.
/// </summary>
[System.Serializable]
public class InGameTimeSystemSaveData
{
    [Header("Time & Date")]
    public float currentTimeOfDay = 6f; // 0-24 hours
    public SeasonType currentSeason = SeasonType.Spring;
    public int currentDayOfSeason = 1;
    public int totalDaysElapsed = 0;
    public float dayDurationMinutes = 20f; // Real-time minutes per game day

    public InGameTimeSystemSaveData()
    {
        // Set reasonable defaults
        currentTimeOfDay = 6f;
        currentSeason = SeasonType.Spring;
        currentDayOfSeason = 1;
        totalDaysElapsed = 0;
        dayDurationMinutes = 20f;
    }

    /// <summary>
    /// Copy constructor for creating independent copies during scene transitions.
    /// </summary>
    public InGameTimeSystemSaveData(InGameTimeSystemSaveData other)
    {
        if (other == null) return;

        currentTimeOfDay = other.currentTimeOfDay;
        currentSeason = other.currentSeason;
        currentDayOfSeason = other.currentDayOfSeason;
        totalDaysElapsed = other.totalDaysElapsed;
        dayDurationMinutes = other.dayDurationMinutes;
    }

    #region Validation and Debugging

    /// <summary>
    /// Validates the integrity of the time system data.
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

        return true;
    }

    /// <summary>
    /// Gets detailed debug information about the time system state.
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== In-Game Time System Save Data Debug Info ===");
        info.AppendLine($"Time: {GetFormattedTime()} ({currentTimeOfDay:F2})");
        info.AppendLine($"Date: Day {currentDayOfSeason} of {currentSeason}");
        info.AppendLine($"Total Days: {totalDaysElapsed}");
        info.AppendLine($"Day Duration: {dayDurationMinutes:F1} minutes");
        info.AppendLine($"Data Valid: {IsValid()}");

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

    #region Helper Methods

    /// <summary>
    /// Checks if it's currently daytime (6 AM to 6 PM).
    /// </summary>
    public bool IsDaytime() => currentTimeOfDay >= 6f && currentTimeOfDay < 18f;

    /// <summary>
    /// Checks if it's currently nighttime.
    /// </summary>
    public bool IsNighttime() => !IsDaytime();

    /// <summary>
    /// Gets the time of day as a normalized value (0-1, where 0.5 is noon).
    /// </summary>
    public float GetNormalizedTimeOfDay()
    {
        return currentTimeOfDay / 24f;
    }

    /// <summary>
    /// Gets the day progress within the current season (0-1).
    /// </summary>
    public float GetSeasonProgress()
    {
        // Assumes 30 days per season as default
        return Mathf.Clamp01((currentDayOfSeason - 1) / 29f);
    }

    #endregion
}