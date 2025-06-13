using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// Updated PlayerPersistenceManager that works with the new data-driven inventory system
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Persistent Player Data")]
    [SerializeField] private PlayerPersistentData persistentData;
    [SerializeField] private bool showDebugLogs = true;

    // Flag to track if we have persistent data to restore
    private bool hasPersistentData = false;

    // Flag to prevent restoration when SaveManager is handling it
    private bool saveManagerIsHandlingRestore = false;

    // Reference to persistent inventory
    private PersistentInventoryManager persistentInventory;
    private InventorySaveComponent inventorySaveComponent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            persistentData = new PlayerPersistentData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        // Get references to inventory systems
        RefreshInventoryReferences();
    }

    private void RefreshInventoryReferences()
    {
        persistentInventory = PersistentInventoryManager.Instance;
        if (persistentInventory == null)
        {
            persistentInventory = FindFirstObjectByType<PersistentInventoryManager>();
        }

        inventorySaveComponent = FindFirstObjectByType<InventorySaveComponent>();

        DebugLog($"Inventory references refreshed - PersistentInventory: {persistentInventory != null}, SaveComponent: {inventorySaveComponent != null}");
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (hasPersistentData && !saveManagerIsHandlingRestore)
        {
            DebugLog("PlayerPersistenceManager.OnSceneLoaded() called, triggering restoration");
            StartCoroutine(RestorePlayerDataCoroutine());
        }
    }

    private System.Collections.IEnumerator RestorePlayerDataCoroutine()
    {
        DebugLog("PlayerPersistenceManager.RestorePlayerDataCoroutine() called");

        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        // Double-check that SaveManager isn't handling this
        if (!saveManagerIsHandlingRestore)
        {
            RestorePlayerDataAfterTransition();
        }
        else
        {
            DebugLog("Skipping doorway data restoration - SaveManager is handling it");
        }
    }

    /// <summary>
    /// Only called by RestorePlayerDataCoroutine() after a scene transition VIA DOORWAY
    /// </summary>
    private void RestorePlayerDataAfterTransition()
    {
        if (!hasPersistentData || saveManagerIsHandlingRestore) return;

        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager != null && playerController != null)
        {
            // Restore basic player data
            playerManager.currentHealth = persistentData.currentHealth;
            playerController.canJump = persistentData.canJump;
            playerController.canSprint = persistentData.canSprint;
            playerController.canCrouch = persistentData.canCrouch;

            // Restore inventory data using the new system
            RestoreInventoryData();

            // Trigger UI updates
            if (GameManager.Instance?.playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(persistentData.currentHealth, GameManager.Instance.playerData.maxHealth);
            }

            DebugLog($"Player data restored after doorway transition: Health={persistentData.currentHealth}, Inventory={persistentData.inventoryData?.ItemCount ?? 0} items");
        }
    }

    /// <summary>
    /// Restore inventory data using the new persistent inventory system
    /// </summary>
    private void RestoreInventoryData()
    {
        if (persistentData.inventoryData == null || persistentData.inventoryData.ItemCount == 0)
        {
            DebugLog("No inventory data to restore");
            return;
        }

        // Ensure we have inventory system references
        RefreshInventoryReferences();

        if (persistentInventory != null)
        {
            // Load inventory data directly into persistent inventory - no UI required!
            persistentInventory.LoadFromSaveData(persistentData.inventoryData);
            DebugLog($"Restored inventory data via PersistentInventoryManager: {persistentData.inventoryData.ItemCount} items");
        }
        else
        {
            DebugLog("PersistentInventoryManager not found - cannot restore inventory");
        }
    }

    /// <summary>
    /// Get current persistent data for save system
    /// </summary>
    public PlayerPersistentData GetPersistentDataForSave()
    {
        UpdatePersistentPlayerDataForTransition(); // Update with current values
        return new PlayerPersistentData(persistentData); // Return copy
    }

    /// <summary>
    /// Update current player data before scene transition (called by doorway)
    /// </summary>
    public void UpdatePersistentPlayerDataForTransition()
    {
        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager != null && playerController != null)
        {
            // Save basic player data
            persistentData.currentHealth = playerManager.currentHealth;
            persistentData.canJump = playerController.canJump;
            persistentData.canSprint = playerController.canSprint;
            persistentData.canCrouch = playerController.canCrouch;

            // Save inventory data using the new system
            SaveInventoryData();

            hasPersistentData = true;
            DebugLog($"Player data saved for doorway transition: Health={persistentData.currentHealth}, Inventory={persistentData.inventoryData?.ItemCount ?? 0} items");
        }
    }

    /// <summary>
    /// Save inventory data using the new persistent inventory system
    /// </summary>
    private void SaveInventoryData()
    {
        // Ensure we have inventory system references
        RefreshInventoryReferences();

        if (persistentInventory != null)
        {
            // Get inventory data directly from persistent inventory - no UI required!
            persistentData.inventoryData = persistentInventory.GetSaveData();
            DebugLog($"Saved inventory data via PersistentInventoryManager: {persistentData.inventoryData.ItemCount} items");
        }
        else if (inventorySaveComponent != null)
        {
            // Fallback to save component
            persistentData.inventoryData = inventorySaveComponent.GetInventorySaveData();
            DebugLog($"Saved inventory data via InventorySaveComponent: {persistentData.inventoryData.ItemCount} items");
        }
        else
        {
            DebugLog("No inventory system found - inventory not saved");
            persistentData.inventoryData = new InventorySaveData(); // Empty but valid
        }
    }

    /// <summary>
    /// Load persistent data from save system - Clear doorway data when loading from save to prevent conflicts
    /// </summary>
    public void LoadPersistentDataFromSave(PlayerPersistentData saveData)
    {
        if (saveData != null)
        {
            // Clear existing data before loading new
            persistentData = new PlayerPersistentData(saveData);

            // Clear doorway transition data since we're loading from save
            hasPersistentData = false;
            saveManagerIsHandlingRestore = true; // Disables doorway transition data restoration

            DebugLog($"Player persistent data loaded from save - doorway data cleared. Inventory: {persistentData.inventoryData?.ItemCount ?? 0} items");
        }
    }

    /// <summary>
    /// Clear persistent data (useful for new game)
    /// </summary>
    public void ClearPersistentData()
    {
        persistentData = new PlayerPersistentData();
        hasPersistentData = false;
        saveManagerIsHandlingRestore = false;
        DebugLog("Player persistent data cleared");
    }

    /// <summary>
    /// Call this when SaveManager finishes loading to re-enable doorway transitions
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
        UpdatePersistentPlayerDataForTransition(); // Update with current values
        return persistentData;
    }

    /// <summary>
    /// Manually add an item to persistent inventory (useful for pickup systems)
    /// </summary>
    public bool AddItemToPersistentInventory(ItemData itemData, Vector2Int? preferredPosition = null)
    {
        RefreshInventoryReferences();

        if (persistentInventory != null)
        {
            bool success = persistentInventory.AddItem(itemData, preferredPosition);
            if (success)
            {
                DebugLog($"Added item {itemData.itemName} to persistent inventory");
                // Update persistent data immediately
                UpdatePersistentPlayerDataForTransition();
            }
            return success;
        }
        else
        {
            DebugLog("Cannot add item - PersistentInventoryManager not found");
            return false;
        }
    }

    /// <summary>
    /// Check if persistent inventory has space for an item
    /// </summary>
    public bool HasInventorySpaceForItem(ItemData itemData)
    {
        RefreshInventoryReferences();

        if (persistentInventory != null)
        {
            return persistentInventory.HasSpaceForItem(itemData);
        }

        DebugLog("Cannot check inventory space - PersistentInventoryManager not found");
        return false;
    }

    /// <summary>
    /// Get inventory statistics
    /// </summary>
    public (int itemCount, int occupiedCells, int totalCells) GetInventoryStats()
    {
        RefreshInventoryReferences();

        if (persistentInventory != null)
        {
            return persistentInventory.GetInventoryStats();
        }

        return (0, 0, 0);
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

