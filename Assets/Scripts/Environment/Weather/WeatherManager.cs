using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using DistantLands.Cozy;

/// <summary>
/// Weather data interface that reads from Cozy Weather 3's ecosystem and climate modules.
/// This manager no longer controls weather - instead it reads Cozy's state for game logic,
/// save/load operations, and UI display. Cozy handles all weather and temperature logic.
/// 
/// COZY-DRIVEN APPROACH: Cozy's forecast system, climate module, and ecosystem control
/// all weather events and temperature. This manager just observes and persists that data.
/// </summary>
public class WeatherManager : MonoBehaviour, IManager
{
    public static WeatherManager Instance { get; private set; }

    [Header("Cozy Integration Settings")]
    [SerializeField] private bool enableCozyReading = true;
    [SerializeField] private float cozyReadInterval = 0.1f; // How often to read from Cozy
    [SerializeField] private bool trackWeatherChanges = true;
    [SerializeField] private bool trackTemperatureChanges = true;

    [Header("Data Persistence Settings")]
    [SerializeField] private bool saveCozyState = true;
    [SerializeField] private bool restoreCozyState = true;
    [SerializeField] private float significantTempChange = 0.5f; // °C change to trigger events

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Current state read from Cozy (read-only data for game logic)
    [ShowInInspector, ReadOnly] private string currentWeatherName = "Clear";
    [ShowInInspector, ReadOnly] private float currentTemperature = 20f;
    [ShowInInspector, ReadOnly] private float currentPrecipitation = 0f;
    [ShowInInspector, ReadOnly] private bool isCozyConnected = false;

    // Weather change tracking
    [ShowInInspector, ReadOnly] private string previousWeatherName = "";
    [ShowInInspector, ReadOnly] private float previousTemperature = 20f;
    [ShowInInspector, ReadOnly] private float lastCozyReadTime = 0f;

    // Cached Cozy references
    private object currentWeatherProfile;
    private InGameTimeManager timeManager;

    // Events for external systems (simplified for Cozy-driven approach)
    public static event System.Action<string> OnWeatherChanged; // Weather profile name
    public static event System.Action<float> OnTemperatureChanged; // Current temperature
    public static event System.Action<CozyWeatherData> OnCozyWeatherUpdated; // Complete weather data

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("WeatherManager initialized as Cozy data interface");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region IManager Implementation

    public void Initialize()
    {
        RefreshReferences();
        ConnectToCozy();

        if (isCozyConnected)
        {
            ReadInitialCozyState();
        }

        DebugLog("WeatherManager initialized - reading from Cozy Weather 3");
    }

    public void RefreshReferences()
    {
        timeManager = InGameTimeManager.Instance;
        ConnectToCozy();
        DebugLog("References refreshed");
    }

    public void Cleanup()
    {
        DebugLog("Cleanup completed");
    }

    #endregion

    private void Update()
    {
        if (enableCozyReading && isCozyConnected)
        {
            ReadFromCozy();
        }
    }

    #region Cozy Data Reading

    /// <summary>
    /// Connects to Cozy Weather 3's modules for data reading
    /// </summary>
    private void ConnectToCozy()
    {
        if (CozyWeather.instance != null)
        {
            bool hasWeatherModule = CozyWeather.instance.weatherModule != null;
            bool hasClimateModule = CozyWeather.instance.climateModule != null;

            isCozyConnected = hasWeatherModule || hasClimateModule;

            if (isCozyConnected)
            {
                DebugLog($"Connected to Cozy - Weather Module: {hasWeatherModule}, Climate Module: {hasClimateModule}");
            }
            else
            {
                DebugLog("Cozy Weather 3 found but no weather or climate modules available");
            }
        }
        else
        {
            isCozyConnected = false;
            DebugLog("Cozy Weather 3 not found");
        }
    }

    /// <summary>
    /// Reads initial state from Cozy when connecting
    /// </summary>
    private void ReadInitialCozyState()
    {
        if (!isCozyConnected) return;

        try
        {
            // Read weather state
            ReadCurrentWeather();

            // Read temperature state
            ReadCurrentTemperature();

            // Read precipitation state
            ReadCurrentPrecipitation();

            // Initialize previous values
            previousWeatherName = currentWeatherName;
            previousTemperature = currentTemperature;

            DebugLog($"Initial Cozy state - Weather: {currentWeatherName}, Temp: {currentTemperature:F1}°C, Precip: {currentPrecipitation:F2}");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading initial Cozy state: {e.Message}");
        }
    }

    /// <summary>
    /// Reads current state from Cozy at regular intervals
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
            // Read current values
            ReadCurrentWeather();
            ReadCurrentTemperature();
            ReadCurrentPrecipitation();

            // Check for changes and fire events
            CheckForWeatherChanges();
            CheckForTemperatureChanges();

            // Fire comprehensive update event
            FireWeatherDataUpdateEvent();
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading from Cozy: {e.Message}");
            isCozyConnected = false;
        }
    }

    /// <summary>
    /// Reads current weather from Cozy's weather module
    /// </summary>
    private void ReadCurrentWeather()
    {
        if (CozyWeather.instance?.weatherModule?.ecosystem == null) return;

        try
        {
            // Get current weather profile
            currentWeatherProfile = CozyWeather.instance.weatherModule.ecosystem.currentWeather;

            if (currentWeatherProfile != null)
            {
                // Try to get the weather profile name
                var nameProperty = currentWeatherProfile.GetType().GetProperty("name");
                if (nameProperty != null)
                {
                    currentWeatherName = nameProperty.GetValue(currentWeatherProfile) as string ?? "Unknown";
                }
                else
                {
                    currentWeatherName = currentWeatherProfile.GetType().Name;
                }
            }
            else
            {
                currentWeatherName = "Clear";
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading weather: {e.Message}");
            currentWeatherName = "Error";
        }
    }

    /// <summary>
    /// Reads current temperature from Cozy's climate module
    /// </summary>
    private void ReadCurrentTemperature()
    {
        try
        {
            // Try to get temperature from climate module
            if (CozyWeather.instance?.climateModule != null)
            {
                var climateModule = CozyWeather.instance.climateModule;

                // Try different possible property names for temperature
                float? temp = GetFloatProperty(climateModule, "currentTemperature") ??
                             GetFloatProperty(climateModule, "temperature") ??
                             GetFloatProperty(climateModule, "globalTemperature");

                if (temp.HasValue)
                {
                    currentTemperature = temp.Value;
                    return;
                }
            }

            // Fallback: try to get temperature from weather sphere directly
            if (CozyWeather.instance != null)
            {
                float? temp = GetFloatProperty(CozyWeather.instance, "currentTemperature") ??
                             GetFloatProperty(CozyWeather.instance, "temperature");

                if (temp.HasValue)
                {
                    currentTemperature = temp.Value;
                    return;
                }
            }

            // If no temperature source found, keep previous value
            DebugLog("No temperature source found in Cozy");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading temperature: {e.Message}");
        }
    }

    /// <summary>
    /// Reads current precipitation from Cozy's climate module
    /// </summary>
    private void ReadCurrentPrecipitation()
    {
        try
        {
            if (CozyWeather.instance?.climateModule != null)
            {
                var climateModule = CozyWeather.instance.climateModule;

                float? precip = GetFloatProperty(climateModule, "currentPrecipitation") ??
                               GetFloatProperty(climateModule, "precipitation") ??
                               GetFloatProperty(climateModule, "globalPrecipitation");

                if (precip.HasValue)
                {
                    currentPrecipitation = precip.Value;
                }
            }
        }
        catch (System.Exception e)
        {
            DebugLog($"Error reading precipitation: {e.Message}");
        }
    }

    #endregion

    #region Change Detection and Events

    /// <summary>
    /// Checks for weather changes and fires events
    /// </summary>
    private void CheckForWeatherChanges()
    {
        if (!trackWeatherChanges) return;

        if (currentWeatherName != previousWeatherName)
        {
            DebugLog($"Weather changed: {previousWeatherName} → {currentWeatherName}");
            OnWeatherChanged?.Invoke(currentWeatherName);
            previousWeatherName = currentWeatherName;
        }
    }

    /// <summary>
    /// Checks for temperature changes and fires events
    /// </summary>
    private void CheckForTemperatureChanges()
    {
        if (!trackTemperatureChanges) return;

        float tempDifference = Mathf.Abs(currentTemperature - previousTemperature);
        if (tempDifference >= significantTempChange)
        {
            DebugLog($"Temperature changed: {previousTemperature:F1}°C → {currentTemperature:F1}°C");
            OnTemperatureChanged?.Invoke(currentTemperature);
            previousTemperature = currentTemperature;
        }
    }

    /// <summary>
    /// Fires comprehensive weather data update event
    /// </summary>
    private void FireWeatherDataUpdateEvent()
    {
        var weatherData = new CozyWeatherData
        {
            weatherName = currentWeatherName,
            weatherProfile = currentWeatherProfile,
            temperature = currentTemperature,
            precipitation = currentPrecipitation,
            isConnected = isCozyConnected,
            timestamp = Time.time
        };

        OnCozyWeatherUpdated?.Invoke(weatherData);
    }

    #endregion

    #region Public API for Game Logic

    /// <summary>
    /// Gets current weather name from Cozy
    /// </summary>
    public string GetCurrentWeatherName() => currentWeatherName;

    /// <summary>
    /// Gets current temperature from Cozy (°C)
    /// </summary>
    public float GetCurrentTemperature() => currentTemperature;

    /// <summary>
    /// Gets current precipitation level from Cozy
    /// </summary>
    public float GetCurrentPrecipitation() => currentPrecipitation;

    /// <summary>
    /// Gets current weather profile object from Cozy
    /// </summary>
    public object GetCurrentWeatherProfile() => currentWeatherProfile;

    /// <summary>
    /// Checks if currently connected to Cozy
    /// </summary>
    public bool IsCozyConnected() => isCozyConnected;

    /// <summary>
    /// Gets complete weather data structure
    /// </summary>
    public CozyWeatherData GetCurrentWeatherData()
    {
        return new CozyWeatherData
        {
            weatherName = currentWeatherName,
            weatherProfile = currentWeatherProfile,
            temperature = currentTemperature,
            precipitation = currentPrecipitation,
            isConnected = isCozyConnected,
            timestamp = Time.time
        };
    }

    /// <summary>
    /// Checks if specific weather is currently active (by name)
    /// </summary>
    public bool IsWeatherActive(string weatherName)
    {
        return currentWeatherName.Contains(weatherName, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if it's currently raining/snowing (precipitation > 0)
    /// </summary>
    public bool IsPrecipitating() => currentPrecipitation > 0.1f;

    /// <summary>
    /// Gets temperature in Fahrenheit for UI display
    /// </summary>
    public float GetTemperatureFahrenheit() => (currentTemperature * 9f / 5f) + 32f;

    #endregion

    #region Save/Load Interface

    /// <summary>
    /// Gets data that should be saved (current Cozy state)
    /// </summary>
    public CozyWeatherSaveData GetSaveData()
    {
        return new CozyWeatherSaveData
        {
            weatherName = currentWeatherName,
            temperature = currentTemperature,
            precipitation = currentPrecipitation,
            saveTimestamp = System.DateTime.Now,
            cozyConnected = isCozyConnected
        };
    }

    /// <summary>
    /// Restores Cozy state from saved data (attempts to set Cozy's state)
    /// </summary>
    public void RestoreFromSaveData(CozyWeatherSaveData saveData)
    {
        if (saveData == null || !restoreCozyState)
        {
            DebugLog("Cannot restore - invalid data or restoration disabled");
            return;
        }

        DebugLog($"Restoring Cozy state - Weather: {saveData.weatherName}, Temp: {saveData.temperature:F1}°C");

        // Attempt to restore weather if possible
        if (!string.IsNullOrEmpty(saveData.weatherName) && saveData.weatherName != "Clear")
        {
            SetCozyWeatherByName(saveData.weatherName);
        }

        // Attempt to restore temperature if possible (this might not be supported by Cozy)
        if (saveData.temperature != 0f)
        {
            SetCozyTemperature(saveData.temperature);
        }

        // Force immediate read to sync our state
        ReadInitialCozyState();
    }

    /// <summary>
    /// Attempts to set Cozy weather by profile name (for save restoration)
    /// </summary>
    private void SetCozyWeatherByName(string weatherName)
    {
        try
        {
            if (CozyWeather.instance?.weatherModule?.ecosystem == null) return;

            // This is tricky since we need to find the weather profile by name
            // We would need access to Cozy's weather profile list to do this properly
            DebugLog($"Attempting to restore weather: {weatherName} (this may not be fully supported)");

            // In a full implementation, you would:
            // 1. Get list of available weather profiles from Cozy
            // 2. Find profile with matching name
            // 3. Set that profile as current weather
        }
        catch (System.Exception e)
        {
            DebugLog($"Error setting Cozy weather: {e.Message}");
        }
    }

    /// <summary>
    /// Attempts to set Cozy temperature (may not be supported)
    /// </summary>
    private void SetCozyTemperature(float temperature)
    {
        try
        {
            // Climate module might not allow direct temperature setting
            // since it's usually controlled by the climate profile and time of year
            DebugLog($"Attempting to set temperature: {temperature:F1}°C (may not be supported by Cozy)");
        }
        catch (System.Exception e)
        {
            DebugLog($"Error setting Cozy temperature: {e.Message}");
        }
    }

    #endregion

    #region Manual Controls (For Testing)

    /// <summary>
    /// Forces immediate read from Cozy
    /// </summary>
    [Button("Force Read From Cozy")]
    public void ForceReadFromCozy()
    {
        if (isCozyConnected)
        {
            lastCozyReadTime = 0f; // Force immediate read
            ReadFromCozy();
            DebugLog("Forced read from Cozy completed");
        }
        else
        {
            DebugLog("Not connected to Cozy - cannot force read");
        }
    }

    /// <summary>
    /// Reconnects to Cozy modules
    /// </summary>
    [Button("Reconnect to Cozy")]
    public void ReconnectToCozy()
    {
        ConnectToCozy();
        if (isCozyConnected)
        {
            ReadInitialCozyState();
            DebugLog("Reconnected to Cozy");
        }
    }

    /// <summary>
    /// Toggles Cozy reading on/off
    /// </summary>
    [Button("Toggle Cozy Reading")]
    public void ToggleCozyReading()
    {
        enableCozyReading = !enableCozyReading;
        DebugLog($"Cozy reading {(enableCozyReading ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Tests save/restore functionality
    /// </summary>
    [Button("Test Save/Restore")]
    public void TestSaveRestore()
    {
        var saveData = GetSaveData();
        DebugLog($"Current state: {saveData.GetDebugInfo()}");

        // Test restoration (this won't change much since we're restoring current state)
        RestoreFromSaveData(saveData);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Attempts to get a float property from an object using reflection
    /// </summary>
    private float? GetFloatProperty(object obj, string propertyName)
    {
        if (obj == null) return null;

        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property != null && (property.PropertyType == typeof(float) || property.PropertyType == typeof(double)))
            {
                return (float)property.GetValue(obj);
            }

            var field = obj.GetType().GetField(propertyName);
            if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(double)))
            {
                return (float)field.GetValue(obj);
            }
        }
        catch
        {
            // Property/field doesn't exist or isn't accessible
        }

        return null;
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[WeatherManager] {message}");
        }
    }

    private void OnValidate()
    {
        cozyReadInterval = Mathf.Max(0.01f, cozyReadInterval);
        significantTempChange = Mathf.Max(0.1f, significantTempChange);
    }
}

/// <summary>
/// Data structure for current weather information read from Cozy
/// </summary>
[System.Serializable]
public class CozyWeatherData
{
    public string weatherName;
    public object weatherProfile;
    public float temperature;
    public float precipitation;
    public bool isConnected;
    public float timestamp;

    public string GetDebugInfo()
    {
        return $"Weather: {weatherName}, Temp: {temperature:F1}°C, Precip: {precipitation:F2}, Connected: {isConnected}";
    }
}

/// <summary>
/// Simplified save data structure for Cozy weather state
/// </summary>
[System.Serializable]
public class CozyWeatherSaveData
{
    public string weatherName = "Clear";
    public float temperature = 20f;
    public float precipitation = 0f;
    public System.DateTime saveTimestamp;
    public bool cozyConnected = false;

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(weatherName) &&
               temperature > -100f && temperature < 100f &&
               precipitation >= 0f;
    }

    public string GetDebugInfo()
    {
        return $"Weather: {weatherName}, Temp: {temperature:F1}°C, Precip: {precipitation:F2}, Saved: {saveTimestamp:yyyy-MM-dd HH:mm}";
    }
}