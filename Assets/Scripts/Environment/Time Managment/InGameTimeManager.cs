using UnityEngine;
using System;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Simplified time manager that uses Cozy Weather 3's native season and calendar system.
/// Instead of trying to adapt Cozy to our custom system, we use Cozy's native approach.
/// 
/// PURE COZY APPROACH: Uses Cozy's MeridiemTime, day/year system, and season handling.
/// This provides the cleanest integration and lets Cozy handle all time complexity.
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
    [SerializeField] private MeridiemTime startTime = new MeridiemTime(6, 0);
    [SerializeField] private int startDay = 1;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Cozy connection state
    [ShowInInspector, ReadOnly] private bool isCozyConnected = false;
    [ShowInInspector, ReadOnly] private CozyTimeModule timeModule;
    [ShowInInspector, ReadOnly] private CozyClimateModule climateModule;
    [ShowInInspector, ReadOnly] private float lastCozyReadTime = 0f;

    // Current time state (using Cozy's native format)
    [ShowInInspector, ReadOnly] private MeridiemTime currentTime = new MeridiemTime(6, 0);
    [ShowInInspector, ReadOnly] private int currentDay = 1; // Cozy's day system
    [ShowInInspector, ReadOnly] private float currentTemperature = 20f;
    [ShowInInspector, ReadOnly] private object currentSeason; // Cozy's season object

    // Change tracking for events
    [ShowInInspector, ReadOnly] private MeridiemTime previousTime = new MeridiemTime(-1, 0);
    [ShowInInspector, ReadOnly] private int previousDay = -1;
    [ShowInInspector, ReadOnly] private object previousSeason;

    // Manual time progression (fallback when Cozy unavailable)
    private float manualTimeProgressionRate;

    // Events for external systems
    public static event Action<MeridiemTime> OnTimeChanged; // Cozy's time format
    public static event Action<int> OnDayChanged; // Cozy's day number
    public static event Action<object> OnSeasonChanged; // Cozy's season object
    public static event Action<TimeData> OnTimeDataUpdated; // Complete time data

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CalculateManualProgressionRate();
            DebugLog("InGameTimeManager initialized for pure Cozy integration");
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
            SetManualTime(startTime, startDay);
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
    /// Connects to Cozy Weather 3's time and climate systems
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
            climateModule = CozyWeather.instance.climateModule; // Optional
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
            previousTime = new MeridiemTime(currentTime.hours, currentTime.minutes);
            previousDay = currentDay;
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
    /// Reads time data from Cozy's native systems
    /// </summary>
    private void ReadCozyTimeData()
    {
        if (timeModule == null) return;

        // Read Cozy's native time format
        currentTime = timeModule.currentTime;
        currentDay = timeModule.currentDay;

        // Read additional data from climate module if available
        if (climateModule != null)
        {
            try
            {
                currentTemperature = climateModule.currentTemperature;

                // Try to get season information from climate module
                var seasonProperty = climateModule.GetType().GetProperty("currentSeason");
                if (seasonProperty != null)
                {
                    currentSeason = seasonProperty.GetValue(climateModule);
                }
            }
            catch (System.Exception e)
            {
                DebugLog($"Error reading climate data: {e.Message}");
            }
        }
    }

    #endregion

    #region Manual Time Progression (Fallback)

    /// <summary>
    /// Manual time progression when Cozy is not available
    /// </summary>
    private void ProgressTimeManually()
    {
        float timeAdvancement = manualTimeProgressionRate * Time.deltaTime * 60f; // Convert to minutes

        // Add minutes to current time
        int totalMinutes = (currentTime.hours * 60) + currentTime.minutes + Mathf.RoundToInt(timeAdvancement);

        // Handle day rollover
        if (totalMinutes >= 1440) // 24 hours * 60 minutes
        {
            totalMinutes -= 1440;
            currentDay++;
        }

        // Update time
        currentTime = new MeridiemTime(totalMinutes / 60, totalMinutes % 60);

        if (enableEventTracking)
        {
            CheckForTimeChanges();
        }
    }

    /// <summary>
    /// Calculates manual time progression rate
    /// </summary>
    private void CalculateManualProgressionRate()
    {
        manualTimeProgressionRate = 1440f / (dayDurationMinutes * 60f); // Minutes per second
    }

    /// <summary>
    /// Sets manual values (used when Cozy is not available)
    /// </summary>
    private void SetManualTime(MeridiemTime time, int day)
    {
        currentTime = new MeridiemTime(time.hours, time.minutes);
        currentDay = day;
    }

    #endregion

    #region Change Detection and Events

    /// <summary>
    /// Checks for time changes and fires appropriate events
    /// </summary>
    private void CheckForTimeChanges()
    {
        // Check for time changes (compare total minutes)
        int currentTotalMinutes = (currentTime.hours * 60) + currentTime.minutes;
        int previousTotalMinutes = (previousTime.hours * 60) + previousTime.minutes;

        if (Mathf.Abs(currentTotalMinutes - previousTotalMinutes) >= 1) // At least 1 minute change
        {
            OnTimeChanged?.Invoke(currentTime);
            previousTime = new MeridiemTime(currentTime.hours, currentTime.minutes);
        }

        // Check for day changes
        if (currentDay != previousDay)
        {
            DebugLog($"Day changed: {previousDay} → {currentDay}");
            OnDayChanged?.Invoke(currentDay);
            previousDay = currentDay;
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
    /// Manually sets the time (updates Cozy if connected)
    /// </summary>
    [Button("Set Time")]
    public void SetTime(int hours, int minutes)
    {
        var newTime = new MeridiemTime(hours, minutes);

        if (isCozyConnected && timeModule != null)
        {
            // Set time in Cozy
            timeModule.currentTime = newTime;
            DebugLog($"Set Cozy time to: {newTime}");
        }
        else
        {
            // Set manual time
            currentTime = newTime;
            DebugLog($"Set manual time to: {newTime}");
        }

        // Fire event
        OnTimeChanged?.Invoke(currentTime);
    }

    /// <summary>
    /// Manually sets the time using float hours (for convenience)
    /// </summary>
    public void SetTimeOfDay(float hours)
    {
        int h = Mathf.FloorToInt(hours);
        int m = Mathf.FloorToInt((hours - h) * 60f);
        SetTime(h, m);
    }

    /// <summary>
    /// Manually sets the current day (updates Cozy if connected)
    /// </summary>
    [Button("Set Day")]
    public void SetDay(int day)
    {
        day = Mathf.Max(1, day);

        if (isCozyConnected && timeModule != null)
        {
            // Set day in Cozy
            timeModule.currentDay = day;
            DebugLog($"Set Cozy day to: {day}");
        }
        else
        {
            // Set manual day
            currentDay = day;
            DebugLog($"Set manual day to: {day}");
        }

        // Fire event
        OnDayChanged?.Invoke(currentDay);
    }

    /// <summary>
    /// Sets complete date and time using Cozy's native format
    /// </summary>
    [Button("Set DateTime")]
    public void SetDateTime(int hours, int minutes, int day)
    {
        SetTime(hours, minutes);
        SetDay(day);
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
        SetDateTime(timeData.time.hours, timeData.time.minutes, timeData.day);

        DebugLog($"Time data restored successfully");
    }

    /// <summary>
    /// Internal method for save component to get current time data
    /// </summary>
    internal TimeData GetTimeDataForSaving()
    {
        var timeData = new TimeData
        {
            time = new MeridiemTime(currentTime.hours, currentTime.minutes),
            day = currentDay,
            temperature = currentTemperature,
            season = currentSeason?.ToString() ?? "Unknown",
            wasCozyDriven = isCozyConnected,
            saveTimestamp = System.DateTime.Now
        };

        DebugLog($"Providing time data for saving: {timeData.GetFormattedDateTime()}");
        return timeData;
    }

    #endregion

    #region Public API for External Scripts

    /// <summary>
    /// Gets current time in Cozy's MeridiemTime format
    /// </summary>
    public MeridiemTime GetCurrentTime() => currentTime;

    /// <summary>
    /// Gets current time as float (0-24 hours) for compatibility
    /// </summary>
    public float GetCurrentTimeOfDay() => currentTime.hours + (currentTime.minutes / 60f);

    /// <summary>
    /// Gets current day using Cozy's day system
    /// </summary>
    public int GetCurrentDay() => currentDay;

    /// <summary>
    /// Gets current season (Cozy's season object)
    /// </summary>
    public object GetCurrentSeason() => currentSeason;

    /// <summary>
    /// Gets current season as string for display
    /// </summary>
    public string GetCurrentSeasonString() => currentSeason?.ToString() ?? "Unknown";

    /// <summary>
    /// Gets current temperature from Cozy
    /// </summary>
    public float GetCurrentTemperature() => currentTemperature;

    /// <summary>
    /// Checks if it's currently daytime (6 AM to 6 PM)
    /// </summary>
    public bool IsDaytime() => currentTime.hours >= 6 && currentTime.hours < 18;

    /// <summary>
    /// Checks if it's currently nighttime
    /// </summary>
    public bool IsNighttime() => !IsDaytime();

    /// <summary>
    /// Checks if Cozy time system is connected and active
    /// </summary>
    public bool IsCozyConnected() => isCozyConnected;

    /// <summary>
    /// Gets complete time data structure for external use
    /// </summary>
    public TimeData GetCurrentTimeData()
    {
        return new TimeData
        {
            time = new MeridiemTime(currentTime.hours, currentTime.minutes),
            day = currentDay,
            temperature = currentTemperature,
            season = currentSeason?.ToString() ?? "Unknown",
            wasCozyDriven = isCozyConnected,
            saveTimestamp = System.DateTime.Now
        };
    }

    /// <summary>
    /// Gets formatted time string (HH:MM format)
    /// </summary>
    public string GetFormattedTime()
    {
        return $"{currentTime.hours:D2}:{currentTime.minutes:D2}";
    }

    /// <summary>
    /// Gets formatted date string using Cozy's day system
    /// </summary>
    public string GetFormattedDate()
    {
        string seasonStr = GetCurrentSeasonString();
        return $"Day {currentDay} ({seasonStr})";
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
        int newHour = currentTime.hours + 1;
        if (newHour >= 24) newHour = 0;
        SetTime(newHour, currentTime.minutes);
    }

    /// <summary>
    /// Advances to next day (for testing)
    /// </summary>
    [Button("Advance 1 Day")]
    public void AdvanceOneDay()
    {
        SetDay(currentDay + 1);
    }

    /// <summary>
    /// Shows current Cozy time information
    /// </summary>
    [Button("Show Cozy Time Info")]
    public void ShowCozyTimeInfo()
    {
        DebugLog("=== COZY TIME INFORMATION ===");
        DebugLog($"Connected to Cozy: {isCozyConnected}");
        DebugLog($"Current Time: {currentTime} ({GetCurrentTimeOfDay():F2})");
        DebugLog($"Current Day: {currentDay}");
        DebugLog($"Current Season: {GetCurrentSeasonString()}");
        DebugLog($"Current Temperature: {currentTemperature:F1}°C");
        DebugLog($"Is Daytime: {IsDaytime()}");

        if (isCozyConnected && timeModule != null)
        {
            DebugLog("Raw Cozy Data:");
            DebugLog($"  timeModule.currentTime: {timeModule.currentTime}");
            DebugLog($"  timeModule.currentDay: {timeModule.currentDay}");

            if (climateModule != null)
            {
                DebugLog($"  climateModule.currentTemperature: {climateModule.currentTemperature}");
            }
        }
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
