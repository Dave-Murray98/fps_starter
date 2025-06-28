using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Dedicated save component for the In-Game Time System with Cozy Weather 3 integration.
/// Handles all persistence, save/load operations, and data restoration for the InGameTimeManager.
/// Now properly handles both Cozy-driven and manual time progression modes.
/// </summary>
public class InGameTimeManagerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private InGameTimeManager inGameTimeManager;
    [SerializeField] private bool autoFindManager = true;

    [Header("Cozy Integration")]
    [SerializeField] private bool restoreCozySettings = true;
    [SerializeField] private bool forceCozySync = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "InGameTimeSystem_Main";
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
        if (inGameTimeManager == null)
        {
            inGameTimeManager = InGameTimeManager.Instance;

            if (inGameTimeManager == null)
            {
                inGameTimeManager = FindFirstObjectByType<InGameTimeManager>();
            }
        }
    }

    /// <summary>
    /// Validates that the manager reference is available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (inGameTimeManager == null)
        {
            Debug.LogError($"[{name}] InGameTimeManager reference missing! Time data won't be saved.");
        }
        else
        {
            DebugLog("InGameTimeManager reference validated successfully");
        }
    }

    /// <summary>
    /// Extracts current time system state from the manager.
    /// Returns only time-related data - no weather information.
    /// </summary>
    public override object GetDataToSave()
    {
        if (inGameTimeManager == null)
        {
            DebugLog("Cannot save - InGameTimeManager reference is null");
            return null;
        }

        var saveData = new InGameTimeSystemSaveData
        {
            currentTimeOfDay = inGameTimeManager.GetCurrentTimeOfDay(),
            currentSeason = inGameTimeManager.GetCurrentSeason(),
            currentDayOfSeason = inGameTimeManager.GetCurrentDayOfSeason(),
            totalDaysElapsed = inGameTimeManager.GetTotalDaysElapsed(),
            dayDurationMinutes = inGameTimeManager.dayDurationMinutes,
            daysPerSeason = inGameTimeManager.daysPerSeason,
            daysPerYear = inGameTimeManager.GetDaysPerYear()
        };

        DebugLog($"Saving time system data: {saveData.GetFormattedDateTime()}, Health check: {saveData.IsValid()}");
        return saveData;
    }

    /// <summary>
    /// Extracts time system data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting time system data from container type: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer is InGameTimeSystemSaveData timeData)
        {
            DebugLog($"Direct extraction - Time: {timeData.GetFormattedDateTime()}");
            return timeData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedData = persistentData.GetComponentData<InGameTimeSystemSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from persistent data - Time: {extractedData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("No time system data found in persistent data");
            }
            return extractedData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedData = playerSaveData.GetCustomData<InGameTimeSystemSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from player save data - Time: {extractedData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("No time system data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts time system data from the unified save structure.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Cannot extract from unified save - unifiedData is null");
            return null;
        }

        DebugLog("Using modular extraction from unified save data");
        var extractedData = unifiedData.GetComponentData<InGameTimeSystemSaveData>(SaveID);

        if (extractedData != null)
        {
            DebugLog($"Modular extraction successful - Time: {extractedData.GetFormattedDateTime()}");
        }
        else
        {
            DebugLog("No time system data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default time system data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default time system data for new game");

        // Use manager's configured defaults if available
        if (inGameTimeManager != null)
        {
            return new InGameTimeSystemSaveData
            {
                currentTimeOfDay = inGameTimeManager.startTimeOfDay,
                currentSeason = inGameTimeManager.startingSeason,
                currentDayOfSeason = inGameTimeManager.startingDayOfSeason,
                totalDaysElapsed = 0,
                dayDurationMinutes = inGameTimeManager.dayDurationMinutes,
                daysPerSeason = inGameTimeManager.daysPerSeason,
                daysPerYear = inGameTimeManager.GetDaysPerYear()
            };
        }
        else
        {
            // Fallback defaults
            var defaultData = new InGameTimeSystemSaveData
            {
                currentTimeOfDay = 6f,
                currentSeason = SeasonType.Spring,
                currentDayOfSeason = 1,
                totalDaysElapsed = 0,
                dayDurationMinutes = 20f,
                daysPerSeason = 30,
                daysPerYear = 120
            };

            DebugLog($"Created fallback default data: {defaultData.GetFormattedDateTime()}");
            return defaultData;
        }
    }

    /// <summary>
    /// Stores time system data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is InGameTimeSystemSaveData timeData && unifiedData != null)
        {
            DebugLog($"Contributing time system data to unified save: {timeData.GetFormattedDateTime()}");
            unifiedData.SetComponentData(SaveID, timeData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected InGameTimeSystemSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration to the InGameTimeManager with Cozy integration support.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING TIME SYSTEM DATA (Context: {context}) ===");

        if (!(data is InGameTimeSystemSaveData timeData))
        {
            DebugLog($"Invalid save data type - expected InGameTimeSystemSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid data - Time: {timeData.GetFormattedDateTime()}");

        // Refresh manager reference in case it changed after scene load
        if (autoFindManager && inGameTimeManager == null)
        {
            FindTimeManager();
        }

        if (inGameTimeManager == null)
        {
            Debug.LogError("Cannot restore time system data - InGameTimeManager not found!");
            return;
        }

        // Validate data before applying
        if (!timeData.IsValid())
        {
            Debug.LogWarning("Time system save data failed validation - applying anyway with corrections");
        }

        // Apply the data to the manager
        RestoreTimeData(timeData, context);

        DebugLog($"Time system data restoration complete for context: {context}");
    }

    /// <summary>
    /// Applies time system data to the InGameTimeManager with Cozy integration.
    /// Updated to handle the new Cozy-driven approach properly.
    /// </summary>
    private void RestoreTimeData(InGameTimeSystemSaveData timeData, RestoreContext context)
    {
        DebugLog($"Restoring time data to manager:");
        DebugLog($"  Current manager time: {inGameTimeManager.GetCurrentTimeOfDay():F2}");
        DebugLog($"  Restoring to time: {timeData.currentTimeOfDay:F2}");
        DebugLog($"  Season: {timeData.currentSeason}, Day: {timeData.currentDayOfSeason}");

        // Step 1: Update configuration settings
        UpdateTimeConfiguration(timeData);

        // Step 2: Wait a frame for Cozy to potentially update, then set the actual time/date
        StartCoroutine(RestoreTimeDataDelayed(timeData, context));
    }

    /// <summary>
    /// Updates the time manager's configuration settings (day duration, days per season, etc.)
    /// </summary>
    private void UpdateTimeConfiguration(InGameTimeSystemSaveData timeData)
    {
        // Update day duration in the manager's public field
        inGameTimeManager.dayDurationMinutes = timeData.dayDurationMinutes;

        // Update days per season in the manager's public field
        inGameTimeManager.daysPerSeason = timeData.daysPerSeason;

        DebugLog($"Updated configuration - Day duration: {timeData.dayDurationMinutes}min, Days per season: {timeData.daysPerSeason}");

        // If the manager has methods to update these settings and sync with Cozy, call them
        // Note: SetDayDuration method doesn't exist in the new Cozy-driven approach
        // The configuration is read directly from the public fields
    }

    /// <summary>
    /// Delayed restoration to allow Cozy to update after configuration changes
    /// </summary>
    private System.Collections.IEnumerator RestoreTimeDataDelayed(InGameTimeSystemSaveData timeData, RestoreContext context)
    {
        // Wait a frame to allow any Cozy updates to process
        yield return null;

        DebugLog("Applying saved time and date values");

        // Apply the saved time and date
        inGameTimeManager.SetGameDate(timeData.currentSeason, timeData.currentDayOfSeason, timeData.currentTimeOfDay);

        // Set total days elapsed
        inGameTimeManager.SetTotalDaysElapsed(timeData.totalDaysElapsed);

        DebugLog($"Time data applied - Manager now shows: {inGameTimeManager.GetFormattedDateTime()}");

        // Force Cozy reconnection and sync if needed
        if (restoreCozySettings && inGameTimeManager.IsCozyConnected())
        {
            DebugLog("Forcing Cozy sync after data restoration");
            inGameTimeManager.ReconnectToCozy();

            if (forceCozySync)
            {
                // Wait another frame and force sync
                yield return null;
                inGameTimeManager.ReconnectToCozy();
            }
        }

        // Test manager events to ensure systems are working
        TestManagerEvents();

        DebugLog("Delayed time data restoration completed");
    }

    /// <summary>
    /// Tests that the manager's events are working after data restoration.
    /// </summary>
    private void TestManagerEvents()
    {
        DebugLog("Testing manager events after restoration...");
        inGameTimeManager.TestEvents();
    }

    /// <summary>
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing time system data for save operation");

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
        DebugLog("Time system data load completed - refreshing connected systems");

        if (inGameTimeManager != null)
        {
            // Test events to ensure lighting controllers and UI get updated
            inGameTimeManager.TestEvents();

            // Force Cozy reconnection if enabled
            if (restoreCozySettings && inGameTimeManager.IsCozyConnected())
            {
                StartCoroutine(DelayedCozyRefresh());
            }
        }
    }

    /// <summary>
    /// Delayed Cozy refresh to ensure everything is properly initialized
    /// </summary>
    private System.Collections.IEnumerator DelayedCozyRefresh()
    {
        yield return new WaitForSecondsRealtime(0.1f);

        if (inGameTimeManager != null)
        {
            DebugLog("Performing delayed Cozy refresh");
            inGameTimeManager.ReconnectToCozy();
        }
    }

    /// <summary>
    /// Manual method to force complete restoration from saved data (for debugging)
    /// </summary>
    [Button("Force Restore Test")]
    public void ForceRestoreTest()
    {
        if (inGameTimeManager == null)
        {
            DebugLog("Cannot test - InGameTimeManager reference missing");
            return;
        }

        // Get current data
        var currentData = GetDataToSave() as InGameTimeSystemSaveData;
        if (currentData != null)
        {
            DebugLog("Testing restoration with current data");
            LoadSaveDataWithContext(currentData, RestoreContext.SaveFileLoad);
        }
    }

    /// <summary>
    /// Toggles Cozy restoration settings
    /// </summary>
    [Button("Toggle Cozy Restore")]
    public void ToggleCozyRestore()
    {
        restoreCozySettings = !restoreCozySettings;
        DebugLog($"Cozy restoration {(restoreCozySettings ? "enabled" : "disabled")}");
    }

    private void OnDestroy()
    {
        // Unregister from persistence manager
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.UnregisterComponent(this);
        }
    }
}