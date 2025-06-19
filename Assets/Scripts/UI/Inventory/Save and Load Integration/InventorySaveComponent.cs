using UnityEngine;

/// <summary>
/// Updated save component that works with the new data-driven inventory system
/// No longer depends on UI being active - works directly with InventoryManager
/// </summary>
public class InventorySaveComponent : SaveComponentBase
{
    private InventoryManager inventoryManager;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();

        // Fixed ID for inventory
        saveID = "Inventory_Main";
        autoGenerateID = false;

        // Get reference to persistent inventory
        inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            // Try to find it in the scene
            inventoryManager = FindFirstObjectByType<InventoryManager>();
        }
    }

    private void Start()
    {
        // Ensure we have the persistent inventory reference
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
        }
    }

    public override object GetDataToSave()
    {
        if (inventoryManager == null)
        {
            DebugLog("Cannot save inventory - PersistentInventoryManager not found");
            return null;
        }

        var saveData = inventoryManager.GetSaveData();
        DebugLog($"Saved inventory: {saveData.ItemCount} items in {saveData.gridWidth}x{saveData.gridHeight} grid");
        return saveData;
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("InventorySaveComponent: Extracting inventory save data");

        if (saveContainer == null)
        {
            DebugLog("InventorySaveComponent.ExtractRelevantData(): saveContainer is null");
            return null;
        }

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Extract inventory data from player save
            if (playerSaveData.inventoryData != null && playerSaveData.inventoryData.ItemCount > 0)
            {
                DebugLog($"Extracting inventory data from PlayerSaveData: {playerSaveData.inventoryData.ItemCount} items");
                return playerSaveData.inventoryData;
            }
            else
            {
                DebugLog("No valid inventory data found in PlayerSaveData - creating empty inventory");
                return new InventorySaveData(); // Return empty but valid data
            }
        }
        else if (saveContainer is InventorySaveData inventorySaveData)
        {
            // Direct inventory save data
            DebugLog($"Extracting direct InventorySaveData: {inventorySaveData.ItemCount} items");
            return inventorySaveData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData or InventorySaveData, got {saveContainer.GetType()}");
            return null;
        }
    }

    public override void LoadSaveData(object data)
    {
        if (!(data is InventorySaveData inventoryData))
        {
            DebugLog($"Invalid save data type for inventory. Data type: {data?.GetType()}");
            return;
        }

        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                DebugLog("Cannot load inventory - PersistentInventoryManager not found");
                return;
            }
        }

        DebugLog($"Loading inventory: {inventoryData.ItemCount} items in {inventoryData.gridWidth}x{inventoryData.gridHeight} grid");

        try
        {
            // Load directly into persistent inventory - no UI dependencies!
            inventoryManager.LoadFromSaveData(inventoryData);
            DebugLog("Inventory loaded successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load inventory: {e.Message}");
        }
    }

    public override void OnBeforeSave()
    {
        DebugLog("Preparing inventory for save");
        // Any preparation needed before saving
    }

    public override void OnAfterLoad()
    {
        DebugLog("Inventory load completed");
        // Any cleanup needed after loading
    }

    /// <summary>
    /// Get current inventory save data (useful for testing)
    /// </summary>
    public InventorySaveData GetCurrentSaveData()
    {
        return GetDataToSave() as InventorySaveData;
    }

    /// <summary>
    /// Manually load inventory data (useful for testing)
    /// </summary>
    public void LoadInventoryData(InventorySaveData saveData)
    {
        LoadSaveData(saveData);
    }

    /// <summary>
    /// Public method to get inventory save data for PlayerPersistenceManager
    /// </summary>
    public InventorySaveData GetInventorySaveData()
    {
        if (inventoryManager == null)
        {
            DebugLog("PersistentInventoryManager not found - returning empty inventory data");
            return new InventorySaveData();
        }

        return inventoryManager.GetSaveData();
    }

    /// <summary>
    /// Public method to load inventory from save data
    /// </summary>
    public void LoadInventoryFromSaveData(InventorySaveData saveData)
    {
        LoadSaveData(saveData);
    }

    /// <summary>
    /// Check if persistent inventory manager is available
    /// </summary>
    public bool IsPersistentInventoryAvailable()
    {
        return inventoryManager != null;
    }

    /// <summary>
    /// Force refresh the persistent inventory reference
    /// </summary>
    public void RefreshPersistentInventoryReference()
    {
        inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            inventoryManager = FindFirstObjectByType<InventoryManager>();
        }
        DebugLog($"Persistent inventory reference refreshed: {inventoryManager != null}");
    }
}