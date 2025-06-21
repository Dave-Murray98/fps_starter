using UnityEngine;

/// <summary>
/// ENHANCED: InventorySaveComponent now implements IPlayerDependentSaveable for true modularity
/// Handles its own data extraction, default creation, and contribution to unified saves
/// No longer requires hardcoded knowledge in PlayerPersistenceManager
/// </summary>
public class InventorySaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private InventoryManager inventoryManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();

        // Fixed ID for inventory
        saveID = "Inventory_Main";
        autoGenerateID = false;

        // Auto-find references if enabled
        if (autoFindReferences)
        {
            FindInventoryReferences();
        }
    }

    private void Start()
    {
        // Ensure we have inventory reference
        ValidateReferences();
    }

    /// <summary>
    /// Automatically find inventory-related components
    /// </summary>
    private void FindInventoryReferences()
    {
        // Try to find on same GameObject first
        if (inventoryManager == null)
            inventoryManager = GetComponent<InventoryManager>();

        // If not found on same GameObject, get from Instance
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance;

        // If still not found, search scene
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>();

        DebugLog($"Auto-found inventory reference: {inventoryManager != null}");
    }

    /// <summary>
    /// Validate that we have necessary references
    /// </summary>
    private void ValidateReferences()
    {
        if (inventoryManager == null)
        {
            Debug.LogError($"[{name}] InventoryManager reference is missing! Inventory won't be saved/loaded.");
        }
        else
        {
            DebugLog($"InventoryManager reference validated: {inventoryManager.name}");
        }
    }

    /// <summary>
    /// EXTRACT inventory data from InventoryManager (manager doesn't handle its own saving anymore)
    /// </summary>
    public override object GetDataToSave()
    {
        if (inventoryManager == null)
        {
            DebugLog("Cannot save inventory - InventoryManager not found");
            return new InventorySaveData(); // Return empty but valid data
        }

        // Extract data from the manager (manager doesn't do this itself anymore)
        var saveData = ExtractInventoryDataFromManager();

        DebugLog($"Extracted inventory data: {saveData.ItemCount} items in {saveData.gridWidth}x{saveData.gridHeight} grid");
        return saveData;
    }

    /// <summary>
    /// Extract inventory data from the manager (replaces manager's GetSaveData method)
    /// </summary>
    private InventorySaveData ExtractInventoryDataFromManager()
    {
        var saveData = new InventorySaveData(inventoryManager.GridWidth, inventoryManager.GridHeight);

        // Get the next item ID using the public property
        saveData.nextItemId = inventoryManager.NextItemId;

        // Extract all items from the inventory data
        var allItems = inventoryManager.InventoryData.GetAllItems();
        foreach (var item in allItems)
        {
            var itemSaveData = item.ToSaveData();
            if (itemSaveData.IsValid())
            {
                saveData.AddItem(itemSaveData);
            }
        }

        return saveData;
    }

    /// <summary>
    /// FIXED: For PlayerPersistenceManager - extract only inventory data
    /// The issue was checking PlayerSaveData.customStats instead of PlayerPersistentData first
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("InventorySaveComponent: Extracting inventory save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new InventorySaveData();
        }

        // FIXED: Check PlayerPersistentData FIRST since that's where the rebuilt data is stored
        if (saveContainer is PlayerPersistentData persistentData)
        {
            // Extract from dynamic component storage
            var inventoryData = persistentData.GetComponentData<InventorySaveData>(SaveID);
            if (inventoryData != null)
            {
                DebugLog($"Extracted inventory from persistent data dynamic storage: {inventoryData.ItemCount} items in {inventoryData.gridWidth}x{inventoryData.gridHeight} grid");
                return inventoryData;
            }
            else
            {
                DebugLog("No inventory data in persistent data - returning empty inventory");
                return new InventorySaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Check if PlayerSaveData has inventory data in its custom stats
            if (playerSaveData.customStats.TryGetValue("inventoryData", out object invDataObj) &&
                invDataObj is InventorySaveData invData)
            {
                DebugLog($"Extracted inventory data from PlayerSaveData customStats: {invData.ItemCount} items");
                return invData;
            }

            // ALSO check for the component ID in custom stats
            if (playerSaveData.customStats.TryGetValue(SaveID, out object inventoryDataObj) &&
                inventoryDataObj is InventorySaveData inventorySaveData)
            {
                DebugLog($"Extracted inventory data from PlayerSaveData custom stats by SaveID: {inventorySaveData.ItemCount} items in {inventorySaveData.gridWidth}x{inventorySaveData.gridHeight} grid");
                return inventorySaveData;
            }

            DebugLog("No inventory data found in PlayerSaveData - returning empty inventory");
            return new InventorySaveData();
        }
        else if (saveContainer is InventorySaveData inventorySaveData)
        {
            // Direct inventory save data
            DebugLog($"Extracted direct InventorySaveData: {inventorySaveData.ItemCount} items");
            return inventorySaveData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, InventorySaveData, or PlayerPersistentData, got {saveContainer.GetType()}");
            return new InventorySaveData();
        }
    }

    /// <summary>
    /// RESTORE data back to InventoryManager (manager doesn't handle its own loading anymore)
    /// </summary>
    public override void LoadSaveData(object data)
    {
        if (!(data is InventorySaveData inventoryData))
        {
            DebugLog($"Invalid save data type for inventory. Data type: {data?.GetType()}");
            return;
        }

        DebugLog("=== RESTORING INVENTORY DATA TO MANAGER ===");

        // Ensure we have current references (they might have changed after scene load)
        if (autoFindReferences)
        {
            FindInventoryReferences();
        }

        if (inventoryManager == null)
        {
            DebugLog("Cannot load inventory - InventoryManager not found");
            return;
        }

        DebugLog($"Loading inventory: {inventoryData.ItemCount} items in {inventoryData.gridWidth}x{inventoryData.gridHeight} grid");

        try
        {
            // Restore data to the manager (manager doesn't do this itself anymore)
            RestoreInventoryDataToManager(inventoryData);
            DebugLog("Inventory restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load inventory: {e.Message}");
        }
    }

    /// <summary>
    /// Restore inventory data to the manager (replaces manager's LoadFromSaveData method call)
    /// </summary>
    private void RestoreInventoryDataToManager(InventorySaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            DebugLog("Invalid inventory save data - clearing inventory");
            inventoryManager.ClearInventory();
            return;
        }

        // Create new inventory data
        var newInventoryData = new InventoryGridData(saveData.gridWidth, saveData.gridHeight);

        // Restore each item to the new inventory data
        foreach (var itemSaveData in saveData.items)
        {
            var item = InventoryItemData.FromSaveData(itemSaveData);
            if (item != null)
            {
                if (!newInventoryData.PlaceItem(item))
                {
                    Debug.LogWarning($"Failed to restore item {item.ID} at position {item.GridPosition}");
                }
            }
        }

        // Set the complete data to the manager using the helper method
        inventoryManager.SetInventoryData(newInventoryData, saveData.nextItemId);

        DebugLog($"Restored inventory: {newInventoryData.ItemCount} items");
    }

    #region IPlayerDependentSaveable Implementation - NEW MODULAR INTERFACE

    /// <summary>
    /// MODULAR: Extract inventory data from unified save structure
    /// This component knows how to get its data from PlayerPersistentData
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        // Get from dynamic component data storage
        var inventoryData = unifiedData.GetComponentData<InventorySaveData>(SaveID);
        if (inventoryData != null)
        {
            DebugLog($"Extracted inventory from dynamic storage: {inventoryData.ItemCount} items");
            return inventoryData;
        }

        // Return empty inventory if nothing found
        DebugLog("No inventory data found in unified save - returning empty inventory");
        return new InventorySaveData();
    }

    /// <summary>
    /// MODULAR: Create default inventory data for new games
    /// This component knows what its default state should be
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default inventory data for new game");

        // Get grid size from manager if available, otherwise use defaults
        int gridWidth = 10;
        int gridHeight = 10;

        if (inventoryManager != null)
        {
            gridWidth = inventoryManager.GridWidth;
            gridHeight = inventoryManager.GridHeight;
        }

        var defaultData = new InventorySaveData(gridWidth, gridHeight);

        // Start with next item ID of 1
        defaultData.nextItemId = 1;

        DebugLog($"Default inventory data created: {gridWidth}x{gridHeight} grid, empty");
        return defaultData;
    }

    /// <summary>
    /// MODULAR: Contribute inventory data to unified save structure
    /// This component knows how to store its data in PlayerPersistentData
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is InventorySaveData inventoryData && unifiedData != null)
        {
            DebugLog($"Contributing inventory data to unified save structure: {inventoryData.ItemCount} items");

            // Store in dynamic storage
            unifiedData.SetComponentData(SaveID, inventoryData);

            DebugLog($"Inventory data contributed: {inventoryData.ItemCount} items stored in dynamic storage");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected InventorySaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Lifecycle and Utility Methods

    /// <summary>
    /// Called before save operations
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing inventory for save");

        // Refresh references in case they changed
        if (autoFindReferences)
        {
            FindInventoryReferences();
        }
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Inventory load completed");

        // Any cleanup needed after loading
        // Inventory UI will automatically update via events
    }

    /// <summary>
    /// Public method to manually set inventory manager reference
    /// </summary>
    public void SetInventoryManager(InventoryManager manager)
    {
        inventoryManager = manager;
        autoFindReferences = false; // Disable auto-find when manually set
        DebugLog("Inventory manager reference manually set");
    }

    /// <summary>
    /// Get current inventory item count (useful for other systems)
    /// </summary>
    public int GetCurrentItemCount()
    {
        return inventoryManager?.InventoryData?.ItemCount ?? 0;
    }

    /// <summary>
    /// Get current inventory stats (useful for other systems)
    /// </summary>
    public (int itemCount, int occupiedCells, int totalCells) GetInventoryStats()
    {
        if (inventoryManager != null)
        {
            return inventoryManager.GetInventoryStats();
        }
        return (0, 0, 0);
    }

    /// <summary>
    /// Check if inventory manager reference is valid
    /// </summary>
    public bool HasValidReference()
    {
        return inventoryManager != null;
    }

    /// <summary>
    /// Force refresh of inventory manager reference
    /// </summary>
    public void RefreshReference()
    {
        if (autoFindReferences)
        {
            FindInventoryReferences();
            ValidateReferences();
        }
    }

    /// <summary>
    /// Check if inventory has space for an item (useful for other systems)
    /// </summary>
    public bool HasSpaceForItem(ItemData itemData)
    {
        return inventoryManager?.HasSpaceForItem(itemData) ?? false;
    }

    /// <summary>
    /// Get inventory grid dimensions
    /// </summary>
    public (int width, int height) GetGridDimensions()
    {
        if (inventoryManager != null)
        {
            return (inventoryManager.GridWidth, inventoryManager.GridHeight);
        }
        return (0, 0);
    }

    /// <summary>
    /// Check if inventory is empty
    /// </summary>
    public bool IsInventoryEmpty()
    {
        return GetCurrentItemCount() == 0;
    }

    /// <summary>
    /// Get debug information about current inventory state
    /// </summary>
    public string GetInventoryDebugInfo()
    {
        if (inventoryManager == null)
            return "InventoryManager: null";

        var stats = GetInventoryStats();
        return $"Inventory: {stats.itemCount} items, {stats.occupiedCells}/{stats.totalCells} cells used";
    }

    #endregion
}