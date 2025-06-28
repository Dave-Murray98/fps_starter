using UnityEngine;
using System;
using Sirenix.OdinInspector;

/// <summary>
/// Manages the day/night cycle and game time progression. Persists across scenes.
/// Save/load functionality is handled by InGameTimeManagerSaveComponent.
/// Updated with pause-aware event firing system for consistent updates.
/// TEMPERATURE LOGIC REMOVED - Now handled by WeatherManager.
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

    [Header("Event Timing")]
    [SerializeField] private float eventFireIntervalSeconds = 0.5f; // Real-world seconds between events (for calculation)
    [SerializeField] private bool forceEventOnSignificantChange = true; // Fire event if time jumps significantly

    [Header("Debug Settings")]
    public bool showDebugLogs = true;
    [SerializeField] private bool enableTimeProgression = true;

    // Current time state
    [ShowInInspector, ReadOnly] private float currentTimeOfDay = 6f; // 0-24 hours
    [ShowInInspector, ReadOnly] private SeasonType currentSeason = SeasonType.Spring;
    [ShowInInspector, ReadOnly] private int currentDayOfSeason = 1;
    [ShowInInspector, ReadOnly] private int totalDaysElapsed = 0;

    // Calculated values
    [ShowInInspector, ReadOnly] private float timeProgressionRate; // Time units per real second

    // Event timing tracking
    [ShowInInspector, ReadOnly] private float minEventGameTimeInterval = 0.0167f; // Calculated minimum game time between events
    [ShowInInspector, ReadOnly] private float timeSinceLastEvent = 0f; // Accumulated game time since last event
    private float lastFiredTimeOfDay = -1f;

    // Events for external systems
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
        if (enableTimeProgression && Time.timeScale > 0f)
        {
            ProgressTime();
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        SetTimeOfDay(startTimeOfDay);
        SetGameDate(startingSeason, startingDayOfSeason, currentTimeOfDay);
        DebugLog($"Initialized - {GetFormattedDate()} at {GetFormattedTime()}");
    }

    public void RefreshReferences()
    {
        // No scene-dependent references to refresh
        DebugLog("References refreshed");
    }

    public void Cleanup()
    {
        DebugLog("Cleanup completed");
    }

    #endregion

    #region Time Progression

    /// <summary>
    /// Updates the current time based on real-time progression and triggers events.
    /// Uses accumulated time tracking to fire events at consistent intervals while respecting pause state.
    /// </summary>
    private void ProgressTime()
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

        // Fire time events when we've accumulated enough game time
        if (timeSinceLastEvent >= minEventGameTimeInterval)
        {
            OnTimeChanged?.Invoke(currentTimeOfDay);
            DebugLog("OnTimeChanged event fired");
            timeSinceLastEvent = 0f;
            lastFiredTimeOfDay = currentTimeOfDay;
            DebugLog($"Time event fired: {GetFormattedDateTime()} - Sent to {GetEventSubscriberCount()} listeners");
        }

        // Check for significant time jumps (manual setting, loading, etc.)
        if (forceEventOnSignificantChange && lastFiredTimeOfDay >= 0f)
        {
            float timeDifference = Mathf.Abs(currentTimeOfDay - lastFiredTimeOfDay);

            // Handle day boundary crossing
            if (timeDifference > 12f)
            {
                timeDifference = 24f - timeDifference;
            }

            // If time changed significantly beyond normal progression
            if (timeDifference > minEventGameTimeInterval * 2f)
            {
                OnTimeChanged?.Invoke(currentTimeOfDay);
                timeSinceLastEvent = 0f;
                lastFiredTimeOfDay = currentTimeOfDay;
                DebugLog($"Time event fired (significant change): {GetFormattedDateTime()}");
            }
        }

        // Fire day/season events when they actually change
        if (currentDayOfSeason != previousDay)
        {
            OnDayChanged?.Invoke(currentDayOfSeason);
            DebugLog($"Day changed event fired: {GetFormattedDate()}");
        }

        if (currentSeason != previousSeason)
        {
            OnSeasonChanged?.Invoke(currentSeason);
            DebugLog($"Season changed event fired: {currentSeason}");
        }
    }

    /// <summary>
    /// Advances to the next day and handles season transitions.
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
    /// Advances to the next season in the cycle.
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

    /// <summary>
    /// Calculates the time progression rate and event intervals based on configured day duration.
    /// </summary>
    private void CalculateTimeProgressionRate()
    {
        timeProgressionRate = 24f / (dayDurationMinutes * 60f); // 24 hours per configured minutes

        // Calculate how much game time should pass between events to achieve desired real-time interval
        // Formula: (desired_real_seconds * time_progression_rate) = game_hours_between_events
        minEventGameTimeInterval = eventFireIntervalSeconds * timeProgressionRate;

        DebugLog($"Time progression rate: {timeProgressionRate:F4} game hours per real second");
        DebugLog($"Event fire interval: every {minEventGameTimeInterval:F4} game hours (≈{eventFireIntervalSeconds}s real time)");
    }

    #endregion

    #region Manual Control Methods

    /// <summary>
    /// Manually sets the time of day and immediately fires events.
    /// </summary>
    [Button("Set Time of Day")]
    public void SetTimeOfDay(float hours)
    {
        hours = Mathf.Clamp(hours, 0f, 23.99f);
        currentTimeOfDay = hours;

        // Force immediate event fire and reset accumulators
        OnTimeChanged?.Invoke(currentTimeOfDay);
        timeSinceLastEvent = 0f;
        lastFiredTimeOfDay = currentTimeOfDay;
        DebugLog($"Time manually set to: {GetFormattedTime()} - Event fired to {GetEventSubscriberCount()} listeners");
    }

    /// <summary>
    /// Manually sets the current season and optionally the day within that season.
    /// </summary>
    [Button("Set Season")]
    public void SetSeason(SeasonType season, int dayOfSeason = 1)
    {
        dayOfSeason = Mathf.Clamp(dayOfSeason, 1, daysPerSeason);

        SeasonType previousSeason = currentSeason;
        currentSeason = season;
        currentDayOfSeason = dayOfSeason;

        if (previousSeason != currentSeason)
        {
            OnSeasonChanged?.Invoke(currentSeason);
        }

        OnDayChanged?.Invoke(currentDayOfSeason);
        DebugLog($"Season manually set to: {GetFormattedDate()}");
    }

    /// <summary>
    /// Manually sets the complete game date and time.
    /// </summary>
    [Button("Set Game Date")]
    public void SetGameDate(SeasonType season, int dayOfSeason, float timeOfDay)
    {
        SetSeason(season, dayOfSeason);
        SetTimeOfDay(timeOfDay); // This will fire the event
        DebugLog($"Game date manually set to: {GetFormattedDateTime()}");
    }

    /// <summary>
    /// Sets the total days elapsed. Used by save system.
    /// </summary>
    public void SetTotalDaysElapsed(int days)
    {
        totalDaysElapsed = Mathf.Max(0, days);
        DebugLog($"Total days elapsed set to: {totalDaysElapsed}");
    }

    /// <summary>
    /// Advances time by the specified number of hours and fires events if needed.
    /// </summary>
    public void AdvanceTime(float hours)
    {
        float newTime = currentTimeOfDay + hours;

        // Handle day rollovers
        while (newTime >= 24f)
        {
            newTime -= 24f;
            AdvanceDay();
        }

        currentTimeOfDay = newTime;

        // Add to accumulator and check if we should fire event
        timeSinceLastEvent += hours;

        if (timeSinceLastEvent >= minEventGameTimeInterval)
        {
            OnTimeChanged?.Invoke(currentTimeOfDay);
            timeSinceLastEvent = 0f;
            lastFiredTimeOfDay = currentTimeOfDay;
            DebugLog($"Time advanced by {hours:F1} hours to: {GetFormattedDateTime()} - Event fired");
        }
    }

    /// <summary>
    /// Toggles time progression on/off.
    /// </summary>
    [Button("Toggle Time Progression")]
    public void ToggleTimeProgression()
    {
        enableTimeProgression = !enableTimeProgression;
        DebugLog($"Time progression {(enableTimeProgression ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Tests the event system by firing a time change event.
    /// </summary>
    [Button("Test Events")]
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
        else
        {
            DebugLog("No subscribers found - Systems may not be connected");
        }
    }

    /// <summary>
    /// Sets the event fire interval and recalculates timing.
    /// </summary>
    public void SetEventFireInterval(float seconds)
    {
        eventFireIntervalSeconds = Mathf.Max(0.1f, seconds);
        CalculateTimeProgressionRate();
        DebugLog($"Event fire interval set to {eventFireIntervalSeconds:F1} seconds");
    }

    /// <summary>
    /// Resets event timing accumulators. Useful when loading saves or changing scenes.
    /// </summary>
    public void ResetEventTiming()
    {
        timeSinceLastEvent = 0f;
        lastFiredTimeOfDay = currentTimeOfDay;
        DebugLog("Event timing reset");
    }

    #endregion

    #region Getters & Information

    /// <summary>
    /// Gets the current time of day (0-24 hours).
    /// </summary>
    public float GetCurrentTimeOfDay() => currentTimeOfDay;

    /// <summary>
    /// Gets the current season.
    /// </summary>
    public SeasonType GetCurrentSeason() => currentSeason;

    /// <summary>
    /// Gets the current day within the season.
    /// </summary>
    public int GetCurrentDayOfSeason() => currentDayOfSeason;

    /// <summary>
    /// Gets the total days elapsed since game start.
    /// </summary>
    public int GetTotalDaysElapsed() => totalDaysElapsed;

    /// <summary>
    /// Checks if it's currently daytime (6 AM to 6 PM).
    /// </summary>
    public bool IsDaytime() => currentTimeOfDay >= 6f && currentTimeOfDay < 18f;

    /// <summary>
    /// Checks if it's currently nighttime.
    /// </summary>
    public bool IsNighttime() => !IsDaytime();

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

    /// <summary>
    /// Gets the time progression rate (game hours per real second).
    /// Used by other systems like WeatherManager for time calculations.
    /// </summary>
    public float GetTimeProgressionRate() => timeProgressionRate;

    #endregion

    #region Configuration

    /// <summary>
    /// Updates the day duration and recalculates progression rate.
    /// </summary>
    public void SetDayDuration(float minutes)
    {
        dayDurationMinutes = Mathf.Max(0.1f, minutes);
        CalculateTimeProgressionRate();
        DebugLog($"Day duration set to {dayDurationMinutes:F1} minutes");
    }

    /// <summary>
    /// Updates the number of days per season.
    /// </summary>
    public void SetDaysPerSeason(int days)
    {
        daysPerSeason = Mathf.Max(1, days);

        // Clamp current day if it exceeds new limit
        if (currentDayOfSeason > daysPerSeason)
        {
            currentDayOfSeason = daysPerSeason;
            OnDayChanged?.Invoke(currentDayOfSeason);
        }

        DebugLog($"Days per season set to {daysPerSeason}");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[InGameTimeManager] {message}");
        }
    }

    /// <summary>
    /// Gets the number of subscribers to the OnTimeChanged event for debugging.
    /// </summary>
    public int GetEventSubscriberCount()
    {
        return OnTimeChanged?.GetInvocationList()?.Length ?? 0;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            CalculateTimeProgressionRate();
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