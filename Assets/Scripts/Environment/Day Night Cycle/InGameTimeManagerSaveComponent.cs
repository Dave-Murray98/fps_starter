using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Dedicated save component for the Day/Night Cycle system. Handles all persistence,
/// save/load operations, and data restoration for the DayNightCycleManager.
/// This separation ensures the save system can properly discover and manage day/night data.
/// </summary>
public class InGameTimeManagerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private InGameTimeManager inGameTimeManager;
    [SerializeField] private bool autoFindManager = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "GameTime_Main";
        autoGenerateID = false;
        enableDebugLogs = true; // Enable for debugging
        base.Awake();

        if (autoFindManager)
        {
            FindDayNightManager();
        }
    }

    private void Start()
    {
        ValidateReferences();

        // Register with PlayerPersistenceManager if available
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.RegisterComponent(this);
            DebugLog("Registered with PlayerPersistenceManager");
        }
        else
        {
            DebugLog("PlayerPersistenceManager not found - will be discovered automatically");
        }
    }

    /// <summary>
    /// Automatically locates the DayNightCycleManager in the scene.
    /// </summary>
    private void FindDayNightManager()
    {
        if (inGameTimeManager == null)
        {
            inGameTimeManager = InGameTimeManager.Instance;

            if (inGameTimeManager == null)
            {
                inGameTimeManager = FindFirstObjectByType<InGameTimeManager>();
            }
        }

        DebugLog($"Auto-found DayNightCycleManager: {inGameTimeManager != null}");
    }

    /// <summary>
    /// Validates that the manager reference is available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (inGameTimeManager == null)
        {
            Debug.LogError($"[{name}] DayNightCycleManager reference missing! Time data won't be saved.");
        }
        else
        {
            DebugLog("DayNightCycleManager reference validated successfully");
        }
    }

    /// <summary>
    /// Extracts current day/night cycle state from the manager.
    /// </summary>
    public override object GetDataToSave()
    {
        if (inGameTimeManager == null)
        {
            DebugLog("Cannot save - DayNightCycleManager reference is null");
            return null;
        }

        var saveData = new EnvironmentSaveData
        {
            currentTimeOfDay = inGameTimeManager.GetCurrentTimeOfDay(),
            currentSeason = inGameTimeManager.GetCurrentSeason(),
            currentDayOfSeason = inGameTimeManager.GetCurrentDayOfSeason(),
            totalDaysElapsed = inGameTimeManager.GetTotalDaysElapsed(),
            dayDurationMinutes = inGameTimeManager.dayDurationMinutes,
            currentTemperatureModifier = inGameTimeManager.GetTemperatureModifier()
        };

        DebugLog($"Saving day/night data: {saveData.GetFormattedDateTime()}, Health check: {saveData.IsValid()}");
        return saveData;
    }

    /// <summary>
    /// Extracts day/night data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting day/night data from container type: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer is EnvironmentSaveData envData)
        {
            DebugLog($"Direct extraction - Time: {envData.GetFormattedDateTime()}");
            return envData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedData = persistentData.GetComponentData<EnvironmentSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from persistent data - Time: {extractedData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("No day/night data found in persistent data");
            }
            return extractedData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedData = playerSaveData.GetCustomData<EnvironmentSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from player save data - Time: {extractedData.GetFormattedDateTime()}");
            }
            else
            {
                DebugLog("No day/night data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts day/night data from the unified save structure.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Cannot extract from unified save - unifiedData is null");
            return null;
        }

        DebugLog("Using modular extraction from unified save data");
        var extractedData = unifiedData.GetComponentData<EnvironmentSaveData>(SaveID);

        if (extractedData != null)
        {
            DebugLog($"Modular extraction successful - Time: {extractedData.GetFormattedDateTime()}");
        }
        else
        {
            DebugLog("No day/night data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default day/night data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default day/night data for new game");

        // Use manager's configured defaults if available
        if (inGameTimeManager != null)
        {
            return new EnvironmentSaveData
            {
                currentTimeOfDay = inGameTimeManager.startTimeOfDay,
                currentSeason = inGameTimeManager.startingSeason,
                currentDayOfSeason = inGameTimeManager.startingDayOfSeason,
                totalDaysElapsed = 0,
                dayDurationMinutes = inGameTimeManager.dayDurationMinutes,
                currentTemperatureModifier = 0f
            };
        }
        else
        {
            // Fallback defaults
            var defaultData = new EnvironmentSaveData
            {
                currentTimeOfDay = 6f,
                currentSeason = SeasonType.Spring,
                currentDayOfSeason = 1,
                totalDaysElapsed = 0,
                dayDurationMinutes = 20f,
                currentTemperatureModifier = 0f
            };

            DebugLog($"Created fallback default data: {defaultData.GetFormattedDateTime()}");
            return defaultData;
        }
    }

    /// <summary>
    /// Stores day/night data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is EnvironmentSaveData envData && unifiedData != null)
        {
            DebugLog($"Contributing day/night data to unified save: {envData.GetFormattedDateTime()}");
            unifiedData.SetComponentData(SaveID, envData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected EnvironmentSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration to the DayNightCycleManager.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING DAY/NIGHT DATA (Context: {context}) ===");

        if (!(data is EnvironmentSaveData envData))
        {
            DebugLog($"Invalid save data type - expected EnvironmentSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid data - Time: {envData.GetFormattedDateTime()}");

        // Refresh manager reference in case it changed after scene load
        if (autoFindManager && inGameTimeManager == null)
        {
            FindDayNightManager();
        }

        if (inGameTimeManager == null)
        {
            Debug.LogError("Cannot restore day/night data - DayNightCycleManager not found!");
            return;
        }

        // Validate data before applying
        if (!envData.IsValid())
        {
            Debug.LogWarning("Day/night save data failed validation - applying anyway with corrections");
        }

        // Apply the data to the manager
        RestoreTimeData(envData, context);

        DebugLog($"Day/night data restoration complete for context: {context}");
    }

    /// <summary>
    /// Applies environment data to the DayNightCycleManager.
    /// </summary>
    private void RestoreTimeData(EnvironmentSaveData envData, RestoreContext context)
    {
        DebugLog($"Restoring time data to manager:");
        DebugLog($"  Current manager time: {inGameTimeManager.GetCurrentTimeOfDay():F2}");
        DebugLog($"  Restoring to time: {envData.currentTimeOfDay:F2}");
        DebugLog($"  Season: {envData.currentSeason}, Day: {envData.currentDayOfSeason}");

        // Apply all the data through the manager's methods
        inGameTimeManager.SetGameDate(envData.currentSeason, envData.currentDayOfSeason, envData.currentTimeOfDay);
        inGameTimeManager.SetDayDuration(envData.dayDurationMinutes);

        // Set total days elapsed (access private field via reflection if needed, or add public method)
        SetTotalDaysElapsed(envData.totalDaysElapsed);

        DebugLog($"Time data applied - Manager now shows: {inGameTimeManager.GetFormattedDateTime()}");

        // Force an immediate event to update connected systems
        TestManagerEvents();
    }

    /// <summary>
    /// Sets the total days elapsed on the manager.
    /// </summary>
    private void SetTotalDaysElapsed(int totalDays)
    {
        // Since totalDaysElapsed is private, we need to use reflection or add a public method
        // For now, we'll use reflection as a temporary solution
        var field = typeof(InGameTimeManager).GetField("totalDaysElapsed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(inGameTimeManager, totalDays);
            DebugLog($"Set total days elapsed to: {totalDays}");
        }
        else
        {
            DebugLog("Could not set totalDaysElapsed - field not found");
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
        DebugLog("Preparing day/night data for save operation");

        if (autoFindManager)
        {
            FindDayNightManager();
        }

        ValidateReferences();
    }

    /// <summary>
    /// Called after load operations to refresh connected systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Day/night data load completed - refreshing connected systems");

        if (inGameTimeManager != null)
        {
            // Test events to ensure lighting controllers get updated
            inGameTimeManager.TestEvents();
        }
    }

    /// <summary>
    /// Manual button to test save/load functionality in the editor.
    /// </summary>
    [Button("Test Save Data")]
    public void TestSaveData()
    {
        var data = GetDataToSave();
        if (data is EnvironmentSaveData envData)
        {
            DebugLog($"Test save data: {envData.GetDebugInfo()}");
        }
        else
        {
            DebugLog("Test save failed - no data returned");
        }
    }

    /// <summary>
    /// Manual button to test the save component registration.
    /// </summary>
    [Button("Test Registration")]
    public void TestRegistration()
    {
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.RegisterComponent(this);
            DebugLog("Manually registered with PlayerPersistenceManager");
        }
        else
        {
            DebugLog("PlayerPersistenceManager not found");
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