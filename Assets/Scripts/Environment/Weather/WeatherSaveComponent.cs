using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Save component for the Cozy-driven weather system. Since Cozy now handles all weather
/// and temperature logic, this component simply saves/loads Cozy's current state for
/// persistence across scene transitions and game sessions.
/// 
/// Much simpler than before - just captures Cozy's state and attempts to restore it.
/// </summary>
public class WeatherSystemSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private bool autoFindManager = true;

    [Header("Save Settings")]
    [SerializeField] private bool saveCozyState = true;
    [SerializeField] private bool restoreCozyState = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "CozyWeather_Interface";
        autoGenerateID = false;
        base.Awake();

        if (autoFindManager)
        {
            FindWeatherManager();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates the weather manager in the scene.
    /// </summary>
    private void FindWeatherManager()
    {
        if (weatherManager == null)
        {
            weatherManager = WeatherManager.Instance;
        }

        if (weatherManager == null)
        {
            weatherManager = FindFirstObjectByType<WeatherManager>();
        }
    }

    /// <summary>
    /// Validates that the manager reference is available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (weatherManager == null)
        {
            Debug.LogWarning($"[{name}] WeatherManager reference missing! Cozy weather state won't be saved.");
        }
        else
        {
            DebugLog("WeatherManager reference validated successfully");
        }
    }

    /// <summary>
    /// Gets current Cozy weather state for saving.
    /// Much simpler now - just captures what Cozy is currently doing.
    /// </summary>
    public override object GetDataToSave()
    {
        if (weatherManager == null || !saveCozyState)
        {
            DebugLog("Cannot save - WeatherManager reference missing or save disabled");
            return CreateDefaultSaveData();
        }

        if (!weatherManager.IsCozyConnected())
        {
            DebugLog("Cozy not connected - saving default state");
            return CreateDefaultSaveData();
        }

        var saveData = weatherManager.GetSaveData();
        DebugLog($"Saving Cozy weather state: {saveData.GetDebugInfo()}");
        return saveData;
    }

    /// <summary>
    /// Creates default save data when Cozy isn't available
    /// </summary>
    private CozyWeatherSaveData CreateDefaultSaveData()
    {
        return new CozyWeatherSaveData
        {
            weatherName = "Clear",
            temperature = 20f,
            precipitation = 0f,
            saveTimestamp = System.DateTime.Now,
            cozyConnected = false
        };
    }

    /// <summary>
    /// Extracts weather data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting Cozy weather data from container type: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer is CozyWeatherSaveData weatherData)
        {
            DebugLog($"Direct extraction - {weatherData.GetDebugInfo()}");
            return weatherData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedData = persistentData.GetComponentData<CozyWeatherSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from persistent data - {extractedData.GetDebugInfo()}");
            }
            else
            {
                DebugLog("No Cozy weather data found in persistent data");
            }
            return extractedData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedData = playerSaveData.GetCustomData<CozyWeatherSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from player save data - {extractedData.GetDebugInfo()}");
            }
            else
            {
                DebugLog("No Cozy weather data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts Cozy weather data from the unified save structure.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Cannot extract from unified save - unifiedData is null");
            return null;
        }

        DebugLog("Using modular extraction from unified save data");
        var extractedData = unifiedData.GetComponentData<CozyWeatherSaveData>(SaveID);

        if (extractedData != null)
        {
            DebugLog($"Modular extraction successful - {extractedData.GetDebugInfo()}");
        }
        else
        {
            DebugLog("No Cozy weather data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default Cozy weather data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default Cozy weather data for new game");

        var defaultData = new CozyWeatherSaveData
        {
            weatherName = "Clear",
            temperature = 20f,
            precipitation = 0f,
            saveTimestamp = System.DateTime.Now,
            cozyConnected = false
        };

        DebugLog($"Created default Cozy weather data: {defaultData.GetDebugInfo()}");
        return defaultData;
    }

    /// <summary>
    /// Stores Cozy weather data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is CozyWeatherSaveData weatherData && unifiedData != null)
        {
            DebugLog($"Contributing Cozy weather data to unified save: {weatherData.GetDebugInfo()}");
            unifiedData.SetComponentData(SaveID, weatherData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected CozyWeatherSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration. Attempts to restore Cozy's state from saved data.
    /// This may have limited success since Cozy controls its own weather logic.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING COZY WEATHER DATA (Context: {context}) ===");

        if (!(data is CozyWeatherSaveData weatherData))
        {
            DebugLog($"Invalid save data type - expected CozyWeatherSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid data - {weatherData.GetDebugInfo()}");

        // Refresh manager reference in case it changed after scene load
        if (autoFindManager && weatherManager == null)
        {
            FindWeatherManager();
        }

        if (weatherManager == null)
        {
            Debug.LogError("WeatherManager not found - cannot restore Cozy weather data!");
            return;
        }

        // Validate data before applying
        if (!weatherData.IsValid())
        {
            Debug.LogWarning("Cozy weather save data failed validation - applying anyway with corrections");
        }

        // Attempt to restore Cozy state
        if (restoreCozyState)
        {
            RestoreCozyWeatherData(weatherData, context);
        }
        else
        {
            DebugLog("Cozy state restoration disabled - skipping restoration");
        }

        DebugLog($"Cozy weather data restoration complete for context: {context}");
    }

    /// <summary>
    /// Attempts to restore Cozy weather state. Success depends on Cozy's API support.
    /// Since Cozy controls its own logic, this may have limited effectiveness.
    /// </summary>
    private void RestoreCozyWeatherData(CozyWeatherSaveData weatherData, RestoreContext context)
    {
        DebugLog($"Attempting to restore Cozy state:");
        DebugLog($"  Weather: {weatherData.weatherName}");
        DebugLog($"  Temperature: {weatherData.temperature:F1}°C");
        DebugLog($"  Precipitation: {weatherData.precipitation:F2}");

        // Wait a frame for Cozy to be fully initialized, then attempt restoration
        StartCoroutine(RestoreCozyStateDelayed(weatherData, context));
    }

    /// <summary>
    /// Delayed restoration to ensure Cozy is fully initialized
    /// </summary>
    private System.Collections.IEnumerator RestoreCozyStateDelayed(CozyWeatherSaveData weatherData, RestoreContext context)
    {
        // Wait for Cozy to be ready
        yield return new WaitForSecondsRealtime(0.2f);

        DebugLog("Attempting delayed Cozy state restoration");

        // Ensure weather manager is connected to Cozy
        weatherManager.ReconnectToCozy();

        yield return new WaitForSecondsRealtime(0.1f);

        // Attempt to restore the weather state
        if (weatherManager.IsCozyConnected())
        {
            try
            {
                weatherManager.RestoreFromSaveData(weatherData);
                DebugLog("Cozy state restoration attempted");
            }
            catch (System.Exception e)
            {
                DebugLog($"Error during Cozy state restoration: {e.Message}");
            }
        }
        else
        {
            DebugLog("Cannot restore Cozy state - not connected to Cozy");
        }

        // Force a read to ensure our data is current after restoration attempt
        yield return new WaitForSecondsRealtime(0.1f);
        weatherManager.ForceReadFromCozy();

        DebugLog("Delayed Cozy weather restoration completed");
    }

    /// <summary>
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing Cozy weather data for save operation");

        if (autoFindManager)
        {
            FindWeatherManager();
        }

        ValidateReferences();

        // Force a read from Cozy to ensure we have the latest state
        if (weatherManager != null && weatherManager.IsCozyConnected())
        {
            weatherManager.ForceReadFromCozy();
        }
    }

    /// <summary>
    /// Called after load operations to refresh connected systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Cozy weather data load completed - refreshing connected systems");

        if (weatherManager != null)
        {
            // Force weather manager to reconnect and read current state
            weatherManager.ReconnectToCozy();

            // // Update any weather debug UI components
            // var weatherDebugUI = FindFirstObjectByType<WeatherDebugUI>();
            // if (weatherDebugUI != null)
            // {
            //     weatherDebugUI.ForceUpdate();
            //     DebugLog("Refreshed weather debug UI");
            // }
        }
    }

    /// <summary>
    /// Manual method to force complete restoration from saved data (for debugging)
    /// </summary>
    [Button("Force Restore Test")]
    public void ForceRestoreTest()
    {
        if (weatherManager == null)
        {
            DebugLog("Cannot test - WeatherManager reference missing");
            return;
        }

        // Get current Cozy state
        var currentData = GetDataToSave() as CozyWeatherSaveData;
        if (currentData != null)
        {
            DebugLog("Testing restoration with current Cozy data");
            LoadSaveDataWithContext(currentData, RestoreContext.SaveFileLoad);
        }
    }

    /// <summary>
    /// Toggles Cozy state saving/restoration
    /// </summary>
    [Button("Toggle Cozy Save/Restore")]
    public void ToggleCozySaveRestore()
    {
        saveCozyState = !saveCozyState;
        restoreCozyState = saveCozyState; // Keep them in sync
        DebugLog($"Cozy save/restore {(saveCozyState ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Forces immediate save of current Cozy state (for debugging)
    /// </summary>
    [Button("Force Save Current State")]
    public void ForceSaveCurrentState()
    {
        if (weatherManager != null && weatherManager.IsCozyConnected())
        {
            var saveData = GetDataToSave() as CozyWeatherSaveData;
            if (saveData != null)
            {
                DebugLog($"Current Cozy state: {saveData.GetDebugInfo()}");
            }
        }
        else
        {
            DebugLog("Cannot save - WeatherManager not connected to Cozy");
        }
    }

    /// <summary>
    /// Gets detailed information about current save state
    /// </summary>
    [Button("Show Save Info")]
    public void ShowSaveInfo()
    {
        DebugLog("=== COZY WEATHER SAVE INFO ===");
        DebugLog($"Save Enabled: {saveCozyState}");
        DebugLog($"Restore Enabled: {restoreCozyState}");
        DebugLog($"Manager Connected: {weatherManager != null}");

        if (weatherManager != null)
        {
            DebugLog($"Cozy Connected: {weatherManager.IsCozyConnected()}");

            if (weatherManager.IsCozyConnected())
            {
                var currentData = weatherManager.GetCurrentWeatherData();
                DebugLog($"Current State: {currentData.GetDebugInfo()}");
            }
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