using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Dedicated save component for the In-Game Time System. Handles all persistence,
/// save/load operations, and data restoration for the InGameTimeManager.
/// Now uses InGameTimeSystemSaveData specifically for time-related data only.
/// Weather data is handled separately by weather systems.
/// </summary>
public class InGameTimeManagerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private InGameTimeManager inGameTimeManager;
    [SerializeField] private bool autoFindManager = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "InGameTimeSystem_Main";
        autoGenerateID = false;
        //enableDebugLogs = true; // Enable for debugging
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
            //            DebugLog("InGameTimeManager reference validated successfully");
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
            dayDurationMinutes = inGameTimeManager.dayDurationMinutes
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
                dayDurationMinutes = inGameTimeManager.dayDurationMinutes
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
                dayDurationMinutes = 20f
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
    /// Context-aware data restoration to the InGameTimeManager.
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
    /// Applies time system data to the InGameTimeManager.
    /// </summary>
    private void RestoreTimeData(InGameTimeSystemSaveData timeData, RestoreContext context)
    {
        DebugLog($"Restoring time data to manager:");
        DebugLog($"  Current manager time: {inGameTimeManager.GetCurrentTimeOfDay():F2}");
        DebugLog($"  Restoring to time: {timeData.currentTimeOfDay:F2}");
        DebugLog($"  Season: {timeData.currentSeason}, Day: {timeData.currentDayOfSeason}");

        // Apply all the data through the manager's methods
        inGameTimeManager.SetGameDate(timeData.currentSeason, timeData.currentDayOfSeason, timeData.currentTimeOfDay);
        inGameTimeManager.SetDayDuration(timeData.dayDurationMinutes);

        // Set total days elapsed - use reflection since the method might be private
        SetTotalDaysElapsed(timeData.totalDaysElapsed);

        DebugLog($"Time data applied - Manager now shows: {inGameTimeManager.GetFormattedDateTime()}");

        // Force an immediate event to update connected systems
        TestManagerEvents();
    }

    /// <summary>
    /// Sets the total days elapsed on the manager using the public method if available,
    /// or reflection if needed for backwards compatibility.
    /// </summary>
    private void SetTotalDaysElapsed(int totalDays)
    {
        // Try to call the public method first
        var method = typeof(InGameTimeManager).GetMethod("SetTotalDaysElapsed",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (method != null)
        {
            method.Invoke(inGameTimeManager, new object[] { totalDays });
            DebugLog($"Set total days elapsed to: {totalDays} using public method");
        }
        else
        {
            // Fall back to reflection on private field
            var field = typeof(InGameTimeManager).GetField("totalDaysElapsed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(inGameTimeManager, totalDays);
                DebugLog($"Set total days elapsed to: {totalDays} using reflection");
            }
            else
            {
                DebugLog("Could not set totalDaysElapsed - neither method nor field found");
            }
        }
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
            // Test events to ensure lighting controllers get updated
            inGameTimeManager.TestEvents();
        }
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