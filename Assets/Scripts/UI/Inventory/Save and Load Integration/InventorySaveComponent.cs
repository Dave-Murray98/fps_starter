using UnityEngine;

/// <summary>
/// Handles saving and loading of the inventory system including all items, their positions,
/// rotations, and grid state. Integrates with the modular save system and provides
/// context-aware loading (though inventory restoration is the same for all contexts).
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
        saveID = "Inventory_Main";
        autoGenerateID = false;

        if (autoFindReferences)
        {
            FindInventoryReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates inventory-related components.
    /// Checks current GameObject, then Instance, then scene search.
    /// </summary>
    private void FindInventoryReferences()
    {
        if (inventoryManager == null)
            inventoryManager = GetComponent<InventoryManager>() ??
                               InventoryManager.Instance ??
                               FindFirstObjectByType<InventoryManager>();

        DebugLog($"Auto-found inventory reference: {inventoryManager != null}");
    }

    /// <summary>
    /// Validates that necessary references are available.
    /// </summary>
    private void ValidateReferences()
    {
        if (inventoryManager == null)
        {
            Debug.LogError($"[{name}] InventoryManager reference missing! Inventory won't be saved/loaded.");
        }
        else
        {
            DebugLog($"InventoryManager reference validated: {inventoryManager.name}");
        }
    }

    /// <summary>
    /// Extracts complete inventory state including grid size, items, and next ID counter.
    /// </summary>
    public override object GetDataToSave()
    {
        if (inventoryManager == null)
        {
            DebugLog("Cannot save inventory - InventoryManager not found");
            return new InventorySaveData();
        }

        var saveData = ExtractInventoryDataFromManager();
        DebugLog($"Extracted inventory data: {saveData.ItemCount} items in {saveData.gridWidth}x{saveData.gridHeight} grid");
        return saveData;
    }

    /// <summary>
    /// Extracts inventory data from the manager by directly accessing its state.
    /// Creates InventorySaveData with all items converted to save format.
    /// </summary>
    private InventorySaveData ExtractInventoryDataFromManager()
    {
        var saveData = new InventorySaveData(inventoryManager.GridWidth, inventoryManager.GridHeight);
        saveData.nextItemId = inventoryManager.NextItemId;

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
    /// Extracts inventory data from various save container formats.
    /// Handles the transition from unified save structures to inventory-specific data.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting inventory save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new InventorySaveData();
        }

        // Check PlayerPersistentData first (where rebuilt data is stored)
        if (saveContainer is PlayerPersistentData persistentData)
        {
            var inventoryData = persistentData.GetComponentData<InventorySaveData>(SaveID);
            if (inventoryData != null)
            {
                DebugLog($"Extracted inventory from persistent data: {inventoryData.ItemCount} items");
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
            // Check custom stats for inventory data
            if (playerSaveData.customStats.TryGetValue("inventoryData", out object invDataObj) &&
                invDataObj is InventorySaveData invData)
            {
                DebugLog($"Extracted inventory from PlayerSaveData customStats: {invData.ItemCount} items");
                return invData;
            }

            // Check for component ID in custom stats
            if (playerSaveData.customStats.TryGetValue(SaveID, out object inventoryDataObj) &&
                inventoryDataObj is InventorySaveData inventorySaveData)
            {
                DebugLog($"Extracted inventory from PlayerSaveData by SaveID: {inventorySaveData.ItemCount} items");
                return inventorySaveData;
            }

            DebugLog("No inventory data found in PlayerSaveData");
            return new InventorySaveData();
        }
        else if (saveContainer is InventorySaveData directInventoryData)
        {
            DebugLog($"Extracted direct InventorySaveData: {directInventoryData.ItemCount} items");
            return directInventoryData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return new InventorySaveData();
    }

    /// <summary>
    /// Restores inventory data to the manager. Inventory restoration is the same
    /// regardless of context - items are always fully restored to their saved state.
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is InventorySaveData inventoryData))
        {
            DebugLog($"Invalid save data type for inventory. Data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING INVENTORY DATA (Context: {context}) ===");

        // Refresh references after scene load
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
            RestoreInventoryDataToManager(inventoryData);
            DebugLog("Inventory restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load inventory: {e.Message}");
        }
    }

    /// <summary>
    /// Restores complete inventory state by rebuilding the grid and placing all items.
    /// </summary>
    private void RestoreInventoryDataToManager(InventorySaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            DebugLog("Invalid inventory save data - clearing inventory");
            inventoryManager.ClearInventory();
            return;
        }

        // Create new inventory grid
        var newInventoryData = new InventoryGridData(saveData.gridWidth, saveData.gridHeight);

        // Restore each item to the grid
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

        // Set complete data to manager
        inventoryManager.SetInventoryData(newInventoryData, saveData.nextItemId);
        DebugLog($"Restored inventory: {newInventoryData.ItemCount} items");
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts inventory data from unified save structure for modular loading.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var inventoryData = unifiedData.GetComponentData<InventorySaveData>(SaveID);
        if (inventoryData != null)
        {
            DebugLog($"Extracted inventory from dynamic storage: {inventoryData.ItemCount} items");
            return inventoryData;
        }

        DebugLog("No inventory data found in unified save - returning empty inventory");
        return new InventorySaveData();
    }

    /// <summary>
    /// Creates default empty inventory for new games.
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default inventory data for new game");

        int gridWidth = 10;
        int gridHeight = 10;

        if (inventoryManager != null)
        {
            gridWidth = inventoryManager.GridWidth;
            gridHeight = inventoryManager.GridHeight;
        }

        var defaultData = new InventorySaveData(gridWidth, gridHeight);
        defaultData.nextItemId = 1;

        DebugLog($"Default inventory data created: {gridWidth}x{gridHeight} grid, empty");
        return defaultData;
    }

    /// <summary>
    /// Contributes inventory data to unified save structure for save file creation.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is InventorySaveData inventoryData && unifiedData != null)
        {
            DebugLog($"Contributing inventory data to unified save: {inventoryData.ItemCount} items");

            unifiedData.SetComponentData(SaveID, inventoryData);

            DebugLog($"Inventory data contributed: {inventoryData.ItemCount} items stored");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected InventorySaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Called before save operations to ensure current references.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing inventory for save");

        if (autoFindReferences)
        {
            FindInventoryReferences();
        }
    }

    /// <summary>
    /// Called after load operations. Inventory UI updates automatically via events.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Inventory load completed");
    }
}