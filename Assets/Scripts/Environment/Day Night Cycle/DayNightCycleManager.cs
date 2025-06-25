using UnityEngine;
using System;
using Sirenix.OdinInspector;

/// <summary>
/// Manages the day/night cycle and game time progression. Persists across scenes and integrates
/// with the save system. Handles time-based temperature modulation and provides manual override
/// capabilities for setting specific times/dates.
/// </summary>
public class DayNightCycleManager : MonoBehaviour, IManager, IPlayerDependentSaveable
{
    public static DayNightCycleManager Instance { get; private set; }

    [Header("Time Configuration")]
    [SerializeField] private float dayDurationMinutes = 20f; // Real-time minutes per game day
    [SerializeField] private float startTimeOfDay = 6f; // Starting hour (0-24)

    [Header("Season & Date Configuration")]
    [SerializeField] private int daysPerSeason = 30;
    [SerializeField] private SeasonType startingSeason = SeasonType.Spring;
    [SerializeField] private int startingDayOfSeason = 1;

    [Header("Temperature Modulation")]
    [SerializeField] private float dayNightTemperatureVariance = 10f; // °C difference between day/night
    [SerializeField] private AnimationCurve temperatureCurve = AnimationCurve.EaseInOut(0f, -1f, 1f, 1f);

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool enableTimeProgression = true;

    // Current time state
    [ShowInInspector, ReadOnly] private float currentTimeOfDay = 6f; // 0-24 hours
    [ShowInInspector, ReadOnly] private SeasonType currentSeason = SeasonType.Spring;
    [ShowInInspector, ReadOnly] private int currentDayOfSeason = 1;
    [ShowInInspector, ReadOnly] private int totalDaysElapsed = 0;

    // Calculated values
    [ShowInInspector, ReadOnly] private float timeProgressionRate; // Time units per real second
    [ShowInInspector, ReadOnly] private float currentTemperatureModifier = 0f;

    // Events for external systems
    public static event Action<float> OnTimeChanged; // Current time (0-24)
    public static event Action<int> OnDayChanged; // Day of season
    public static event Action<SeasonType> OnSeasonChanged; // New season
    public static event Action<float> OnTemperatureModifierChanged; // Temperature modifier from time

    // Save system integration
    public string SaveID => "DayNightCycle";
    public SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CalculateTimeProgressionRate();
            DebugLog("DayNightCycleManager initialized");
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
    /// </summary>
    private void ProgressTime()
    {
        float previousTime = currentTimeOfDay;
        int previousDay = currentDayOfSeason;
        SeasonType previousSeason = currentSeason;

        // Progress time
        currentTimeOfDay += timeProgressionRate * Time.deltaTime;

        // Handle day rollover
        if (currentTimeOfDay >= 24f)
        {
            currentTimeOfDay -= 24f;
            AdvanceDay();
        }

        // Update temperature modifier based on time of day
        UpdateTemperatureModifier();

        // Fire time events at reasonable intervals (every ~6 seconds of game time or 0.1 hours)
        if (Mathf.Abs(currentTimeOfDay - previousTime) >= 0.1f)
        {
            OnTimeChanged?.Invoke(currentTimeOfDay);
        }

        // Fire day/season events when they actually change
        if (currentDayOfSeason != previousDay)
        {
            OnDayChanged?.Invoke(currentDayOfSeason);
        }

        if (currentSeason != previousSeason)
        {
            OnSeasonChanged?.Invoke(currentSeason);
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
    /// Calculates the time progression rate based on configured day duration.
    /// </summary>
    private void CalculateTimeProgressionRate()
    {
        timeProgressionRate = 24f / (dayDurationMinutes * 60f); // 24 hours per configured minutes
        DebugLog($"Time progression rate calculated: {timeProgressionRate:F4} game hours per real second");
    }

    #endregion

    #region Temperature Modulation

    /// <summary>
    /// Updates the temperature modifier based on current time of day using the configured curve.
    /// </summary>
    private void UpdateTemperatureModifier()
    {
        // Convert time to 0-1 range (noon = 1, midnight = 0)
        float normalizedTime = Mathf.Sin((currentTimeOfDay - 6f) / 24f * 2f * Mathf.PI) * 0.5f + 0.5f;

        // Apply temperature curve
        float curveValue = temperatureCurve.Evaluate(normalizedTime);
        float newModifier = curveValue * dayNightTemperatureVariance;

        if (Mathf.Abs(newModifier - currentTemperatureModifier) > 0.1f)
        {
            currentTemperatureModifier = newModifier;
            OnTemperatureModifierChanged?.Invoke(currentTemperatureModifier);
        }
    }

    /// <summary>
    /// Gets the current temperature modifier from time of day in Celsius.
    /// </summary>
    public float GetTemperatureModifier()
    {
        return currentTemperatureModifier;
    }

    #endregion

    #region Manual Control Methods

    /// <summary>
    /// Manually sets the time of day (0-24 hours).
    /// </summary>
    [Button("Set Time of Day")]
    public void SetTimeOfDay(float hours)
    {
        hours = Mathf.Clamp(hours, 0f, 23.99f);
        currentTimeOfDay = hours;
        UpdateTemperatureModifier();

        // Fire event and log it
        OnTimeChanged?.Invoke(currentTimeOfDay);
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
        SetTimeOfDay(timeOfDay);
        DebugLog($"Game date manually set to: {GetFormattedDateTime()}");
    }

    /// <summary>
    /// Advances time by the specified number of hours.
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
        UpdateTemperatureModifier();
        OnTimeChanged?.Invoke(currentTimeOfDay);
        DebugLog($"Time advanced by {hours:F1} hours to: {GetFormattedDateTime()}");
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
            DebugLog($"Test event fired with time: {currentTimeOfDay:F2}");
        }
        else
        {
            DebugLog("No subscribers found - SunMoonLightController may not be connected");
        }
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

    /// <summary>
    /// Updates the day/night temperature variance.
    /// </summary>
    public void SetTemperatureVariance(float variance)
    {
        dayNightTemperatureVariance = Mathf.Max(0f, variance);
        UpdateTemperatureModifier();
        DebugLog($"Temperature variance set to {dayNightTemperatureVariance:F1}°C");
    }

    #endregion

    #region Save System Implementation

    public object GetDataToSave()
    {
        var saveData = new EnvironmentSaveData
        {
            currentTimeOfDay = currentTimeOfDay,
            currentSeason = currentSeason,
            currentDayOfSeason = currentDayOfSeason,
            totalDaysElapsed = totalDaysElapsed,
            dayDurationMinutes = dayDurationMinutes,
            currentTemperatureModifier = currentTemperatureModifier
        };

        DebugLog($"Saving environment data: {GetFormattedDateTime()}");
        return saveData;
    }

    public object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is EnvironmentSaveData envData)
        {
            return envData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            return persistentData.GetComponentData<EnvironmentSaveData>(SaveID);
        }

        return null;
    }

    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        return unifiedData.GetComponentData<EnvironmentSaveData>(SaveID);
    }

    public object CreateDefaultData()
    {
        return new EnvironmentSaveData
        {
            currentTimeOfDay = startTimeOfDay,
            currentSeason = startingSeason,
            currentDayOfSeason = startingDayOfSeason,
            totalDaysElapsed = 0,
            dayDurationMinutes = dayDurationMinutes,
            currentTemperatureModifier = 0f
        };
    }

    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is EnvironmentSaveData envData)
        {
            unifiedData.SetComponentData(SaveID, envData);
        }
    }

    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is EnvironmentSaveData envData))
        {
            DebugLog($"Invalid save data type for day/night cycle");
            return;
        }

        DebugLog($"Loading environment data for context: {context}");

        currentTimeOfDay = envData.currentTimeOfDay;
        currentSeason = envData.currentSeason;
        currentDayOfSeason = envData.currentDayOfSeason;
        totalDaysElapsed = envData.totalDaysElapsed;
        dayDurationMinutes = envData.dayDurationMinutes;
        currentTemperatureModifier = envData.currentTemperatureModifier;

        CalculateTimeProgressionRate();
        UpdateTemperatureModifier();

        // Fire events to update connected systems
        OnTimeChanged?.Invoke(currentTimeOfDay);
        OnDayChanged?.Invoke(currentDayOfSeason);
        OnSeasonChanged?.Invoke(currentSeason);
        OnTemperatureModifierChanged?.Invoke(currentTemperatureModifier);

        DebugLog($"Environment data loaded: {GetFormattedDateTime()}");
    }

    public void OnBeforeSave()
    {
        DebugLog("Preparing day/night cycle data for save");
    }

    public void OnAfterLoad()
    {
        DebugLog("Day/night cycle data loaded successfully");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[DayNightCycle] {message}");
        }
    }

    /// <summary>
    /// Gets the number of subscribers to the OnTimeChanged event for debugging.
    /// </summary>
    private int GetEventSubscriberCount()
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
[System.Serializable]
public enum SeasonType
{
    Spring,
    Summer,
    Fall,
    Winter
}