using UnityEngine;

/// <summary>
/// Save data structure specifically for the in-game time system with Cozy Weather 3 integration.
/// Contains time, date, season, and configuration data for time progression.
/// Updated to include settings needed for proper Cozy integration restoration.
/// </summary>
[System.Serializable]
public class InGameTimeSystemSaveData
{
    [Header("Time & Date")]
    public float currentTimeOfDay = 6f; // 0-24 hours
    public SeasonType currentSeason = SeasonType.Spring;
    public int currentDayOfSeason = 1;
    public int totalDaysElapsed = 0;

    [Header("Time Configuration")]
    public float dayDurationMinutes = 20f; // Real-time minutes per game day
    public int daysPerSeason = 30; // Days in each season
    public int daysPerYear = 120; // Total days in a year (daysPerSeason * 4)

    public InGameTimeSystemSaveData()
    {
        // Set reasonable defaults
        currentTimeOfDay = 6f;
        currentSeason = SeasonType.Spring;
        currentDayOfSeason = 1;
        totalDaysElapsed = 0;
        dayDurationMinutes = 20f;
        daysPerSeason = 30;
        daysPerYear = 120;
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
        daysPerSeason = other.daysPerSeason;
        daysPerYear = other.daysPerYear;
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
        if (currentDayOfSeason < 1 || currentDayOfSeason > daysPerSeason)
            return false;

        // Check total days
        if (totalDaysElapsed < 0)
            return false;

        // Check day duration
        if (dayDurationMinutes <= 0f)
            return false;

        // Check days per season
        if (daysPerSeason <= 0)
            return false;

        // Check year consistency
        if (daysPerYear != daysPerSeason * 4)
        {
            // Auto-correct if possible
            daysPerYear = daysPerSeason * 4;
        }

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
        info.AppendLine($"Configuration:");
        info.AppendLine($"  Day Duration: {dayDurationMinutes:F1} minutes");
        info.AppendLine($"  Days Per Season: {daysPerSeason}");
        info.AppendLine($"  Days Per Year: {daysPerYear}");
        info.AppendLine($"Data Valid: {IsValid()}");
        info.AppendLine($"Day of Year: {GetDayOfYear()}");
        info.AppendLine($"Year Progress: {GetYearProgress():P1}");

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
        return Mathf.Clamp01((currentDayOfSeason - 1) / (float)(daysPerSeason - 1));
    }

    /// <summary>
    /// Gets the current day of the year (1-based).
    /// </summary>
    public int GetDayOfYear()
    {
        int seasonOffset = currentSeason switch
        {
            SeasonType.Spring => 0,
            SeasonType.Summer => daysPerSeason,
            SeasonType.Fall => daysPerSeason * 2,
            SeasonType.Winter => daysPerSeason * 3,
            _ => 0
        };

        return seasonOffset + currentDayOfSeason;
    }

    /// <summary>
    /// Gets the progress through the current year (0-1).
    /// </summary>
    public float GetYearProgress()
    {
        return Mathf.Clamp01((GetDayOfYear() - 1) / (float)(daysPerYear - 1));
    }

    /// <summary>
    /// Calculates the time progression rate (game hours per real second).
    /// Useful for systems that need to know how fast time is moving.
    /// </summary>
    public float GetTimeProgressionRate()
    {
        return 24f / (dayDurationMinutes * 60f);
    }

    /// <summary>
    /// Gets the real-time seconds per game hour.
    /// </summary>
    public float GetRealSecondsPerGameHour()
    {
        return (dayDurationMinutes * 60f) / 24f;
    }

    /// <summary>
    /// Gets the real-time minutes per game day.
    /// </summary>
    public float GetRealMinutesPerGameDay()
    {
        return dayDurationMinutes;
    }

    #endregion

    #region Configuration Helpers

    /// <summary>
    /// Updates the days per season and recalculates year length.
    /// </summary>
    public void SetDaysPerSeason(int days)
    {
        daysPerSeason = Mathf.Max(1, days);
        daysPerYear = daysPerSeason * 4;

        // Clamp current day if it exceeds new limit
        if (currentDayOfSeason > daysPerSeason)
        {
            currentDayOfSeason = daysPerSeason;
        }
    }

    /// <summary>
    /// Updates the day duration and ensures it's within valid bounds.
    /// </summary>
    public void SetDayDuration(float minutes)
    {
        dayDurationMinutes = Mathf.Max(0.1f, minutes);
    }

    /// <summary>
    /// Sets the current season and ensures day is within valid range.
    /// </summary>
    public void SetSeason(SeasonType season, int dayOfSeason = -1)
    {
        currentSeason = season;

        if (dayOfSeason > 0)
        {
            currentDayOfSeason = Mathf.Clamp(dayOfSeason, 1, daysPerSeason);
        }
        else if (currentDayOfSeason > daysPerSeason)
        {
            currentDayOfSeason = daysPerSeason;
        }
    }

    /// <summary>
    /// Sets the time of day and ensures it's within valid bounds.
    /// </summary>
    public void SetTimeOfDay(float hours)
    {
        currentTimeOfDay = Mathf.Clamp(hours, 0f, 23.99f);
    }

    /// <summary>
    /// Advances to the next day and handles season transitions.
    /// Returns true if the season changed.
    /// </summary>
    public bool AdvanceDay()
    {
        currentDayOfSeason++;
        totalDaysElapsed++;

        if (currentDayOfSeason > daysPerSeason)
        {
            currentDayOfSeason = 1;
            return AdvanceSeason();
        }

        return false;
    }

    /// <summary>
    /// Advances to the next season.
    /// Returns true (season always changes when this is called).
    /// </summary>
    public bool AdvanceSeason()
    {
        currentSeason = currentSeason switch
        {
            SeasonType.Spring => SeasonType.Summer,
            SeasonType.Summer => SeasonType.Fall,
            SeasonType.Fall => SeasonType.Winter,
            SeasonType.Winter => SeasonType.Spring,
            _ => SeasonType.Spring
        };

        return true;
    }

    #endregion

    #region Cozy Integration Helpers

    /// <summary>
    /// Gets the time in Cozy's MeridiemTime format for integration.
    /// </summary>
    public object GetCozyTime()
    {
        int hours = Mathf.FloorToInt(currentTimeOfDay);
        int minutes = Mathf.FloorToInt((currentTimeOfDay - hours) * 60f);

        // Return as object to avoid requiring Cozy namespace in save data
        // The save component will handle the actual MeridiemTime conversion
        return new { hours, minutes };
    }

    /// <summary>
    /// Calculates the time speed multiplier for Cozy integration.
    /// </summary>
    public float GetCozyTimeSpeed()
    {
        return 1440f / (dayDurationMinutes * 60f); // 1440 minutes in 24 hours
    }

    /// <summary>
    /// Gets configuration data for Cozy year setup.
    /// </summary>
    public object GetCozyYearConfig()
    {
        return new
        {
            daysPerYear = this.daysPerYear,
            daysPerSeason = this.daysPerSeason,
            currentDayOfYear = GetDayOfYear(),
            realisticYear = false // Use our custom system
        };
    }

    #endregion
}