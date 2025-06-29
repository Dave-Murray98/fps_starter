using UnityEngine;
using System;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Simplified time manager that lets Cozy Weather 3 handle all time progression while providing
/// a clean interface for saving/loading time data and accessing current time information.
/// 
/// COZY-DRIVEN APPROACH: Cozy controls time progression, we just monitor and persist it.
/// This provides the simplest possible integration while maintaining all functionality.
/// </summary>
public class InGameTimeManager : MonoBehaviour, IManager
{
    public static InGameTimeManager Instance { get; private set; }

    [Header("Cozy Integration")]
    [SerializeField] private bool useCozyTimeSystem = true;
    [SerializeField] private float cozyReadInterval = 0.1f; // How often to read from Cozy
    [SerializeField] private bool enableEventTracking = true;

    [Header("Manual Fallback Settings (if Cozy unavailable)")]
    [SerializeField] private float dayDurationMinutes = 20f;
    [SerializeField] private float startTimeOfDay = 6f;

    [Header("Season Configuration")]
    [SerializeField] private int daysPerSeason = 30;
    [SerializeField] private SeasonType startingSeason = SeasonType.Spring;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Cozy connection state
    [ShowInInspector, ReadOnly] private bool isCozyConnected = false;
    [ShowInInspector, ReadOnly] private CozyTimeModule timeModule;
    [ShowInInspector, ReadOnly] private float lastCozyReadTime = 0f;

    // Current time state (read from Cozy or calculated manually)
    [ShowInInspector, ReadOnly] private float currentTimeOfDay = 6f; // 0-24 hours
    [ShowInInspector, ReadOnly] private int currentDayOfYear = 1; // 1-based day of year
    [ShowInInspector, ReadOnly] private SeasonType currentSeason = SeasonType.Spring;
    [ShowInInspector, ReadOnly] private int currentDayOfSeason = 1;

    // Change tracking for events
    [ShowInInspector, ReadOnly] private float previousTimeOfDay = -1f;
    [ShowInInspector, ReadOnly] private int previousDayOfYear = -1;
    [ShowInInspector, ReadOnly] private SeasonType previousSeason = (SeasonType)(-1);

    // Manual time progression (fallback when Cozy unavailable)
    private float manualTimeProgressionRate;

    // Events for external systems
    public static event Action<float> OnTimeChanged; // Current time (0-24)
    public static event Action<int, SeasonType> OnDayChanged; // Day of season, current season
    public static event Action<SeasonType> OnSeasonChanged; // New season
    public static event Action<TimeData> OnTimeDataUpdated; // Complete time data

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CalculateManualProgressionRate();
            DebugLog("InGameTimeManager initialized as Cozy time interface");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (useCozyTimeSystem && isCozyConnected)
        {
            ReadFromCozy();
        }
        else if (!useCozyTimeSystem && Time.timeScale > 0f)
        {
            ProgressTimeManually();
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        ConnectToCozy();

        if (isCozyConnected)
        {
            ReadInitialCozyState();
        }
        else
        {
            // Set initial manual values
            SetManualTime(startTimeOfDay, currentSeason, 1);
        }

        DebugLog($"Initialized - {GetFormattedDateTime()}");
    }

    public void RefreshReferences()
    {
        ConnectToCozy();
        DebugLog("References refreshed");
    }

    public void Cleanup()
    {
        DebugLog("Cleanup completed");
    }

    #endregion

    #region Cozy Integration

    /// <summary>
    /// Connects to Cozy Weather 3's time system
    /// </summary>
    private void ConnectToCozy()
    {
        if (!useCozyTimeSystem)
        {
            isCozyConnected = false;
            DebugLog("Cozy time system disabled - using manual progression");
            return;
        }

        if (CozyWeather.instance?.timeModule != null)
        {
            timeModule = CozyWeather.instance.timeModule;
            isCozyConnected = true;
            DebugLog("Successfully connected to Cozy time system");
        }
        else
        {
            isCozyConnected = false;
            DebugLog("Cozy time module not found - using manual progression");
        }
    }

    /// <summary>
    /// Reads initial time state from Cozy
    /// </summary>
    private void ReadInitialCozyState()
    {
        if (!isCozyConnected) return;

        try
        {
            ReadCozyTimeData();

            // Initialize previous values for change detection
            previousTimeOfDay = currentTimeOfDay;
            previousDayOfYear = currentDayOfYear;
            previousSeason = currentSeason;

            DebugLog($"Initial Cozy time: {GetFormattedDateTime()}");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading initial Cozy state: {e.Message}");
        }
    }

    /// <summary>
    /// Reads current time data from Cozy at regular intervals
    /// </summary>
    private void ReadFromCozy()
    {
        if (Time.time - lastCozyReadTime < cozyReadInterval) return;
        lastCozyReadTime = Time.time;

        if (!isCozyConnected)
        {
            ConnectToCozy();
            return;
        }

        try
        {
            ReadCozyTimeData();

            if (enableEventTracking)
            {
                CheckForTimeChanges();
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading from Cozy: {e.Message}");
            isCozyConnected = false;
        }
    }

    /// <summary>
    /// Reads time data from Cozy's time module
    /// </summary>
    private void ReadCozyTimeData()
    {
        if (timeModule == null) return;

        // Read current time
        var currentTime = timeModule.currentTime;
        currentTimeOfDay = currentTime.hours + (currentTime.minutes / 60f);

        // Read day of year (Cozy uses 1-based indexing)
        currentDayOfYear = Mathf.Max(1, timeModule.currentDay);

        // Calculate season and day of season from day of year
        CalculateSeasonFromDayOfYear();
    }

    /// <summary>
    /// Calculates current season and day of season from day of year
    /// </summary>
    private void CalculateSeasonFromDayOfYear()
    {
        if (daysPerSeason <= 0) return;

        // Calculate which season we're in (0-based)
        int seasonIndex = (currentDayOfYear - 1) / daysPerSeason;
        seasonIndex = seasonIndex % 4; // Wrap around after 4 seasons

        // Calculate day within the current season (1-based)
        currentDayOfSeason = ((currentDayOfYear - 1) % daysPerSeason) + 1;

        // Set current season
        currentSeason = (SeasonType)seasonIndex;
    }

    #endregion

    #region Manual Time Progression (Fallback)

    /// <summary>
    /// Manual time progression when Cozy is not available
    /// </summary>
    private void ProgressTimeManually()
    {
        float timeAdvancement = manualTimeProgressionRate * Time.deltaTime;
        currentTimeOfDay += timeAdvancement;

        // Handle day rollover
        if (currentTimeOfDay >= 24f)
        {
            currentTimeOfDay -= 24f;
            AdvanceDay();
        }

        if (enableEventTracking)
        {
            CheckForTimeChanges();
        }
    }

    /// <summary>
    /// Advances to the next day in manual mode
    /// </summary>
    private void AdvanceDay()
    {
        currentDayOfYear++;
        CalculateSeasonFromDayOfYear();
        DebugLog($"Day advanced to: {GetFormattedDateTime()}");
    }

    /// <summary>
    /// Calculates manual time progression rate
    /// </summary>
    private void CalculateManualProgressionRate()
    {
        manualTimeProgressionRate = 24f / (dayDurationMinutes * 60f);
    }

    #endregion

    #region Change Detection and Events

    /// <summary>
    /// Checks for time changes and fires appropriate events
    /// </summary>
    private void CheckForTimeChanges()
    {
        // Check for time of day changes
        if (Mathf.Abs(currentTimeOfDay - previousTimeOfDay) > 0.01f)
        {
            OnTimeChanged?.Invoke(currentTimeOfDay);
            previousTimeOfDay = currentTimeOfDay;
        }

        // Check for day changes
        if (currentDayOfYear != previousDayOfYear)
        {
            DebugLog($"Day changed: {previousDayOfYear} → {currentDayOfYear}");
            OnDayChanged?.Invoke(currentDayOfSeason, currentSeason);
            previousDayOfYear = currentDayOfYear;
        }

        // Check for season changes
        if (currentSeason != previousSeason)
        {
            DebugLog($"Season changed: {previousSeason} → {currentSeason}");
            OnSeasonChanged?.Invoke(currentSeason);
            previousSeason = currentSeason;
        }

        // Fire comprehensive update event
        OnTimeDataUpdated?.Invoke(GetCurrentTimeData());
    }

    #endregion

    #region Public Time Control API

    /// <summary>
    /// Manually sets the time of day (updates Cozy if connected)
    /// </summary>
    [Button("Set Time of Day")]
    public void SetTimeOfDay(float hours)
    {
        hours = Mathf.Clamp(hours, 0f, 23.99f);

        if (isCozyConnected && timeModule != null)
        {
            // Set time in Cozy
            int h = Mathf.FloorToInt(hours);
            int m = Mathf.FloorToInt((hours - h) * 60f);
            timeModule.currentTime = new MeridiemTime(h, m);
            DebugLog($"Set Cozy time to: {h:D2}:{m:D2}");
        }
        else
        {
            // Set manual time
            currentTimeOfDay = hours;
            DebugLog($"Set manual time to: {GetFormattedTime()}");
        }

        // Fire event
        OnTimeChanged?.Invoke(currentTimeOfDay);
    }

    /// <summary>
    /// Manually sets the current day of year (updates Cozy if connected)
    /// </summary>
    [Button("Set Day of Year")]
    public void SetDayOfYear(int dayOfYear)
    {
        dayOfYear = Mathf.Max(1, dayOfYear);

        if (isCozyConnected && timeModule != null)
        {
            // Set day in Cozy
            timeModule.currentDay = dayOfYear;
            DebugLog($"Set Cozy day to: {dayOfYear}");
        }
        else
        {
            // Set manual day
            currentDayOfYear = dayOfYear;
            CalculateSeasonFromDayOfYear();
            DebugLog($"Set manual day to: {dayOfYear}");
        }

        // Fire events
        OnDayChanged?.Invoke(currentDayOfSeason, currentSeason);
    }

    /// <summary>
    /// Manually sets season and day within season
    /// </summary>
    [Button("Set Season")]
    public void SetSeason(SeasonType season, int dayOfSeason = 1)
    {
        dayOfSeason = Mathf.Clamp(dayOfSeason, 1, daysPerSeason);

        // Calculate day of year from season and day
        int seasonOffset = (int)season * daysPerSeason;
        int targetDayOfYear = seasonOffset + dayOfSeason;

        SetDayOfYear(targetDayOfYear);
        DebugLog($"Set season to: {season}, day {dayOfSeason}");
    }

    /// <summary>
    /// Sets complete date and time
    /// </summary>
    [Button("Set Complete DateTime")]
    public void SetDateTime(SeasonType season, int dayOfSeason, float timeOfDay)
    {
        SetSeason(season, dayOfSeason);
        SetTimeOfDay(timeOfDay);
        DebugLog($"Set complete date/time to: {GetFormattedDateTime()}");
    }

    #endregion

    #region Internal Time Management (Save Component Interface)

    /// <summary>
    /// Internal method for save component to set time data during restoration
    /// </summary>
    internal void RestoreTimeData(TimeData timeData)
    {
        if (timeData == null) return;

        DebugLog($"Restoring time data: {timeData.GetFormattedDateTime()}");

        // Apply the time data using our public API
        SetDateTime(timeData.season, timeData.dayOfSeason, timeData.timeOfDay);

        DebugLog($"Time data restored successfully");
    }

    /// <summary>
    /// Internal method for save component to get current time data
    /// </summary>
    internal TimeData GetTimeDataForSaving()
    {
        var timeData = new TimeData
        {
            timeOfDay = currentTimeOfDay,
            dayOfYear = currentDayOfYear,
            dayOfSeason = currentDayOfSeason,
            season = currentSeason,
            daysPerSeason = daysPerSeason,
            wasCozyDriven = isCozyConnected,
            saveTimestamp = System.DateTime.Now
        };

        DebugLog($"Providing time data for saving: {timeData.GetFormattedDateTime()}");
        return timeData;
    }

    /// <summary>
    /// Sets manual values (used internally when Cozy is not available)
    /// </summary>
    private void SetManualTime(float timeOfDay, SeasonType season, int dayOfSeason)
    {
        currentTimeOfDay = timeOfDay;
        currentSeason = season;
        currentDayOfSeason = dayOfSeason;
        currentDayOfYear = ((int)season * daysPerSeason) + dayOfSeason;
    }

    #endregion

    #region Public API for External Scripts

    /// <summary>
    /// Gets current time of day (0-24 hours)
    /// </summary>
    public float GetCurrentTimeOfDay() => currentTimeOfDay;

    /// <summary>
    /// Gets current season
    /// </summary>
    public SeasonType GetCurrentSeason() => currentSeason;

    /// <summary>
    /// Gets current day of season (1-based)
    /// </summary>
    public int GetCurrentDayOfSeason() => currentDayOfSeason;

    /// <summary>
    /// Gets current day of year (1-based)
    /// </summary>
    public int GetCurrentDayOfYear() => currentDayOfYear;

    /// <summary>
    /// Checks if it's currently daytime (6 AM to 6 PM)
    /// </summary>
    public bool IsDaytime() => currentTimeOfDay >= 6f && currentTimeOfDay < 18f;

    /// <summary>
    /// Checks if it's currently nighttime
    /// </summary>
    public bool IsNighttime() => !IsDaytime();

    /// <summary>
    /// Checks if Cozy time system is connected and active
    /// </summary>
    public bool IsCozyConnected() => isCozyConnected;

    /// <summary>
    /// Gets days per season setting
    /// </summary>
    public int GetDaysPerSeason() => daysPerSeason;

    /// <summary>
    /// Gets total days per year (4 seasons)
    /// </summary>
    public int GetDaysPerYear() => daysPerSeason * 4;

    /// <summary>
    /// Gets complete time data structure for external use (read-only)
    /// </summary>
    public TimeData GetCurrentTimeData()
    {
        return new TimeData
        {
            timeOfDay = currentTimeOfDay,
            dayOfYear = currentDayOfYear,
            dayOfSeason = currentDayOfSeason,
            season = currentSeason,
            daysPerSeason = daysPerSeason,
            wasCozyDriven = isCozyConnected,
            saveTimestamp = System.DateTime.Now
        };
    }

    /// <summary>
    /// Gets formatted time string (HH:MM format)
    /// </summary>
    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentTimeOfDay);
        int minutes = Mathf.FloorToInt((currentTimeOfDay - hours) * 60f);
        return $"{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// Gets formatted date string
    /// </summary>
    public string GetFormattedDate()
    {
        return $"Day {currentDayOfSeason} of {currentSeason}";
    }

    /// <summary>
    /// Gets formatted date and time string
    /// </summary>
    public string GetFormattedDateTime()
    {
        return $"{GetFormattedDate()} at {GetFormattedTime()}";
    }

    #endregion

    #region Manual Controls (For Testing)

    /// <summary>
    /// Forces immediate read from Cozy
    /// </summary>
    [Button("Force Read From Cozy")]
    public void ForceReadFromCozy()
    {
        if (isCozyConnected)
        {
            lastCozyReadTime = 0f;
            ReadFromCozy();
            DebugLog("Forced read from Cozy completed");
        }
        else
        {
            DebugLog("Not connected to Cozy - cannot force read");
        }
    }

    /// <summary>
    /// Reconnects to Cozy time system
    /// </summary>
    [Button("Reconnect to Cozy")]
    public void ReconnectToCozy()
    {
        ConnectToCozy();
        if (isCozyConnected)
        {
            ReadInitialCozyState();
            DebugLog("Reconnected to Cozy time system");
        }
    }

    /// <summary>
    /// Toggles between Cozy and manual time progression
    /// </summary>
    [Button("Toggle Cozy Time")]
    public void ToggleCozyTime()
    {
        useCozyTimeSystem = !useCozyTimeSystem;

        if (useCozyTimeSystem)
        {
            ConnectToCozy();
            if (isCozyConnected)
            {
                ReadInitialCozyState();
            }
        }

        DebugLog($"Cozy time system {(useCozyTimeSystem ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Advances time by one hour (for testing)
    /// </summary>
    [Button("Advance 1 Hour")]
    public void AdvanceOneHour()
    {
        SetTimeOfDay(currentTimeOfDay + 1f);
    }

    /// <summary>
    /// Advances to next day (for testing)
    /// </summary>
    [Button("Advance 1 Day")]
    public void AdvanceOneDay()
    {
        SetDayOfYear(currentDayOfYear + 1);
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[InGameTimeManager] {message}");
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            CalculateManualProgressionRate();
        }
    }
}

/// <summary>
/// Enum representing the four seasons
/// </summary>
[Serializable]
public enum SeasonType
{
    Spring = 0,
    Summer = 1,
    Fall = 2,
    Winter = 3
}