using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// FIXED: Dedicated save component for weather systems. Now properly handles all persistence,
/// save/load operations, and data restoration for weather-related managers.
/// Uses WeatherSaveData specifically for weather-related data only.
/// </summary>
public class WeatherSystemSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private WeatherManager weatherManager;
    [SerializeField] private bool autoFindManager = true;

    [Header("Weather Settings")]
    [SerializeField] private float defaultBaseTemperature = 20f;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        saveID = "Weather_Main";
        autoGenerateID = false;
        enableDebugLogs = true;
        base.Awake();

        if (autoFindManager)
        {
            FindWeatherManager();
        }
    }

    private void Start()
    {
        ValidateReferences();

        // // Register with PlayerPersistenceManager if available
        // if (PlayerPersistenceManager.Instance != null)
        // {
        //     PlayerPersistenceManager.Instance.RegisterComponent(this);
        //     // DebugLog("Registered with PlayerPersistenceManager");
        // }
        // else
        // {
        //     DebugLog("PlayerPersistenceManager not found - will be discovered automatically");
        // }
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

        //        DebugLog($"Auto-found WeatherManager: {weatherManager != null}");
    }

    /// <summary>
    /// Validates that the manager reference is available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (weatherManager == null)
        {
            Debug.LogWarning($"[{name}] WeatherManager reference missing! Weather data won't be saved.");
        }
        else
        {
            //            DebugLog("WeatherManager reference validated successfully");
        }
    }

    /// <summary>
    /// FIXED: Extracts current weather system state from the WeatherManager.
    /// Returns actual weather data including active events and temperature info.
    /// </summary>
    public override object GetDataToSave()
    {
        return ExtractWeatherDataFromManager();
    }

    private WeatherSaveData ExtractWeatherDataFromManager()
    {
        if (weatherManager == null)
        {
            DebugLog("Cannot extract - WeatherManager reference is null, creating default data");
            return CreateDefaultWeatherData();
        }
        else
        {
            WeatherSaveData extractedData = new WeatherSaveData
            {
                currentBaseTemperature = weatherManager.GetBaseTemperature(),
                weatherTemperatureModifier = weatherManager.GetWeatherTemperatureModifier(),
                lastWeatherUpdateTime = Time.time
            };

            var activeEvents = weatherManager.GetActiveWeatherEvents();
            foreach (var weatherEvent in activeEvents)
            {
                var eventData = weatherEvent.ToSaveData();
                extractedData.AddWeatherEvent(eventData);
            }

            return extractedData;
        }
    }

    /// <summary>
    /// Creates default weather data when no manager is available.
    /// </summary>
    private WeatherSaveData CreateDefaultWeatherData()
    {
        return new WeatherSaveData
        {
            currentBaseTemperature = defaultBaseTemperature,
            weatherTemperatureModifier = 0f,
            lastWeatherUpdateTime = 0f
        };
    }

    /// <summary>
    /// Extracts weather system data from various save container formats.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog($"Extracting weather system data from container type: {saveContainer?.GetType().Name ?? "null"}");

        if (saveContainer is WeatherSaveData weatherData)
        {
            DebugLog($"Direct extraction - Weather events: {weatherData.GetActiveWeatherEventCount()}");
            return weatherData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            var extractedData = persistentData.GetComponentData<WeatherSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from persistent data - Weather events: {extractedData.GetActiveWeatherEventCount()}");
            }
            else
            {
                DebugLog("No weather system data found in persistent data");
            }
            return extractedData;
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            var extractedData = playerSaveData.GetCustomData<WeatherSaveData>(SaveID);
            if (extractedData != null)
            {
                DebugLog($"Extracted from player save data - Weather events: {extractedData.GetActiveWeatherEventCount()}");
            }
            else
            {
                DebugLog("No weather system data found in player save data");
            }
            return extractedData;
        }

        DebugLog($"Unsupported save container type: {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts weather system data from the unified save structure.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null)
        {
            DebugLog("Cannot extract from unified save - unifiedData is null");
            return null;
        }

        DebugLog("Using modular extraction from unified save data");
        var extractedData = unifiedData.GetComponentData<WeatherSaveData>(SaveID);

        if (extractedData != null)
        {
            DebugLog($"Modular extraction successful - Weather events: {extractedData.GetActiveWeatherEventCount()}");
        }
        else
        {
            DebugLog("No weather system data found in unified save structure");
        }

        return extractedData;
    }

    /// <summary>
    /// Creates default weather system data for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default weather system data for new game");

        var defaultData = new WeatherSaveData
        {
            currentBaseTemperature = defaultBaseTemperature,
            weatherTemperatureModifier = 0f,
            lastWeatherUpdateTime = 0f
        };

        DebugLog($"Created default weather data: Base temp {defaultData.currentBaseTemperature}°C");
        return defaultData;
    }

    /// <summary>
    /// Stores weather system data into the unified save structure.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is WeatherSaveData weatherData && unifiedData != null)
        {
            DebugLog($"Contributing weather system data to unified save: {weatherData.GetActiveWeatherEventCount()} events");
            unifiedData.SetComponentData(SaveID, weatherData);
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected WeatherSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// FIXED: Context-aware data restoration to the weather manager.
    /// Now properly restores weather events and temperature data.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        DebugLog($"=== LOADING WEATHER SYSTEM DATA (Context: {context}) ===");

        if (!(data is WeatherSaveData weatherData))
        {
            DebugLog($"Invalid save data type - expected WeatherSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"Received valid data - Weather events: {weatherData.GetActiveWeatherEventCount()}");

        // Refresh manager reference in case it changed after scene load
        if (autoFindManager && weatherManager == null)
        {
            FindWeatherManager();
        }

        if (weatherManager == null)
        {
            Debug.LogError("WeatherManager not found - cannot restore weather data!");
            return;
        }

        // Validate data before applying
        if (!weatherData.IsValid())
        {
            Debug.LogWarning("Weather system save data failed validation - applying anyway with corrections");
        }

        // Apply the data to the manager
        RestoreWeatherData(weatherData, context);

        DebugLog($"Weather system data restoration complete for context: {context}");
    }

    /// <summary>
    /// FIXED: Applies weather system data to the weather manager.
    /// Now properly restores temperature settings and recreates weather events.
    /// </summary>
    private void RestoreWeatherData(WeatherSaveData weatherData, RestoreContext context)
    {
        DebugLog($"  Active events: {weatherData.GetActiveWeatherEventCount()}");

        // Clear existing weather events
        weatherManager.ClearAllWeather();

        // Restore base temperature
        weatherManager.SetBaseTemperature(weatherData.currentBaseTemperature);

        // FIXED: Restore active weather events
        if (weatherData.activeWeatherEvents != null && weatherData.activeWeatherEvents.Count > 0)
        {
            DebugLog($"Restoring {weatherData.activeWeatherEvents.Count} weather events:");

            foreach (var eventData in weatherData.activeWeatherEvents)
            {
                try
                {
                    // Create weather event instance from save data
                    var restoredEvent = new WeatherEventInstance(eventData);

                    // Add to weather manager
                    weatherManager.RestoreWeatherEvent(restoredEvent);

                    DebugLog($"  Restored: {restoredEvent.DisplayName} ({restoredEvent.CurrentPhase}, {restoredEvent.RemainingDuration:F1}h remaining)");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to restore weather event {eventData.eventType}: {e.Message}");
                }
            }
        }
        else
        {
            DebugLog("No weather events to restore");
        }

        // Force temperature recalculation
        weatherManager.ForceTemperatureUpdate();

        DebugLog("Weather data applied to manager successfully");
    }

    /// <summary>
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing weather system data for save operation");

        if (autoFindManager)
        {
            FindWeatherManager();
        }

        ValidateReferences();
    }

    /// <summary>
    /// Called after load operations to refresh connected systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Weather system data load completed - refreshing connected systems");

        // Force weather manager to update displays and events
        if (weatherManager != null)
        {
            weatherManager.ForceTemperatureUpdate();
        }

        // Force weather debug UI to update
        var weatherDebugUI = FindFirstObjectByType<WeatherDebugUI>();
        if (weatherDebugUI != null)
        {
            weatherDebugUI.ForceUpdate();
        }
    }

    /// <summary>
    /// Manual button to test save/load functionality in the editor.
    /// </summary>
    [Button("Test Save Data")]
    public void TestSaveData()
    {
        var data = GetDataToSave();
        if (data is WeatherSaveData weatherData)
        {
            DebugLog($"Test save data: {weatherData.GetDebugInfo()}");
        }
        else
        {
            DebugLog("Test save failed - no data returned");
        }
    }

    /// <summary>
    /// Manual button to test weather event creation and saving.
    /// </summary>
    [Button("Test Weather Events for Save")]
    public void TestWeatherEventsForSave()
    {
        if (weatherManager != null)
        {
            var events = weatherManager.GetActiveWeatherEvents();
            DebugLog($"Current active weather events: {events.Count}");

            foreach (var evt in events)
            {
                DebugLog($"  {evt.DisplayName}: {evt.CurrentPhase}, {evt.RemainingDuration:F1}h, Intensity: {evt.CurrentIntensity:F2}");
            }

            if (events.Count == 0)
            {
                DebugLog("No active weather events - try manually starting one first");
            }
        }
        else
        {
            DebugLog("WeatherManager not found");
        }
    }

    // /// <summary>
    // /// Manual button to test the save component registration.
    // /// </summary>
    // [Button("Test Registration")]
    // public void TestRegistration()
    // {
    //     if (PlayerPersistenceManager.Instance != null)
    //     {
    //         PlayerPersistenceManager.Instance.RegisterComponent(this);
    //         DebugLog("Manually registered with PlayerPersistenceManager");
    //     }
    //     else
    //     {
    //         DebugLog("PlayerPersistenceManager not found");
    //     }
    // }

    private void OnDestroy()
    {
        // Unregister from persistence manager
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.UnregisterComponent(this);
        }
    }
}