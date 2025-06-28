using UnityEngine;
using System;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Manages the day/night cycle and game time progression using Cozy Weather 3 as the time driver.
/// 
/// COZY-DRIVEN APPROACH: Cozy handles smooth time progression and visual transitions,
/// while this manager translates Cozy's time into your game logic (seasons, days, events).
/// This provides smooth visuals while maintaining your existing game systems.
/// 
/// Your game logic, save system, and UI remain exactly the same - only the time source changes.
/// </summary>
public class InGameTimeManager : MonoBehaviour, IManager
{
    public static InGameTimeManager Instance { get; private set; }

    [Header("Time Configuration")]
    [SerializeField] public float dayDurationMinutes = 20f; // Real-time minutes per game day
    [SerializeField] public float startTimeOfDay = 6f; // Starting hour (0-24)

    [Header("Season & Date Configuration")]
    [SerializeField] public int daysPerSeason = 30;
    [SerializeField] public SeasonType startingSeason = SeasonType.Spring;
    [SerializeField] public int startingDayOfSeason = 1;

    [Header("Cozy Integration Settings")]
    [SerializeField] private bool useCozyTimeProgression = true;
    [SerializeField] private bool initializeCozyOnStart = true;
    [SerializeField] private float cozyReadInterval = 0.05f; // How often to read from Cozy
    [SerializeField] private int daysPerYear = 120;

    [Header("Manual Override (When Cozy Disabled)")]
    [SerializeField] private bool enableManualProgression = true;

    [Header("Event Timing")]
    [SerializeField] private float eventFireIntervalSeconds = 0.5f;
    [SerializeField] private bool forceEventOnSignificantChange = true;

    [Header("Debug Settings")]
    public bool showDebugLogs = true;

    // Current time state (derived from Cozy or calculated manually)
    [ShowInInspector, ReadOnly] private float currentTimeOfDay = 6f; // 0-24 hours
    [ShowInInspector, ReadOnly] private SeasonType currentSeason = SeasonType.Spring;
    [ShowInInspector, ReadOnly] private int currentDayOfSeason = 1;
    [ShowInInspector, ReadOnly] private int totalDaysElapsed = 0;

    // Cozy integration state
    [ShowInInspector, ReadOnly] private bool isCozyConnected = false;
    [ShowInInspector, ReadOnly] private float lastCozyReadTime = 0f;
    [ShowInInspector, ReadOnly] private float previousCozyTime = -1f;
    [ShowInInspector, ReadOnly] private int previousCozyDay = -1;

    // Manual time progression (fallback)
    [ShowInInspector, ReadOnly] private float timeProgressionRate;
    [ShowInInspector, ReadOnly] private float timeSinceLastEvent = 0f;
    private float lastFiredTimeOfDay = -1f;
    private float minEventGameTimeInterval = 0.0167f;

    // Events for external systems (preserved for compatibility)
    public static event Action<float> OnTimeChanged; // Current time (0-24)
    public static event Action<int> OnDayChanged; // Day of season
    public static event Action<SeasonType> OnSeasonChanged; // New season

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CalculateTimeProgressionRate();
            DebugLog("InGameTimeManager initialized");
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
        if (useCozyTimeProgression && isCozyConnected)
        {
            ReadFromCozy();
        }
        else if (enableManualProgression && Time.timeScale > 0f)
        {
            ProgressTimeManually();
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        ConnectToCozy();

        if (initializeCozyOnStart && isCozyConnected)
        {
            InitializeCozySettings();
        }
        else
        {
            // Set initial values manually
            SetTimeOfDay(startTimeOfDay);
            SetGameDate(startingSeason, startingDayOfSeason, currentTimeOfDay);
        }

        DebugLog($"Initialized - {GetFormattedDate()} at {GetFormattedTime()}");
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

    #region Cozy Integration - Reading from Cozy

    /// <summary>
    /// Connects to Cozy Weather 3 system
    /// </summary>
    private void ConnectToCozy()
    {
        if (CozyWeather.instance != null && CozyWeather.instance.timeModule != null)
        {
            isCozyConnected = true;
            DebugLog("Successfully connected to Cozy Weather 3");
        }
        else
        {
            isCozyConnected = false;
            DebugLog("Cozy Weather 3 not found - using manual time progression");
        }
    }

    /// <summary>
    /// Sets up Cozy with our initial configuration (day length, year structure)
    /// </summary>
    private void InitializeCozySettings()
    {
        if (!isCozyConnected) return;

        try
        {
            var timeModule = CozyWeather.instance.timeModule;

            // Set up year structure
            daysPerYear = daysPerSeason * 4;
            SetupCozyYearStructure(timeModule);

            // Set time progression speed to match our day duration
            SetupCozyTimeSpeed(timeModule);

            // Set initial time and day
            SetInitialCozyTime(timeModule);

            DebugLog("Cozy initialized with our settings");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error initializing Cozy: {e.Message}");
        }
    }

    /// <summary>
    /// Reads current time from Cozy and translates to our game logic
    /// </summary>
    private void ReadFromCozy()
    {
        // Read at intervals to avoid excessive processing
        if (Time.time - lastCozyReadTime < cozyReadInterval) return;
        lastCozyReadTime = Time.time;

        if (!isCozyConnected)
        {
            ConnectToCozy();
            return;
        }

        try
        {
            var timeModule = CozyWeather.instance.timeModule;
            MeridiemTime cozyTime = timeModule.currentTime;

            // Convert Cozy's time to our format
            float newTimeOfDay = cozyTime.hours + (cozyTime.minutes / 60f);

            // Get day of year from Cozy (if available)
            int cozyDayOfYear = GetCozyDayOfYear(timeModule);

            // Update our time state
            UpdateTimeFromCozy(newTimeOfDay, cozyDayOfYear);
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading from Cozy: {e.Message}");
            isCozyConnected = false;
        }
    }

    /// <summary>
    /// Updates our internal time state based on Cozy's values
    /// </summary>
    private void UpdateTimeFromCozy(float newTimeOfDay, int cozyDayOfYear)
    {
        int previousDay = currentDayOfSeason;
        SeasonType previousSeason = currentSeason;

        // Detect day changes by monitoring Cozy's day transitions
        if (previousCozyTime >= 0f && newTimeOfDay < previousCozyTime && previousCozyTime > 20f && newTimeOfDay < 4f)
        {
            // Day rollover detected (time went from ~23:xx to ~0:xx)
            AdvanceDay();
        }
        else if (cozyDayOfYear >= 0 && cozyDayOfYear != previousCozyDay)
        {
            // Day changed according to Cozy's day counter
            TranslateDayFromCozy(cozyDayOfYear);
        }

        // Update current time
        currentTimeOfDay = newTimeOfDay;

        // Fire events based on time changes
        bool significantTimeChange = Mathf.Abs(newTimeOfDay - previousCozyTime) > 0.01f;
        if (significantTimeChange)
        {
            timeSinceLastEvent += Mathf.Abs(newTimeOfDay - previousCozyTime);

            if (timeSinceLastEvent >= minEventGameTimeInterval)
            {
                OnTimeChanged?.Invoke(currentTimeOfDay);
                timeSinceLastEvent = 0f;
                lastFiredTimeOfDay = currentTimeOfDay;
            }
        }

        // Fire day/season change events
        if (currentDayOfSeason != previousDay)
        {
            OnDayChanged?.Invoke(currentDayOfSeason);
        }

        if (currentSeason != previousSeason)
        {
            OnSeasonChanged?.Invoke(currentSeason);
        }

        // Cache current values
        previousCozyTime = newTimeOfDay;
        previousCozyDay = cozyDayOfYear;
    }

    /// <summary>
    /// Translates Cozy's day of year to our season/day system
    /// </summary>
    private void TranslateDayFromCozy(int cozyDayOfYear)
    {
        if (cozyDayOfYear <= 0 || daysPerSeason <= 0) return;

        // Calculate which season and day within season
        int seasonIndex = (cozyDayOfYear - 1) / daysPerSeason;
        int dayInSeason = ((cozyDayOfYear - 1) % daysPerSeason) + 1;

        // Clamp to valid ranges
        seasonIndex = Mathf.Clamp(seasonIndex, 0, 3);
        dayInSeason = Mathf.Clamp(dayInSeason, 1, daysPerSeason);

        currentSeason = (SeasonType)seasonIndex;
        currentDayOfSeason = dayInSeason;
        totalDaysElapsed = cozyDayOfYear - 1;

        DebugLog($"Translated Cozy day {cozyDayOfYear} to {currentSeason} day {currentDayOfSeason}");
    }

    #endregion

    #region Cozy Setup Helpers

    /// <summary>
    /// Configures Cozy's year structure to match our season system
    /// </summary>
    private void SetupCozyYearStructure(object timeModule)
    {
        try
        {
            // Set total days per year
            if (HasProperty(timeModule, "daysPerYear"))
            {
                SetProperty(timeModule, "daysPerYear", daysPerYear);
            }
            else if (HasProperty(timeModule, "yearLength"))
            {
                SetProperty(timeModule, "yearLength", daysPerYear);
            }

            // Disable realistic year if possible (use our custom system)
            if (HasProperty(timeModule, "realisticYear"))
            {
                SetProperty(timeModule, "realisticYear", false);
            }

            DebugLog($"Set Cozy year structure: {daysPerYear} days per year");
        }
        catch (System.Exception e)
        {
            DebugLog($"Could not configure Cozy year structure: {e.Message}");
        }
    }

    /// <summary>
    /// Sets Cozy's time progression speed to match our day duration
    /// </summary>
    private void SetupCozyTimeSpeed(object timeModule)
    {
        try
        {
            // Calculate the time speed multiplier
            float cozyTimeSpeed = 1440f / (dayDurationMinutes * 60f); // 1440 minutes in 24 hours

            // Try different property names that Cozy might use
            bool speedSet = false;
            string[] speedProperties = { "timeSpeed", "speedMultiplier", "daySpeed", "progressionSpeed" };

            foreach (string prop in speedProperties)
            {
                if (HasProperty(timeModule, prop))
                {
                    SetProperty(timeModule, prop, cozyTimeSpeed);
                    DebugLog($"Set Cozy {prop} to: {cozyTimeSpeed:F4}");
                    speedSet = true;
                    break;
                }
            }

            if (!speedSet)
            {
                DebugLog("Could not find time speed property in Cozy - you may need to set it manually");
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error setting Cozy time speed: {e.Message}");
        }
    }

    /// <summary>
    /// Sets Cozy's initial time and day to match our starting values
    /// </summary>
    private void SetInitialCozyTime(object timeModule)
    {
        try
        {
            // Set initial time
            int hours = Mathf.FloorToInt(startTimeOfDay);
            int minutes = Mathf.FloorToInt((startTimeOfDay - hours) * 60f);
            MeridiemTime initialTime = new MeridiemTime(hours, minutes);
            timeModule.GetType().GetProperty("currentTime")?.SetValue(timeModule, initialTime);

            // Set initial day of year
            int initialDayOfYear = CalculateDayOfYear(startingSeason, startingDayOfSeason);
            if (HasProperty(timeModule, "currentDay"))
            {
                SetProperty(timeModule, "currentDay", initialDayOfYear);
            }
            else if (HasProperty(timeModule, "dayOfYear"))
            {
                SetProperty(timeModule, "dayOfYear", initialDayOfYear);
            }

            // Update our state to match
            currentTimeOfDay = startTimeOfDay;
            currentSeason = startingSeason;
            currentDayOfSeason = startingDayOfSeason;

            DebugLog($"Set Cozy initial state: {GetFormattedDateTime()}");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error setting Cozy initial time: {e.Message}");
        }
    }

    /// <summary>
    /// Gets the current day of year from Cozy (if available)
    /// </summary>
    private int GetCozyDayOfYear(object timeModule)
    {
        try
        {
            if (HasProperty(timeModule, "currentDay"))
            {
                var prop = timeModule.GetType().GetProperty("currentDay");
                if (prop != null && prop.PropertyType == typeof(int))
                {
                    return (int)prop.GetValue(timeModule);
                }
            }
            else if (HasProperty(timeModule, "dayOfYear"))
            {
                var prop = timeModule.GetType().GetProperty("dayOfYear");
                if (prop != null && prop.PropertyType == typeof(int))
                {
                    return (int)prop.GetValue(timeModule);
                }
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error getting Cozy day of year: {e.Message}");
        }

        return -1; // Not available
    }

    #endregion

    #region Manual Time Progression (Fallback)

    /// <summary>
    /// Manual time progression when Cozy is not available (your original logic)
    /// </summary>
    private void ProgressTimeManually()
    {
        int previousDay = currentDayOfSeason;
        SeasonType previousSeason = currentSeason;

        // Progress time
        float timeAdvancement = timeProgressionRate * Time.deltaTime;
        currentTimeOfDay += timeAdvancement;
        timeSinceLastEvent += timeAdvancement;

        // Handle day rollover
        if (currentTimeOfDay >= 24f)
        {
            currentTimeOfDay -= 24f;
            AdvanceDay();
        }

        // Fire time events
        if (timeSinceLastEvent >= minEventGameTimeInterval)
        {
            OnTimeChanged?.Invoke(currentTimeOfDay);
            timeSinceLastEvent = 0f;
            lastFiredTimeOfDay = currentTimeOfDay;
        }

        // Check for significant time jumps
        if (forceEventOnSignificantChange && lastFiredTimeOfDay >= 0f)
        {
            float timeDifference = Mathf.Abs(currentTimeOfDay - lastFiredTimeOfDay);
            if (timeDifference > 12f) timeDifference = 24f - timeDifference;

            if (timeDifference > minEventGameTimeInterval * 2f)
            {
                OnTimeChanged?.Invoke(currentTimeOfDay);
                timeSinceLastEvent = 0f;
                lastFiredTimeOfDay = currentTimeOfDay;
            }
        }

        // Fire day/season events
        if (currentDayOfSeason != previousDay)
        {
            OnDayChanged?.Invoke(currentDayOfSeason);
        }

        if (currentSeason != previousSeason)
        {
            OnSeasonChanged?.Invoke(currentSeason);
        }
    }

    #endregion

    #region Day and Season Management (Your Original Logic)

    /// <summary>
    /// Advances to the next day and handles season transitions
    /// </summary>
    private void AdvanceDay()
    {
        currentDayOfSeason++;
        totalDaysElapsed++;

        DebugLog($"Day advanced to: {GetFormattedDate()}");

        // Check for season transition
        if (currentDayOfSeason > daysPerSeason)
        {
            currentDayOfSeason = 1;
            AdvanceSeason();
        }
    }

    /// <summary>
    /// Advances to the next season in the cycle
    /// </summary>
    private void AdvanceSeason()
    {
        SeasonType previousSeason = currentSeason;

        currentSeason = currentSeason switch
        {
            SeasonType.Spring => SeasonType.Summer,
            SeasonType.Summer => SeasonType.Fall,
            SeasonType.Fall => SeasonType.Winter,
            SeasonType.Winter => SeasonType.Spring,
            _ => SeasonType.Spring
        };

        DebugLog($"Season changed from {previousSeason} to {currentSeason}");
    }

    #endregion

    #region Manual Control Methods (For Save System and Manual Override)

    /// <summary>
    /// Manually sets the time of day (updates Cozy if connected)
    /// </summary>
    [Button("Set Time of Day")]
    public void SetTimeOfDay(float hours)
    {
        hours = Mathf.Clamp(hours, 0f, 23.99f);
        currentTimeOfDay = hours;

        // Update Cozy if connected
        if (isCozyConnected)
        {
            try
            {
                var timeModule = CozyWeather.instance.timeModule;
                int h = Mathf.FloorToInt(hours);
                int m = Mathf.FloorToInt((hours - h) * 60f);
                timeModule.currentTime = new MeridiemTime(h, m);
            }
            catch (System.Exception e)
            {
                DebugLog($"Error setting Cozy time: {e.Message}");
            }
        }

        // Fire event
        OnTimeChanged?.Invoke(currentTimeOfDay);
        timeSinceLastEvent = 0f;
        lastFiredTimeOfDay = currentTimeOfDay;
        DebugLog($"Time manually set to: {GetFormattedTime()}");
    }

    /// <summary>
    /// Manually sets the current season and day
    /// </summary>
    [Button("Set Season")]
    public void SetSeason(SeasonType season, int dayOfSeason = 1)
    {
        dayOfSeason = Mathf.Clamp(dayOfSeason, 1, daysPerSeason);

        SeasonType previousSeason = currentSeason;
        currentSeason = season;
        currentDayOfSeason = dayOfSeason;

        // Update Cozy if connected
        if (isCozyConnected)
        {
            try
            {
                var timeModule = CozyWeather.instance.timeModule;
                int dayOfYear = CalculateDayOfYear(season, dayOfSeason);

                if (HasProperty(timeModule, "currentDay"))
                {
                    SetProperty(timeModule, "currentDay", dayOfYear);
                }
                else if (HasProperty(timeModule, "dayOfYear"))
                {
                    SetProperty(timeModule, "dayOfYear", dayOfYear);
                }
            }
            catch (System.Exception e)
            {
                DebugLog($"Error setting Cozy day: {e.Message}");
            }
        }

        if (previousSeason != currentSeason)
        {
            OnSeasonChanged?.Invoke(currentSeason);
        }

        OnDayChanged?.Invoke(currentDayOfSeason);
        DebugLog($"Season manually set to: {GetFormattedDate()}");
    }

    /// <summary>
    /// Sets complete game date and time
    /// </summary>
    [Button("Set Game Date")]
    public void SetGameDate(SeasonType season, int dayOfSeason, float timeOfDay)
    {
        SetSeason(season, dayOfSeason);
        SetTimeOfDay(timeOfDay);
        DebugLog($"Game date manually set to: {GetFormattedDateTime()}");
    }

    /// <summary>
    /// Toggles between Cozy-driven and manual time progression
    /// </summary>
    [Button("Toggle Cozy Time")]
    public void ToggleCozyTimeProgression()
    {
        useCozyTimeProgression = !useCozyTimeProgression;

        if (useCozyTimeProgression)
        {
            ConnectToCozy();
            if (isCozyConnected)
            {
                InitializeCozySettings();
            }
        }

        DebugLog($"Cozy time progression {(useCozyTimeProgression ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Forces reconnection and resync with Cozy
    /// </summary>
    [Button("Reconnect to Cozy")]
    public void ReconnectToCozy()
    {
        ConnectToCozy();
        if (isCozyConnected)
        {
            InitializeCozySettings();
        }
    }

    #endregion

    #region Getters & Information (Your Original API - Preserved)

    public float GetCurrentTimeOfDay() => currentTimeOfDay;
    public SeasonType GetCurrentSeason() => currentSeason;
    public int GetCurrentDayOfSeason() => currentDayOfSeason;
    public int GetTotalDaysElapsed() => totalDaysElapsed;
    public bool IsDaytime() => currentTimeOfDay >= 6f && currentTimeOfDay < 18f;
    public bool IsNighttime() => !IsDaytime();
    public float GetTimeProgressionRate() => timeProgressionRate;
    public bool IsCozyConnected() => isCozyConnected;
    public int GetDayOfYear() => CalculateDayOfYear(currentSeason, currentDayOfSeason);
    public int GetDaysPerYear() => daysPerYear;

    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(currentTimeOfDay);
        int minutes = Mathf.FloorToInt((currentTimeOfDay - hours) * 60f);
        return $"{hours:D2}:{minutes:D2}";
    }

    public string GetFormattedDate()
    {
        return $"Day {currentDayOfSeason} of {currentSeason}";
    }

    public string GetFormattedDateTime()
    {
        return $"{GetFormattedDate()} at {GetFormattedTime()}";
    }

    #endregion

    #region Utility Methods

    private int CalculateDayOfYear(SeasonType season, int dayOfSeason)
    {
        int seasonOffset = season switch
        {
            SeasonType.Spring => 0,
            SeasonType.Summer => daysPerSeason,
            SeasonType.Fall => daysPerSeason * 2,
            SeasonType.Winter => daysPerSeason * 3,
            _ => 0
        };

        return seasonOffset + dayOfSeason;
    }

    private void CalculateTimeProgressionRate()
    {
        timeProgressionRate = 24f / (dayDurationMinutes * 60f);
        minEventGameTimeInterval = eventFireIntervalSeconds * timeProgressionRate;
    }

    public void SetTotalDaysElapsed(int days)
    {
        totalDaysElapsed = Mathf.Max(0, days);
        DebugLog($"Total days elapsed set to: {totalDaysElapsed}");
    }

    public void TestEvents()
    {
        int subscriberCount = GetEventSubscriberCount();
        DebugLog($"OnTimeChanged has {subscriberCount} subscribers");

        if (subscriberCount > 0)
        {
            OnTimeChanged?.Invoke(currentTimeOfDay);
            timeSinceLastEvent = 0f;
            lastFiredTimeOfDay = currentTimeOfDay;
            DebugLog($"Test event fired with time: {currentTimeOfDay:F2}");
        }
    }

    public int GetEventSubscriberCount()
    {
        return OnTimeChanged?.GetInvocationList()?.Length ?? 0;
    }

    // Reflection helpers
    private bool HasProperty(object obj, string propertyName)
    {
        return obj.GetType().GetProperty(propertyName) != null ||
               obj.GetType().GetField(propertyName) != null;
    }

    private void SetProperty(object obj, string propertyName, object value)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property != null)
        {
            property.SetValue(obj, value);
            return;
        }

        var field = obj.GetType().GetField(propertyName);
        field?.SetValue(obj, value);
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
            CalculateTimeProgressionRate();
            daysPerYear = daysPerSeason * 4;
        }
    }
}

/// <summary>
/// Enum representing the four seasons.
/// </summary>
[Serializable]
public enum SeasonType
{
    Spring,
    Summer,
    Fall,
    Winter
}