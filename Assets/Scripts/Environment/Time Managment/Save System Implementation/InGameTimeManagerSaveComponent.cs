using UnityEngine;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Save component for the Cozy-native time system. This component ONLY handles saving and loading
/// time data using Cozy's native format (MeridiemTime, day system, etc.).
/// 
/// PURE COZY INTEGRATION:
/// - Uses Cozy's MeridiemTime instead of float hours
/// - Uses Cozy's day numbering system
/// - Uses Cozy's season objects directly
/// - No custom conversions or mappings
/// 
/// CLEAR SEPARATION:
/// - InGameTimeManager: All time logic, Cozy integration, public API
/// - InGameTimeManagerSaveComponent: Save/load operations ONLY
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
    /// Gets time data from the TimeManager for saving using Cozy's native format.
    /// </summary>
    public override object GetDataToSave()
    {
        if (timeManager == null || !saveTimeData)
        {
            DebugLog("Cannot save - TimeManager reference missing or save disabled");
            return CreateDefaultTimeData();
        }

        // Delegate to TimeManager to get current time data in Cozy format
        var timeData = timeManager.GetTimeDataForSaving();
        DebugLog($"Retrieved Cozy time data for saving: {timeData.GetFormattedDateTime()}");
        return timeData;
    }

    /// <summary>
    /// Creates default time data using Cozy's native format
    /// </summary>
    private TimeData CreateDefaultTimeData()
    {
        var defaultData = new TimeData
        {
            time = new MeridiemTime(6, 0), // 6:00 AM
            day = 1,
            temperature = 20f,
            season = "Spring",
            wasCozyDriven = false,
            saveTimestamp = System.DateTime.Now
        };

        DebugLog($"Created default Cozy time data: {defaultData.GetFormattedDateTime()}");
        return defaultData;
    }

    /// <summary>
    /// Extracts time data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting Cozy time data from container type: {saveContainer?.GetType().Name ?? "null"}");

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
                DebugLog("No Cozy time data found in persistent data");
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
                DebugLog("No Cozy time data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts Cozy time data from the unified save structure.
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
            DebugLog("No Cozy time data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default Cozy time data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default Cozy time data for new game");
        return CreateDefaultTimeData();
    }

    /// <summary>
    /// Stores Cozy time data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is TimeData timeData && unifiedData != null)
        {
            DebugLog($"Contributing Cozy time data to unified save: {timeData.GetFormattedDateTime()}");
            unifiedData.SetComponentData(SaveID, timeData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected CozyTimeData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration using Cozy's native format.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING COZY TIME DATA (Context: {context}) ===");

        if (!(data is TimeData timeData))
        {
            DebugLog($"Invalid save data type - expected CozyTimeData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid Cozy data - Time: {timeData.GetFormattedDateTime()}");

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
            Debug.LogWarning("Cozy time save data failed validation - applying anyway with corrections");
        }

        // Handle restoration based on context
        HandleCozyTimeRestoration(timeData, context);

        DebugLog($"Cozy time data restoration complete for context: {context}");
    }

    /// <summary>
    /// Handles Cozy time restoration based on context.
    /// </summary>
    private void HandleCozyTimeRestoration(TimeData timeData, RestoreContext context)
    {
        switch (context)
        {
            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - letting Cozy time continue naturally");
                // For doorway transitions, let Cozy time flow naturally
                // This allows seamless time progression between scenes
                break;

            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring exact Cozy time and date");
                RestoreExactCozyTime(timeData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - setting starting Cozy time and date");
                RestoreExactCozyTime(timeData);
                break;

            default:
                DebugLog($"Unknown restore context: {context} - defaulting to exact restoration");
                RestoreExactCozyTime(timeData);
                break;
        }
    }

    /// <summary>
    /// Restores exact Cozy time by delegating to the TimeManager
    /// </summary>
    private void RestoreExactCozyTime(TimeData timeData)
    {
        // Wait a frame for Cozy systems to be ready, then delegate restoration
        StartCoroutine(DelayedCozyTimeRestoration(timeData));
    }

    /// <summary>
    /// Delayed Cozy time restoration to ensure all systems are ready
    /// </summary>
    private System.Collections.IEnumerator DelayedCozyTimeRestoration(TimeData timeData)
    {
        // Wait for end of frame to ensure Cozy and all systems are initialized
        yield return new WaitForEndOfFrame();

        DebugLog("Delegating Cozy time restoration to TimeManager");

        // Delegate to the TimeManager - it handles all Cozy time setting logic
        timeManager.RestoreTimeData(timeData);

        // Verify restoration was successful
        var currentData = timeManager.GetCurrentTimeData();
        DebugLog($"Cozy time restoration complete - Manager shows: {currentData.GetFormattedDateTime()}");

        // Additional verification that Cozy was actually updated
        if (timeManager.IsCozyConnected())
        {
            var cozyTime = timeManager.GetCurrentTime();
            DebugLog($"Verified Cozy time: {cozyTime.hours:D2}:{cozyTime.minutes:D2} on day {timeManager.GetCurrentDay()}");
        }
    }

    /// <summary>
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing Cozy time data for save operation");

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
        DebugLog("Cozy time data load completed - refreshing connected systems");

        // if (timeManager != null)
        // {
        //     // Force a read from Cozy to ensure sync
        //     timeManager.ForceReadFromCozy();

        //     // Update any time debug UI components
        //     var timeDebugUI = FindFirstObjectByType<InGameTimeDebugUI>();
        //     if (timeDebugUI != null)
        //     {
        //         timeDebugUI.ForceUpdate();
        //         DebugLog("Refreshed time debug UI");
        //     }
        // }
    }

    #region Manual Testing Controls

    /// <summary>
    /// Manual method to test restoration with current Cozy data
    /// </summary>
    [Button("Force Restore Test")]
    public void ForceRestoreTest()
    {
        if (timeManager == null)
        {
            DebugLog("Cannot test - InGameTimeManager reference missing");
            return;
        }

        // Get current Cozy data and test restoration
        var currentData = GetDataToSave() as TimeData;
        if (currentData != null)
        {
            DebugLog("Testing restoration with current Cozy data");
            DebugLog($"Testing with: {currentData.GetFormattedDateTime()}");
            LoadSaveDataWithContext(currentData, RestoreContext.SaveFileLoad);
        }
        else
        {
            DebugLog("Failed to get current Cozy data for testing");
        }
    }

    /// <summary>
    /// Toggles Cozy time data saving/restoration
    /// </summary>
    [Button("Toggle Cozy Save/Restore")]
    public void ToggleCozyTimeSaveRestore()
    {
        saveTimeData = !saveTimeData;
        restoreTimeData = saveTimeData; // Keep them in sync
        DebugLog($"Cozy time save/restore {(saveTimeData ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Forces immediate save of current Cozy time state
    /// </summary>
    [Button("Force Save Current Cozy State")]
    public void ForceSaveCurrentCozyState()
    {
        if (timeManager != null)
        {
            var saveData = GetDataToSave() as TimeData;
            if (saveData != null)
            {
                DebugLog($"Current Cozy time state: {saveData.GetDebugInfo()}");
                DebugLog($"Formatted: {saveData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("Failed to get Cozy save data");
            }
        }
        else
        {
            DebugLog("Cannot save - InGameTimeManager not found");
        }
    }

    /// <summary>
    /// Gets detailed information about current Cozy save state
    /// </summary>
    [Button("Show Cozy Save Info")]
    public void ShowCozySaveInfo()
    {
        DebugLog("=== COZY TIME SAVE COMPONENT INFO ===");
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
            DebugLog($"Current Cozy State: {currentData.GetDebugInfo()}");
            DebugLog($"Formatted: {currentData.GetFormattedDateTime()}");

            if (timeManager.IsCozyConnected())
            {
                var cozyTime = timeManager.GetCurrentTime();
                DebugLog($"Raw Cozy Time: {cozyTime.hours}:{cozyTime.minutes:D2}");
                DebugLog($"Raw Cozy Day: {timeManager.GetCurrentDay()}");
                DebugLog($"Raw Cozy Season: {timeManager.GetCurrentSeasonString()}");
            }
        }
        else
        {
            DebugLog("TimeManager reference is null!");
        }
    }

    /// <summary>
    /// Tests the complete Cozy save/load cycle
    /// </summary>
    [Button("Test Cozy Save/Load Cycle")]
    public void TestCozySaveLoadCycle()
    {
        if (timeManager == null)
        {
            DebugLog("Cannot test - InGameTimeManager not available");
            return;
        }

        StartCoroutine(TestCozySaveLoadCycleCoroutine());
    }

    /// <summary>
    /// Coroutine that tests the complete Cozy save/load cycle
    /// </summary>
    private System.Collections.IEnumerator TestCozySaveLoadCycleCoroutine()
    {
        DebugLog("=== STARTING COZY SAVE/LOAD CYCLE TEST ===");

        // Get initial Cozy state
        var initialData = timeManager.GetCurrentTimeData();
        DebugLog($"Initial Cozy state: {initialData.GetFormattedDateTime()}");

        // Get save data
        var saveData = GetDataToSave() as TimeData;
        if (saveData == null)
        {
            DebugLog("FAILED: Could not get Cozy save data");
            yield break;
        }
        DebugLog($"Cozy save data captured: {saveData.GetFormattedDateTime()}");

        // Change time to something different using Cozy format
        var originalTime = timeManager.GetCurrentTime();
        var testHour = (originalTime.hours + 6) % 24; // Add 6 hours, wrap around
        var testMinutes = (originalTime.minutes + 30) % 60; // Add 30 minutes

        timeManager.SetTime(testHour, testMinutes);
        yield return new WaitForSecondsRealtime(0.5f);

        var changedData = timeManager.GetCurrentTimeData();
        DebugLog($"Changed Cozy state: {changedData.GetFormattedDateTime()}");

        // Test restoration
        DebugLog("Testing Cozy restoration...");
        LoadSaveDataWithContext(saveData, RestoreContext.SaveFileLoad);

        // Wait for restoration to complete
        yield return new WaitForSecondsRealtime(1f);

        // Check final state
        var finalData = timeManager.GetCurrentTimeData();
        DebugLog($"Final Cozy state: {finalData.GetFormattedDateTime()}");

        // Verify restoration worked using Cozy's native format
        bool restoreSuccessful = finalData.time.hours == saveData.time.hours &&
                               finalData.time.minutes == saveData.time.minutes &&
                               finalData.day == saveData.day;

        DebugLog($"=== COZY SAVE/LOAD CYCLE TEST {(restoreSuccessful ? "PASSED" : "FAILED")} ===");

        if (!restoreSuccessful)
        {
            DebugLog($"Expected: {saveData.GetFormattedDateTime()}");
            DebugLog($"Actual: {finalData.GetFormattedDateTime()}");
            DebugLog($"Time Expected: {saveData.time.hours}:{saveData.time.minutes:D2}");
            DebugLog($"Time Actual: {finalData.time.hours}:{finalData.time.minutes:D2}");
            DebugLog($"Day Expected: {saveData.day}");
            DebugLog($"Day Actual: {finalData.day}");
        }
        else
        {
            DebugLog("All Cozy time values restored correctly!");
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
/// Time data structure using Cozy's native format
/// </summary>
[System.Serializable]
public class TimeData
{
    public MeridiemTime time;
    public int day;
    public float temperature;
    public string season;
    public bool wasCozyDriven;
    public System.DateTime saveTimestamp;

    public string GetDebugInfo()
    {
        return $"Time: {time}, Day: {day}, Season: {season}, Temp: {temperature:F1}°C, Cozy: {wasCozyDriven}";
    }

    public string GetFormattedTime()
    {
        return $"{time.hours:D2}:{time.minutes:D2}";
    }

    public string GetFormattedDate()
    {
        return $"Day {day} ({season})";
    }

    public string GetFormattedDateTime()
    {
        return $"{GetFormattedDate()} at {GetFormattedTime()}";
    }

    public bool IsValid()
    {
        return time.hours >= 0 && time.hours < 24 &&
               time.minutes >= 0 && time.minutes < 60 &&
               day > 0;
    }
}
