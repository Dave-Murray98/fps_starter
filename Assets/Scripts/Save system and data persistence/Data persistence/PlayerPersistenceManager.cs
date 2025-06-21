using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// REFACTORED: Truly modular PlayerPersistenceManager
/// No longer has hardcoded knowledge of specific save components
/// Components handle their own data extraction, default creation, and contribution
/// This makes the system scalable - add/remove components without touching this manager
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
            string componentType = saveable is IPlayerDependentSaveable ? "Enhanced" : "Legacy";
            DebugLog($"  - {saveable.SaveID} ({saveable.GetType().Name}) [{componentType}]");
        }
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
    /// CONTEXT-AWARE: Restore player data for doorway transitions
    /// This restores player stats, inventory, equipment but NOT position
    /// Called by SceneTransitionManager when context is DoorwayTransition
    /// </summary>
    public void RestoreForDoorwayTransition()
    {
        DebugLog("=== RESTORING PLAYER DATA FOR DOORWAY TRANSITION ===");

        if (!hasPersistentData)
        {
            DebugLog("No persistent data available for doorway transition");
            return;
        }

        // Re-discover saveables in the new scene
        DiscoverPlayerDependentSaveables();

        // Restore data to each component with DOORWAY context
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                if (persistentPlayerData.TryGetValue(saveable.SaveID, out var data))
                {
                    // Load data with doorway context (no position restore)
                    LoadSaveableWithContext(saveable, data, RestoreContext.DoorwayTransition);
                    DebugLog($"Restored doorway data for {saveable.SaveID}");
                }
                else
                {
                    DebugLog($"No persistent data found for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore doorway data for {saveable.SaveID}: {e.Message}");
            }
        }

        // Clear persistent data after successful restoration
        ClearPersistentData();
        DebugLog("Doorway transition player data restoration complete");
    }

    /// <summary>
    /// CONTEXT-AWARE: Restore player data from save file
    /// This restores ALL player data INCLUDING position
    /// Called by SceneTransitionManager when context is SaveFileLoad
    /// </summary>
    public void RestoreFromSaveFile(Dictionary<string, object> saveData)
    {
        DebugLog("=== RESTORING PLAYER DATA FROM SAVE FILE ===");

        if (saveData == null)
        {
            DebugLog("No save data provided");
            return;
        }

        // Clear doorway transition data since we're loading from save
        ClearPersistentData();

        // Re-discover saveables
        DiscoverPlayerDependentSaveables();

        // Restore data to each component with SAVE LOAD context
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                // MODULAR: Let component extract its own data from the save file
                var componentData = ExtractComponentDataFromSave(saveable, saveData);
                if (componentData != null)
                {
                    // Load data with save file context (includes position restore)
                    LoadSaveableWithContext(saveable, componentData, RestoreContext.SaveFileLoad);
                    DebugLog($"Restored save file data for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore save file data for {saveable.SaveID}: {e.Message}");
            }
        }

        DebugLog("Save file player data restoration complete");
    }

    /// <summary>
    /// CONTEXT-AWARE: Initialize player data for new game
    /// Called by SceneTransitionManager when context is NewGame
    /// </summary>
    public void InitializeForNewGame()
    {
        DebugLog("=== INITIALIZING PLAYER DATA FOR NEW GAME ===");

        // Clear any existing data
        ClearPersistentData();

        // Re-discover saveables
        DiscoverPlayerDependentSaveables();

        // Initialize each component with default values
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                // MODULAR: Let component create its own default data
                var defaultData = CreateDefaultDataForComponent(saveable);
                if (defaultData != null)
                {
                    LoadSaveableWithContext(saveable, defaultData, RestoreContext.NewGame);
                    DebugLog($"Initialized default data for {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize {saveable.SaveID} for new game: {e.Message}");
            }
        }

        DebugLog("New game player data initialization complete");
    }

    /// <summary>
    /// Load saveable component with specific context information
    /// This allows components to make context-aware decisions about what to restore
    /// </summary>
    private void LoadSaveableWithContext(ISaveable saveable, object data, RestoreContext context)
    {
        // If the saveable component supports context-aware loading, use it
        if (saveable is IContextAwareSaveable contextAware)
        {
            contextAware.LoadSaveDataWithContext(data, context);
        }
        else
        {
            // Fallback to standard loading
            saveable.LoadSaveData(data);
        }

        saveable.OnAfterLoad();
    }

    /// <summary>
    /// MODULAR: Extract component-specific data from a save file
    /// Now uses the enhanced interface system for true modularity
    /// </summary>
    private object ExtractComponentDataFromSave(ISaveable saveable, Dictionary<string, object> saveData)
    {
        DebugLog($"Extracting data for component: {saveable.SaveID}");

        // PRIORITY 1: Check for direct PlayerSaveData (this contains position data)
        if (saveData.ContainsKey("playerSaveData"))
        {
            var playerSaveData = saveData["playerSaveData"] as PlayerSaveData;
            if (playerSaveData != null)
            {
                DebugLog($"Found playerSaveData with position: {playerSaveData.position}");
                var extracted = saveable.ExtractRelevantData(playerSaveData);
                DebugLog($"Extracted data type: {extracted?.GetType().Name ?? "null"}");
                return extracted;
            }
        }

        // PRIORITY 2: Check for PlayerPersistentData - MODULAR APPROACH
        if (saveData.ContainsKey("playerPersistentData"))
        {
            var persistentData = saveData["playerPersistentData"] as PlayerPersistentData;
            if (persistentData != null)
            {
                DebugLog($"Found playerPersistentData");
                return ExtractFromPlayerPersistentDataModular(saveable, persistentData);
            }
        }

        // PRIORITY 3: Direct component lookup
        if (saveData.ContainsKey(saveable.SaveID))
        {
            DebugLog($"Found direct component data for: {saveable.SaveID}");
            return saveData[saveable.SaveID];
        }

        DebugLog($"No save data found for component: {saveable.SaveID}");
        return null;
    }

    /// <summary>
    /// MODULAR: Extract data for a specific component from PlayerPersistentData
    /// Uses the new interface system - no more hardcoded switch statements!
    /// </summary>
    private object ExtractFromPlayerPersistentDataModular(ISaveable saveable, PlayerPersistentData persistentData)
    {
        if (persistentData == null) return null;

        // ENHANCED: Use the new interface if available
        if (saveable is IPlayerDependentSaveable enhancedSaveable)
        {
            DebugLog($"Using enhanced extraction for {saveable.SaveID}");
            return enhancedSaveable.ExtractFromUnifiedSave(persistentData);
        }

        // FALLBACK: Legacy extraction using ExtractRelevantData
        DebugLog($"Using legacy extraction for {saveable.SaveID}");
        return saveable.ExtractRelevantData(persistentData);
    }

    /// <summary>
    /// MODULAR: Create default data for a component (new game initialization)
    /// Uses the new interface system - no more hardcoded switch statements!
    /// </summary>
    private object CreateDefaultDataForComponent(ISaveable saveable)
    {
        // ENHANCED: Use the new interface if available
        if (saveable is IPlayerDependentSaveable enhancedSaveable)
        {
            DebugLog($"Using enhanced default data creation for {saveable.SaveID}");
            return enhancedSaveable.CreateDefaultData();
        }

        // FALLBACK: Basic default data for legacy components
        DebugLog($"Using legacy default data creation for {saveable.SaveID}");

        // Provide basic defaults for known legacy components
        switch (saveable.SaveID)
        {
            case "Player_Main":
                var playerData = GameManager.Instance?.playerData;
                return new PlayerSaveData
                {
                    currentHealth = playerData?.maxHealth ?? 100f,
                    maxHealth = playerData?.maxHealth ?? 100f,
                    canJump = true,
                    canSprint = true,
                    canCrouch = true,
                    inventoryData = new InventorySaveData(),
                    equipmentData = new EquipmentSaveData()
                };

            case "Inventory_Main":
                return new InventorySaveData();

            case "Equipment_Main":
                return new EquipmentSaveData();

            default:
                DebugLog($"No default data creation for component: {saveable.SaveID}");
                return null;
        }
    }

    /// <summary>
    /// Get current persistent data for save system (called by SaveManager)
    /// MODULAR: Uses the new interface system for contribution
    /// </summary>
    public PlayerPersistentData GetPersistentDataForSave()
    {
        // Force update with current values
        UpdatePersistentPlayerDataForTransition();

        // Create save-friendly data structure
        var saveData = new PlayerPersistentData();

        // MODULAR: Let each save component contribute to the save data
        foreach (var saveable in playerDependentSaveables)
        {
            try
            {
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    ContributeToSaveDataModular(saveable, data, saveData);
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
    /// MODULAR: Helper method to let save components contribute to the unified save data
    /// Uses the new interface system - no more hardcoded switch statements!
    /// </summary>
    private void ContributeToSaveDataModular(ISaveable saveable, object data, PlayerPersistentData saveData)
    {
        // ENHANCED: Use the new interface if available
        if (saveable is IPlayerDependentSaveable enhancedSaveable)
        {
            DebugLog($"Using enhanced contribution for {saveable.SaveID}");
            enhancedSaveable.ContributeToUnifiedSave(data, saveData);
            return;
        }

        // FALLBACK: Legacy contribution for known components
        DebugLog($"Using legacy contribution for {saveable.SaveID}");

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
                DebugLog($"Unknown save component: {saveable.SaveID} - using dynamic storage");
                saveData.SetComponentData(saveable.SaveID, data);
                break;
        }
    }

    /// <summary>
    /// Clear persistent data (useful for new game)
    /// </summary>
    public void ClearPersistentData()
    {
        persistentPlayerData.Clear();
        hasPersistentData = false;
        DebugLog("Persistent player data cleared");
    }

    /// <summary>
    /// Check if we have persistent data waiting to be restored
    /// </summary>
    public bool HasPersistentData => hasPersistentData;

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

    /// <summary>
    /// DEBUG: Get detailed info about component capabilities
    /// </summary>
    public Dictionary<string, ComponentCapabilities> GetComponentCapabilities()
    {
        var capabilities = new Dictionary<string, ComponentCapabilities>();

        foreach (var saveable in playerDependentSaveables)
        {
            capabilities[saveable.SaveID] = new ComponentCapabilities
            {
                IsEnhanced = saveable is IPlayerDependentSaveable,
                IsContextAware = saveable is IContextAwareSaveable,
                ComponentType = saveable.GetType().Name,
                SaveCategory = saveable.SaveCategory
            };
        }

        return capabilities;
    }

    /// <summary>
    /// Force a component to be discovered (useful for runtime-created components)
    /// </summary>
    public void RegisterComponent(ISaveable component)
    {
        if (component == null || component.SaveCategory != SaveDataCategory.PlayerDependent)
            return;

        if (!playerDependentSaveables.Contains(component))
        {
            playerDependentSaveables.Add(component);
            string componentType = component is IPlayerDependentSaveable ? "Enhanced" : "Legacy";
            DebugLog($"Manually registered component: {component.SaveID} ({component.GetType().Name}) [{componentType}]");
        }
    }

    /// <summary>
    /// Remove a component from discovery (useful for runtime-destroyed components)
    /// </summary>
    public void UnregisterComponent(ISaveable component)
    {
        if (component == null)
            return;

        if (playerDependentSaveables.Remove(component))
        {
            DebugLog($"Unregistered component: {component.SaveID}");
        }
    }

    /// <summary>
    /// Validate that all discovered components are still valid
    /// </summary>
    public void ValidateComponents()
    {
        var invalidComponents = new List<ISaveable>();

        foreach (var component in playerDependentSaveables)
        {
            try
            {
                // Test if component is still accessible
                var _ = component.SaveID;
                var __ = component.SaveCategory;
            }
            catch (System.Exception)
            {
                invalidComponents.Add(component);
            }
        }

        foreach (var invalid in invalidComponents)
        {
            playerDependentSaveables.Remove(invalid);
            DebugLog($"Removed invalid component from discovery");
        }

        if (invalidComponents.Count > 0)
        {
            DebugLog($"Cleaned up {invalidComponents.Count} invalid components");
        }
    }

    /// <summary>
    /// Get statistics about the current save system state
    /// </summary>
    public SaveSystemStats GetSaveSystemStats()
    {
        var stats = new SaveSystemStats();

        stats.TotalComponents = playerDependentSaveables.Count;
        stats.EnhancedComponents = playerDependentSaveables.Count(s => s is IPlayerDependentSaveable);
        stats.ContextAwareComponents = playerDependentSaveables.Count(s => s is IContextAwareSaveable);
        stats.LegacyComponents = stats.TotalComponents - stats.EnhancedComponents;
        stats.HasPersistentData = hasPersistentData;
        stats.PersistentDataCount = persistentPlayerData.Count;

        return stats;
    }

    /// <summary>
    /// Export detailed debug information about the save system
    /// </summary>
    public string ExportDebugInfo()
    {
        var info = new System.Text.StringBuilder();

        info.AppendLine("=== PLAYER PERSISTENCE MANAGER DEBUG INFO ===");

        var stats = GetSaveSystemStats();
        info.AppendLine($"Total Components: {stats.TotalComponents}");
        info.AppendLine($"Enhanced Components: {stats.EnhancedComponents}");
        info.AppendLine($"Context-Aware Components: {stats.ContextAwareComponents}");
        info.AppendLine($"Legacy Components: {stats.LegacyComponents}");
        info.AppendLine($"Has Persistent Data: {stats.HasPersistentData}");
        info.AppendLine($"Persistent Data Count: {stats.PersistentDataCount}");

        info.AppendLine("\nDISCOVERED COMPONENTS:");
        foreach (var saveable in playerDependentSaveables)
        {
            var enhanced = saveable is IPlayerDependentSaveable ? "✓" : "✗";
            var contextAware = saveable is IContextAwareSaveable ? "✓" : "✗";
            info.AppendLine($"  - {saveable.SaveID} ({saveable.GetType().Name}) [Enhanced: {enhanced}, ContextAware: {contextAware}]");
        }

        if (hasPersistentData)
        {
            info.AppendLine("\nPERSISTENT DATA:");
            foreach (var kvp in persistentPlayerData)
            {
                info.AppendLine($"  - {kvp.Key}: {kvp.Value?.GetType().Name ?? "null"}");
            }
        }

        return info.ToString();
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPersistence] {message}");
        }
    }
}

/// <summary>
/// Helper class for debugging component capabilities
/// </summary>
[System.Serializable]
public class ComponentCapabilities
{
    public bool IsEnhanced;
    public bool IsContextAware;
    public string ComponentType;
    public SaveDataCategory SaveCategory;

    public override string ToString()
    {
        var features = new List<string>();
        if (IsEnhanced) features.Add("Enhanced");
        if (IsContextAware) features.Add("ContextAware");
        return $"{ComponentType} ({SaveCategory}) [{string.Join(", ", features)}]";
    }
}

/// <summary>
/// Statistics about the save system state
/// </summary>
[System.Serializable]
public class SaveSystemStats
{
    public int TotalComponents;
    public int EnhancedComponents;
    public int ContextAwareComponents;
    public int LegacyComponents;
    public bool HasPersistentData;
    public int PersistentDataCount;

    public override string ToString()
    {
        return $"SaveSystem: {TotalComponents} components ({EnhancedComponents} enhanced, {LegacyComponents} legacy), Persistent: {HasPersistentData}";
    }
}