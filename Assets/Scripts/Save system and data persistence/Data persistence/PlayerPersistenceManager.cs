using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// REFACTORED: Modular PlayerPersistenceManager that discovers and manages all player-dependent save components
/// No direct references to specific managers - uses ISaveable discovery pattern
/// Highly scalable and reusable across projects
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Discovered save components (no direct references!)
    private List<ISaveable> playerDependentSaveables = new List<ISaveable>();

    // Persistent data storage for scene transitions
    private Dictionary<string, object> persistentPlayerData = new Dictionary<string, object>();

    // State management
    private bool hasPersistentData = false;
    private bool saveManagerIsHandlingRestore = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DebugLog("PlayerPersistenceManager initialized with modular architecture");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        DiscoverPlayerDependentSaveables();
    }

    /// <summary>
    /// Discover all player-dependent save components in the scene
    /// This is what makes the system modular - no hardcoded references!
    /// </summary>
    private void DiscoverPlayerDependentSaveables()
    {
        // Find all ISaveable components that are player-dependent
        var allSaveables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.PlayerDependent)
            .ToList();

        playerDependentSaveables = allSaveables;

        DebugLog($"Discovered {playerDependentSaveables.Count} player-dependent save components:");
        foreach (var saveable in playerDependentSaveables)
        {
            DebugLog($"  - {saveable.SaveID} ({saveable.GetType().Name})");
        }
    }

    /// <summary>
    /// Called when scene loads - restore persistent data if we have any
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (hasPersistentData && !saveManagerIsHandlingRestore)
        {
            DebugLog("Scene loaded - restoring persistent player data via doorway transition");
            StartCoroutine(RestorePlayerDataAfterSceneLoad());
        }
    }

    /// <summary>
    /// Restore persistent data after scene load (doorway transitions)
    /// </summary>
    private System.Collections.IEnumerator RestorePlayerDataAfterSceneLoad()
    {
        // Wait for scene to fully initialize
        yield return new WaitForSecondsRealtime(0.1f);

        // Re-discover saveables in the new scene
        DiscoverPlayerDependentSaveables();

        // Restore data to each component
        RestoreDataToSaveComponents();

        // Clear persistent data after successful restoration
        ClearPersistentData();
    }

    /// <summary>
    /// MODULAR: Update persistent data before scene transition (called by doorways)
    /// This method discovers and calls all player-dependent save components automatically
    /// </summary>
    public void UpdatePersistentPlayerDataForTransition()
    {
        DebugLog("=== PREPARING PLAYER DATA FOR TRANSITION ===");

        // Re-discover saveables in case new ones were added
        DiscoverPlayerDependentSaveables();

        // Clear previous data
        persistentPlayerData.Clear();

        // Let each save component prepare its data
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                // Call preparation hook
                saveable.OnBeforeSave();

                // Get the component's data
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    persistentPlayerData[saveable.SaveID] = data;
                    DebugLog($"Prepared data for {saveable.SaveID}: {data.GetType().Name}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to prepare data for {saveable.SaveID}: {e.Message}");
            }
        }

        hasPersistentData = true;
        DebugLog($"Preparation complete - stored data for {persistentPlayerData.Count} components");
    }

    /// <summary>
    /// MODULAR: Restore data to all discovered save components
    /// </summary>
    private void RestoreDataToSaveComponents()
    {
        DebugLog("=== RESTORING PLAYER DATA AFTER TRANSITION ===");

        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                // Check if we have data for this component
                if (persistentPlayerData.TryGetValue(saveable.SaveID, out var data))
                {
                    // Let the component handle its own data restoration
                    saveable.LoadSaveData(data);
                    saveable.OnAfterLoad();

                    DebugLog($"Restored data for {saveable.SaveID}");
                }
                else
                {
                    DebugLog($"No persistent data found for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore data for {saveable.SaveID}: {e.Message}");
            }
        }

        DebugLog("Player data restoration complete");
    }

    /// <summary>
    /// Get current persistent data for save system (called by SaveManager)
    /// </summary>
    public PlayerPersistentData GetPersistentDataForSave()
    {
        // Force update with current values
        UpdatePersistentPlayerDataForTransition();

        // Create save-friendly data structure
        var saveData = new PlayerPersistentData();

        // Let each save component contribute to the save data
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    // Components will populate the save data structure
                    // This is handled by each component's GetDataToSave method
                    ContributeToSaveData(saveable, data, saveData);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to get save data from {saveable.SaveID}: {e.Message}");
            }
        }

        return saveData;
    }

    /// <summary>
    /// Helper method to let save components contribute to the unified save data
    /// </summary>
    private void ContributeToSaveData(ISaveable saveable, object data, PlayerPersistentData saveData)
    {
        // Each save component knows how to contribute to the unified structure
        switch (saveable.SaveID)
        {
            case "Player_Main":
                if (data is PlayerSaveData playerData)
                {
                    saveData.currentHealth = playerData.currentHealth;
                    saveData.canJump = playerData.canJump;
                    saveData.canSprint = playerData.canSprint;
                    saveData.canCrouch = playerData.canCrouch;
                }
                break;

            case "Inventory_Main":
                if (data is InventorySaveData inventoryData)
                {
                    saveData.inventoryData = inventoryData;
                }
                break;

            case "Equipment_Main":
                if (data is EquipmentSaveData equipmentData)
                {
                    saveData.equipmentData = equipmentData;
                }
                break;

            default:
                DebugLog($"Unknown save component: {saveable.SaveID} - data not included in unified save");
                break;
        }
    }

    /// <summary>
    /// Load persistent data from save system (called by SaveManager)
    /// </summary>
    public void LoadPersistentDataFromSave(PlayerPersistentData saveData)
    {
        if (saveData == null) return;

        DebugLog("Loading persistent data from save file");

        // Clear doorway transition data since we're loading from save
        ClearPersistentData();
        saveManagerIsHandlingRestore = true;

        // Re-discover saveables
        DiscoverPlayerDependentSaveables();

        // Distribute data to appropriate save components
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                object componentData = ExtractDataForComponent(saveable, saveData);
                if (componentData != null)
                {
                    saveable.LoadSaveData(componentData);
                    saveable.OnAfterLoad();
                    DebugLog($"Loaded save data for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load save data for {saveable.SaveID}: {e.Message}");
            }
        }

        DebugLog("Save data loading complete");
    }

    /// <summary>
    /// Extract appropriate data for each save component from unified save data
    /// </summary>
    private object ExtractDataForComponent(ISaveable saveable, PlayerPersistentData saveData)
    {
        switch (saveable.SaveID)
        {
            case "Player_Main":
                return new PlayerSaveData
                {
                    currentHealth = saveData.currentHealth,
                    canJump = saveData.canJump,
                    canSprint = saveData.canSprint,
                    canCrouch = saveData.canCrouch
                };

            case "Inventory_Main":
                return saveData.inventoryData;

            case "Equipment_Main":
                return saveData.equipmentData;

            default:
                DebugLog($"No data extraction defined for {saveable.SaveID}");
                return null;
        }
    }

    /// <summary>
    /// Clear persistent data (useful for new game)
    /// </summary>
    public void ClearPersistentData()
    {
        persistentPlayerData.Clear();
        hasPersistentData = false;
        saveManagerIsHandlingRestore = false;
        DebugLog("Persistent player data cleared");
    }

    /// <summary>
    /// Called when SaveManager finishes loading
    /// </summary>
    public void OnSaveLoadComplete()
    {
        saveManagerIsHandlingRestore = false;
        DebugLog("Save load complete - doorway transitions re-enabled");
    }

    /// <summary>
    /// Check if we have persistent data waiting to be restored
    /// </summary>
    public bool HasPersistentData => hasPersistentData && !saveManagerIsHandlingRestore;

    /// <summary>
    /// Get current player data snapshot (useful for debugging)
    /// </summary>
    public PlayerPersistentData GetCurrentSnapshot()
    {
        return GetPersistentDataForSave();
    }

    /// <summary>
    /// Manually refresh discovered saveables (useful when components are added/removed)
    /// </summary>
    public void RefreshDiscoveredSaveables()
    {
        DiscoverPlayerDependentSaveables();
        DebugLog("Manually refreshed discovered saveables");
    }

    /// <summary>
    /// DEBUG: Get list of currently discovered saveables
    /// </summary>
    public List<string> GetDiscoveredSaveableIDs()
    {
        return playerDependentSaveables.Select(s => $"{s.SaveID} ({s.GetType().Name})").ToList();
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPersistence] {message}");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}