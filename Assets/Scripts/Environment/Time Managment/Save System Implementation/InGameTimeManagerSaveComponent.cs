using UnityEngine;
using Sirenix.OdinInspector;


/// <summary>
/// Save component for the Cozy-driven time system. This component ONLY handles saving and loading
/// time data. All time logic, public API, and Cozy integration is handled by InGameTimeManager.
/// 
/// CLEAR SEPARATION OF CONCERNS:
/// - InGameTimeManager: All time logic, public API, Cozy integration, events
/// - InGameTimeManagerSaveComponent: Save/load operations ONLY
/// 
/// The save component acts as a pure data persistence layer that delegates to the TimeManager
/// for all actual time operations.
/// </summary>
public class InGameTimeManagerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private InGameTimeManager timeManager;
    [SerializeField] private bool autoFindManager = true;

    [Header("Save Settings")]
    [SerializeField] private bool saveTimeData = true;
    [SerializeField] private bool restoreTimeData = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "InGameTime_Main";
        autoGenerateID = false;
        base.Awake();

        if (autoFindManager)
        {
            FindTimeManager();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates the InGameTimeManager in the scene.
    /// </summary>
    private void FindTimeManager()
    {
        if (timeManager == null)
        {
            timeManager = InGameTimeManager.Instance;
        }

        if (timeManager == null)
        {
            timeManager = FindFirstObjectByType<InGameTimeManager>();
        }
    }

    /// <summary>
    /// Validates that the manager reference is available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (timeManager == null)
        {
            Debug.LogError($"[{name}] InGameTimeManager reference missing! Time data won't be saved.");
        }
        else
        {
            DebugLog("InGameTimeManager reference validated successfully");
        }
    }

    /// <summary>
    /// Gets time data from the TimeManager for saving. Pure data delegation.
    /// </summary>
    public override object GetDataToSave()
    {
        if (timeManager == null || !saveTimeData)
        {
            DebugLog("Cannot save - TimeManager reference missing or save disabled");
            return CreateDefaultTimeData();
        }

        // Delegate to TimeManager to get current time data
        var timeData = timeManager.GetTimeDataForSaving();
        DebugLog($"Retrieved time data for saving: {timeData.GetFormattedDateTime()}");
        return timeData;
    }

    /// <summary>
    /// Creates default time data when manager is unavailable or for new games
    /// </summary>
    private TimeData CreateDefaultTimeData()
    {
        var defaultData = new TimeData
        {
            timeOfDay = 6f,
            dayOfYear = 1,
            dayOfSeason = 1,
            season = SeasonType.Spring,
            daysPerSeason = 30,
            wasCozyDriven = false,
            saveTimestamp = System.DateTime.Now
        };

        DebugLog($"Created default time data: {defaultData.GetFormattedDateTime()}");
        return defaultData;
    }

    /// <summary>
    /// Extracts time data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting time data from container type: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer is TimeData timeData)
        {
            DebugLog($"Direct extraction - Time: {timeData.GetFormattedDateTime()}");
            return timeData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedData = persistentData.GetComponentData<TimeData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from persistent data - Time: {extractedData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("No time data found in persistent data");
            }
            return extractedData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedData = playerSaveData.GetCustomData<TimeData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from player save data - Time: {extractedData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("No time data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts time data from the unified save structure.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Cannot extract from unified save - unifiedData is null");
            return null;
        }

        DebugLog("Using modular extraction from unified save data");
        var extractedData = unifiedData.GetComponentData<TimeData>(SaveID);

        if (extractedData != null)
        {
            DebugLog($"Modular extraction successful - Time: {extractedData.GetFormattedDateTime()}");
        }
        else
        {
            DebugLog("No time data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default time data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default time data for new game");
        return CreateDefaultTimeData();
    }

    /// <summary>
    /// Stores time data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is TimeData timeData && unifiedData != null)
        {
            DebugLog($"Contributing time data to unified save: {timeData.GetFormattedDateTime()}");
            unifiedData.SetComponentData(SaveID, timeData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected TimeData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration. Delegates actual time setting to TimeManager.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING TIME DATA (Context: {context}) ===");

        if (!(data is TimeData timeData))
        {
            DebugLog($"Invalid save data type - expected TimeData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid data - Time: {timeData.GetFormattedDateTime()}");

        // Refresh manager reference in case it changed after scene load
        if (autoFindManager && timeManager == null)
        {
            FindTimeManager();
        }

        if (timeManager == null)
        {
            Debug.LogError("Cannot restore time data - InGameTimeManager not found!");
            return;
        }

        if (!restoreTimeData)
        {
            DebugLog("Time data restoration disabled - skipping");
            return;
        }

        // Validate data before applying
        if (!timeData.IsValid())
        {
            Debug.LogWarning("Time save data failed validation - applying anyway with corrections");
        }

        // Handle restoration based on context
        HandleTimeDataRestoration(timeData, context);

        DebugLog($"Time data restoration complete for context: {context}");
    }

    /// <summary>
    /// Handles time restoration based on context. Coordinates when to restore vs when to let time flow naturally.
    /// </summary>
    private void HandleTimeDataRestoration(TimeData timeData, RestoreContext context)
    {
        switch (context)
        {
            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - letting time continue naturally (no restoration needed)");
                // For doorway transitions, time should flow naturally with Cozy
                // We don't restore time data - this allows natural progression
                break;

            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring exact time and date");
                RestoreExactTimeData(timeData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - setting starting time and date");
                RestoreExactTimeData(timeData);
                break;

            default:
                DebugLog($"Unknown restore context: {context} - defaulting to exact restoration");
                RestoreExactTimeData(timeData);
                break;
        }
    }

    /// <summary>
    /// Restores exact time and date by delegating to the TimeManager
    /// </summary>
    private void RestoreExactTimeData(TimeData timeData)
    {
        // Wait a frame for systems to be ready, then delegate restoration to TimeManager
        StartCoroutine(DelayedTimeRestoration(timeData));
    }

    /// <summary>
    /// Delayed time restoration to ensure all systems are ready
    /// </summary>
    private System.Collections.IEnumerator DelayedTimeRestoration(TimeData timeData)
    {
        // Wait for end of frame to ensure all systems are initialized
        yield return new WaitForEndOfFrame();

        DebugLog("Delegating time restoration to TimeManager");

        // Delegate to the TimeManager - it handles all the actual time setting logic
        timeManager.RestoreTimeData(timeData);

        // Verify restoration was successful
        var currentData = timeManager.GetCurrentTimeData();
        DebugLog($"Time restoration complete - Manager now shows: {currentData.GetFormattedDateTime()}");
    }

    /// <summary>
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing time data for save operation");

        if (autoFindManager)
        {
            FindTimeManager();
        }

        ValidateReferences();
    }

    /// <summary>
    /// Called after load operations to refresh connected systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Time data load completed - refreshing connected systems");

        if (timeManager != null)
        {
            // Force a read from Cozy to ensure sync
            timeManager.ForceReadFromCozy();

            // Update any time debug UI components
            var timeDebugUI = FindFirstObjectByType<InGameTimeDebugUI>();
            if (timeDebugUI != null)
            {
                timeDebugUI.ForceUpdate();
                DebugLog("Refreshed time debug UI");
            }
        }
    }

    #region Manual Testing Controls

    /// <summary>
    /// Manual method to force complete restoration from saved data (for debugging)
    /// </summary>
    [Button("Force Restore Test")]
    public void ForceRestoreTest()
    {
        if (timeManager == null)
        {
            DebugLog("Cannot test - InGameTimeManager reference missing");
            return;
        }

        // Get current data and test restoration
        var currentData = GetDataToSave() as TimeData;
        if (currentData != null)
        {
            DebugLog("Testing restoration with current data");
            LoadSaveDataWithContext(currentData, RestoreContext.SaveFileLoad);
        }
        else
        {
            DebugLog("Failed to get current data for testing");
        }
    }

    /// <summary>
    /// Toggles time data saving/restoration
    /// </summary>
    [Button("Toggle Time Save/Restore")]
    public void ToggleTimeSaveRestore()
    {
        saveTimeData = !saveTimeData;
        restoreTimeData = saveTimeData; // Keep them in sync
        DebugLog($"Time save/restore {(saveTimeData ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Forces immediate save of current time state (for debugging)
    /// </summary>
    [Button("Force Save Current State")]
    public void ForceSaveCurrentState()
    {
        if (timeManager != null)
        {
            var saveData = GetDataToSave() as TimeData;
            if (saveData != null)
            {
                DebugLog($"Current time state: {saveData.GetDebugInfo()}");
            }
            else
            {
                DebugLog("Failed to get save data");
            }
        }
        else
        {
            DebugLog("Cannot save - InGameTimeManager not found");
        }
    }

    /// <summary>
    /// Gets detailed information about current save state
    /// </summary>
    [Button("Show Save Info")]
    public void ShowSaveInfo()
    {
        DebugLog("=== TIME SAVE COMPONENT INFO ===");
        DebugLog($"Save ID: {SaveID}");
        DebugLog($"Save Enabled: {saveTimeData}");
        DebugLog($"Restore Enabled: {restoreTimeData}");
        DebugLog($"Auto Find Manager: {autoFindManager}");
        DebugLog($"Manager Connected: {timeManager != null}");

        if (timeManager != null)
        {
            DebugLog($"Manager Instance: {timeManager.name}");
            DebugLog($"Cozy Connected: {timeManager.IsCozyConnected()}");

            var currentData = timeManager.GetCurrentTimeData();
            DebugLog($"Current Time State: {currentData.GetDebugInfo()}");
        }
        else
        {
            DebugLog("TimeManager reference is null!");
        }
    }

    /// <summary>
    /// Tests the complete save/load cycle
    /// </summary>
    [Button("Test Save/Load Cycle")]
    public void TestSaveLoadCycle()
    {
        if (timeManager == null)
        {
            DebugLog("Cannot test - InGameTimeManager not available");
            return;
        }

        StartCoroutine(TestSaveLoadCycleCoroutine());
    }

    /// <summary>
    /// Coroutine that tests the complete save/load cycle
    /// </summary>
    private System.Collections.IEnumerator TestSaveLoadCycleCoroutine()
    {
        DebugLog("=== STARTING SAVE/LOAD CYCLE TEST ===");

        // Get initial state
        var initialData = timeManager.GetCurrentTimeData();
        DebugLog($"Initial state: {initialData.GetFormattedDateTime()}");

        // Get save data
        var saveData = GetDataToSave() as TimeData;
        if (saveData == null)
        {
            DebugLog("FAILED: Could not get save data");
            yield break;
        }
        DebugLog($"Save data captured: {saveData.GetFormattedDateTime()}");

        // Change time to something different
        var originalTime = timeManager.GetCurrentTimeOfDay();
        var testTime = originalTime + 6f; // Add 6 hours
        if (testTime >= 24f) testTime -= 24f; // Wrap around

        timeManager.SetTimeOfDay(testTime);
        yield return new WaitForSecondsRealtime(0.5f);

        var changedData = timeManager.GetCurrentTimeData();
        DebugLog($"Changed state: {changedData.GetFormattedDateTime()}");

        // Test restoration
        DebugLog("Testing restoration...");
        LoadSaveDataWithContext(saveData, RestoreContext.SaveFileLoad);

        // Wait for restoration to complete
        yield return new WaitForSecondsRealtime(1f);

        // Check final state
        var finalData = timeManager.GetCurrentTimeData();
        DebugLog($"Final state: {finalData.GetFormattedDateTime()}");

        // Verify restoration worked
        bool restoreSuccessful = Mathf.Abs(finalData.timeOfDay - saveData.timeOfDay) < 0.1f &&
                               finalData.dayOfSeason == saveData.dayOfSeason &&
                               finalData.season == saveData.season;

        DebugLog($"=== SAVE/LOAD CYCLE TEST {(restoreSuccessful ? "PASSED" : "FAILED")} ===");

        if (!restoreSuccessful)
        {
            DebugLog($"Expected: {saveData.GetFormattedDateTime()}");
            DebugLog($"Actual: {finalData.GetFormattedDateTime()}");
        }
    }

    #endregion

    private void OnDestroy()
    {
        // Unregister from persistence manager
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.UnregisterComponent(this);
        }
    }
}

/// <summary>
/// TimeData - Save data structure for time system with proper validation and helper methods.
/// This is the data structure that gets saved/loaded by the save system.
/// </summary>
[System.Serializable]
public class TimeData
{
    [Header("Time & Date")]
    public float timeOfDay = 6f; // 0-24 hours
    public int dayOfYear = 1; // 1-based day of year
    public int dayOfSeason = 1; // 1-based day of season
    public SeasonType season = SeasonType.Spring;

    [Header("Configuration")]
    public int daysPerSeason = 30; // Days in each season
    public bool wasCozyDriven = false; // Whether this data came from Cozy
    public System.DateTime saveTimestamp; // When this was saved

    public TimeData()
    {
        timeOfDay = 6f;
        dayOfYear = 1;
        dayOfSeason = 1;
        season = SeasonType.Spring;
        daysPerSeason = 30;
        wasCozyDriven = false;
        saveTimestamp = System.DateTime.Now;
    }

    /// <summary>
    /// Copy constructor for creating independent copies
    /// </summary>
    public TimeData(TimeData other)
    {
        if (other == null) return;

        timeOfDay = other.timeOfDay;
        dayOfYear = other.dayOfYear;
        dayOfSeason = other.dayOfSeason;
        season = other.season;
        daysPerSeason = other.daysPerSeason;
        wasCozyDriven = other.wasCozyDriven;
        saveTimestamp = other.saveTimestamp;
    }

    #region Validation and Debugging

    /// <summary>
    /// Validates the integrity of the time data
    /// </summary>
    public bool IsValid()
    {
        // Check time bounds
        if (timeOfDay < 0f || timeOfDay >= 24f)
            return false;

        // Check day bounds
        if (dayOfSeason < 1 || dayOfSeason > daysPerSeason)
            return false;

        // Check day of year
        if (dayOfYear < 1)
            return false;

        // Check days per season
        if (daysPerSeason <= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Gets detailed debug information
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Time Data Debug Info ===");
        info.AppendLine($"Time: {GetFormattedTime()} ({timeOfDay:F2})");
        info.AppendLine($"Date: Day {dayOfSeason} of {season}");
        info.AppendLine($"Day of Year: {dayOfYear}");
        info.AppendLine($"Days Per Season: {daysPerSeason}");
        info.AppendLine($"Was Cozy Driven: {wasCozyDriven}");
        info.AppendLine($"Save Timestamp: {saveTimestamp:yyyy-MM-dd HH:mm:ss}");
        info.AppendLine($"Data Valid: {IsValid()}");

        return info.ToString();
    }

    /// <summary>
    /// Gets a formatted time string (HH:MM format)
    /// </summary>
    public string GetFormattedTime()
    {
        int hours = Mathf.FloorToInt(timeOfDay);
        int minutes = Mathf.FloorToInt((timeOfDay - hours) * 60f);
        return $"{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// Gets a formatted date string
    /// </summary>
    public string GetFormattedDate()
    {
        return $"Day {dayOfSeason} of {season}";
    }

    /// <summary>
    /// Gets a formatted date and time string
    /// </summary>
    public string GetFormattedDateTime()
    {
        return $"{GetFormattedDate()} at {GetFormattedTime()}";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if it's currently daytime (6 AM to 6 PM)
    /// </summary>
    public bool IsDaytime() => timeOfDay >= 6f && timeOfDay < 18f;

    /// <summary>
    /// Checks if it's currently nighttime
    /// </summary>
    public bool IsNighttime() => !IsDaytime();

    /// <summary>
    /// Gets the time of day as a normalized value (0-1, where 0.5 is noon)
    /// </summary>
    public float GetNormalizedTimeOfDay()
    {
        return timeOfDay / 24f;
    }

    /// <summary>
    /// Gets the day progress within the current season (0-1)
    /// </summary>
    public float GetSeasonProgress()
    {
        return Mathf.Clamp01((dayOfSeason - 1) / (float)(daysPerSeason - 1));
    }

    /// <summary>
    /// Gets the progress through the current year (0-1)
    /// </summary>
    public float GetYearProgress()
    {
        int totalDaysInYear = daysPerSeason * 4;
        return Mathf.Clamp01((dayOfYear - 1) / (float)(totalDaysInYear - 1));
    }

    /// <summary>
    /// Updates the save timestamp to current time
    /// </summary>
    public void UpdateTimestamp()
    {
        saveTimestamp = System.DateTime.Now;
    }

    #endregion
}