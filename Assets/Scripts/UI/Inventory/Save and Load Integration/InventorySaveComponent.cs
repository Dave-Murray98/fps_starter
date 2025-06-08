using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Save component for inventory system - handles saving/loading inventory state
/// Follows the same pattern as PlayerSaveComponent
/// </summary>
public class InventorySaveComponent : SaveComponentBase
{
    [Header("Inventory References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private GridVisual gridVisual;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();

        // Auto-find references
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>();

        if (gridVisual == null)
            gridVisual = FindFirstObjectByType<GridVisual>();

        // Fixed ID for inventory
        saveID = "Inventory_Main";
        autoGenerateID = false;
    }

    public override object GetSaveData()
    {
        if (inventoryManager == null || gridVisual == null)
        {
            DebugLog("Cannot save inventory - missing references");
            return null;
        }

        var gridData = gridVisual.GridData;
        if (gridData == null)
        {
            DebugLog("Cannot save inventory - GridData is null");
            return null;
        }

        // Create save data
        var saveData = new InventorySaveData(gridData.Width, gridData.Height);

        // Store next item ID from inventory manager
        saveData.nextItemId = GetNextItemIdFromManager();

        // Get all items from grid
        var allItems = gridData.GetAllItems();

        foreach (var gridItem in allItems)
        {
            if (gridItem?.itemData != null)
            {
                var itemSaveData = InventoryItemSaveData.FromGridItem(gridItem);
                if (itemSaveData != null && itemSaveData.IsValid())
                {
                    saveData.AddItem(itemSaveData);
                }
                else
                {
                    DebugLog($"Failed to create save data for item: {gridItem.ID}");
                }
            }
        }

        DebugLog($"Saved inventory: {saveData.ItemCount} items in {gridData.Width}x{gridData.Height} grid");
        return saveData;
    }

    public override void LoadSaveData(object data)
    {
        if (!(data is InventorySaveData inventorySaveData))
        {
            DebugLog("Invalid save data type for inventory");
            return;
        }

        if (!inventorySaveData.IsValid())
        {
            DebugLog("Invalid inventory save data");
            return;
        }

        if (inventoryManager == null || gridVisual == null)
        {
            DebugLog("Cannot load inventory - missing references");
            return;
        }

        DebugLog($"Loading inventory: {inventorySaveData.ItemCount} items in {inventorySaveData.gridWidth}x{inventorySaveData.gridHeight} grid");

        // Clear current inventory completely
        inventoryManager.ClearInventory();
        Debug.Log("InventorySaveComponent.LoadSaveData: Cleared current inventory and recreated inventoryManager.activeItems dictionary");
        inventoryManager.activeItems = new Dictionary<string, DraggableGridItem>();


        // Verify grid dimensions match (or resize if needed)
        var currentGrid = gridVisual.GridData;
        if (currentGrid.Width != inventorySaveData.gridWidth || currentGrid.Height != inventorySaveData.gridHeight)
        {
            DebugLog($"Grid size mismatch - Current: {currentGrid.Width}x{currentGrid.Height}, Save: {inventorySaveData.gridWidth}x{inventorySaveData.gridHeight}");
            // For now, continue with current grid - in the future you might want to resize
        }

        // Restore next item ID
        SetNextItemIdInManager(inventorySaveData.nextItemId);

        // Load each item in the exact position it was saved
        int successCount = 0;
        int failCount = 0;

        foreach (var itemSaveData in inventorySaveData.items)
        {
            if (RestoreItem(itemSaveData))
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        DebugLog($"Inventory load complete: {successCount} items loaded, {failCount} failed");

        // Refresh visual representation
        gridVisual.RefreshVisual();
    }

    /// <summary>
    /// Restore a single item from save data
    /// </summary>
    private bool RestoreItem(InventoryItemSaveData itemSaveData)
    {
        if (!itemSaveData.IsValid())
        {
            DebugLog($"Invalid item save data: {itemSaveData}");
            return false;
        }

        // Convert save data back to GridItem
        GridItem gridItem = itemSaveData.ToGridItem();
        if (gridItem == null)
        {
            DebugLog($"Failed to create GridItem from save data: {itemSaveData}");
            return false;
        }

        // Verify the position is valid for this item's current rotation
        var gridData = gridVisual.GridData;
        if (!gridData.IsValidPosition(gridItem.GridPosition, gridItem))
        {
            DebugLog($"Cannot place item {gridItem.ID} at saved position {gridItem.GridPosition} - position invalid");
            // Try to find alternative position
            Vector2Int? alternativePos = FindAlternativePosition(gridItem);
            if (alternativePos.HasValue)
            {
                gridItem.SetGridPosition(alternativePos.Value);
                DebugLog($"Placed item {gridItem.ID} at alternative position {alternativePos.Value}");
            }
            else
            {
                DebugLog($"No alternative position found for item {gridItem.ID}");
                return false;
            }
        }

        // Place the item in grid data
        if (!gridData.PlaceItem(gridItem))
        {
            DebugLog($"Failed to place item {gridItem.ID} in grid data");
            return false;
        }

        // Create visual representation
        var activeItems = inventoryManager.GetActiveItems();
        var itemObj = CreateItemVisual(gridItem);

        if (itemObj != null)
        {
            var draggableItem = itemObj.GetComponent<DraggableGridItem>();
            if (draggableItem != null)
            {
                activeItems[gridItem.ID] = draggableItem;
            }
        }

        //DONT call RefreshVisual here - it causes duplicates!

        return true;
    }

    /// <summary>
    /// Create visual representation for a restored item
    /// </summary>
    private GameObject CreateItemVisual(GridItem gridItem)
    {
        DebugLog($"Creating visual for item: {gridItem.ID} at position {gridItem.GridPosition}");
        // Use the same method as InventoryManager
        GameObject itemObj = new GameObject($"Item_{gridItem.ID}");
        itemObj.transform.SetParent(gridVisual.transform, false);
        itemObj.AddComponent<RectTransform>();
        itemObj.AddComponent<InventoryItemShapeRenderer>();
        itemObj.AddComponent<DraggableGridItem>();

        // Initialize the draggable component
        DraggableGridItem draggable = itemObj.GetComponent<DraggableGridItem>();
        if (draggable != null)
        {
            draggable.Initialize(gridItem, gridVisual);
        }

        return itemObj;
    }

    /// <summary>
    /// Try to find an alternative position for an item that can't be placed at its saved location
    /// </summary>
    private Vector2Int? FindAlternativePosition(GridItem gridItem)
    {
        var gridData = gridVisual.GridData;

        // Search from top-left, row by row
        for (int y = 0; y < gridData.Height; y++)
        {
            for (int x = 0; x < gridData.Width; x++)
            {
                Vector2Int testPos = new Vector2Int(x, y);
                if (gridData.IsValidPosition(testPos, gridItem))
                {
                    return testPos;
                }
            }
        }

        return null; // No valid position found
    }

    /// <summary>
    /// Get the next item ID from the inventory manager
    /// </summary>
    private int GetNextItemIdFromManager()
    {
        // Access the private field through reflection or add a public property
        // For now, return a default value - you might want to add a public property to InventoryManager
        return 1;
    }

    /// <summary>
    /// Set the next item ID in the inventory manager
    /// </summary>
    private void SetNextItemIdInManager(int nextId)
    {
        // Access the private field through reflection or add a public method
        // For now, do nothing - you might want to add a public method to InventoryManager
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
        return GetSaveData() as InventorySaveData;
    }

    /// <summary>
    /// Manually load inventory data (useful for testing)
    /// </summary>
    public void LoadInventoryData(InventorySaveData saveData)
    {
        LoadSaveData(saveData);
    }

    /// <summary>
    /// Refresh component references (called during setup)
    /// </summary>
    public void RefreshReferences()
    {
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>();

        if (gridVisual == null)
            gridVisual = FindFirstObjectByType<GridVisual>();

        DebugLog($"References refreshed - InventoryManager: {inventoryManager != null}, GridVisual: {gridVisual != null}");
    }

    /// <summary>
    /// Get the next item ID for save system
    /// </summary>
    public int GetNextItemId()
    {
        return inventoryManager.nextItemId;
    }

    /// <summary>
    /// Set the next item ID (used when loading from save)
    /// </summary>
    public void SetNextItemId(int id)
    {
        inventoryManager.nextItemId = id;
    }

    /// <summary>
    /// Get current inventory save data
    /// </summary>
    public InventorySaveData GetInventorySaveData()
    {
        if (gridVisual?.GridData == null)
            return new InventorySaveData();

        var gridData = gridVisual.GridData;
        var saveData = new InventorySaveData(gridData.Width, gridData.Height);
        saveData.nextItemId = inventoryManager.nextItemId;

        // Get all items from grid
        var allItems = gridData.GetAllItems();
        foreach (var gridItem in allItems)
        {
            if (gridItem?.itemData != null)
            {
                var itemSaveData = InventoryItemSaveData.FromGridItem(gridItem);
                if (itemSaveData != null && itemSaveData.IsValid())
                {
                    saveData.AddItem(itemSaveData);
                }
            }
        }

        return saveData;
    }

    /// <summary>
    /// Load inventory from save data
    /// </summary>
    public void LoadInventoryFromSaveData(InventorySaveData saveData)
    {
        Debug.Log("Loading inventory from save data...");
        if (saveData == null || !saveData.IsValid())
        {
            Debug.LogWarning("Invalid inventory save data");
            return;
        }

        // Clear current inventory
        inventoryManager.ClearInventory();
        Debug.Log("InventorySaveComponent.LoadInventoryFromSaveData: Cleared current inventory and recreated inventoryManager.activeItems dictionary");
        inventoryManager.activeItems = new Dictionary<string, DraggableGridItem>();

        // Set next item ID
        inventoryManager.nextItemId = saveData.nextItemId;

        // Restore each item
        foreach (var itemSaveData in saveData.items)
        {
            RestoreItemFromSaveData(itemSaveData);
        }


        // Refresh visual
        //gridVisual?.RefreshVisual();
    }

    /// <summary>
    /// Restore a single item from save data
    /// </summary>
    private bool RestoreItemFromSaveData(InventoryItemSaveData itemSaveData)
    {
        Debug.Log($"Restoring item from save data: {itemSaveData.itemDataName}");
        if (!itemSaveData.IsValid())
        {
            Debug.LogWarning($"Invalid item save data: {itemSaveData}");
            return false;
        }

        // Convert to GridItem
        GridItem gridItem = itemSaveData.ToGridItem();
        if (gridItem == null)
        {
            Debug.LogWarning($"Failed to create GridItem from save data: {itemSaveData}");
            return false;
        }

        // Ensure we have valid references
        if (gridVisual?.GridData == null)
        {
            Debug.LogError("GridVisual or GridData is null - cannot restore item");
            return false;
        }

        // Verify the position is valid for this item's current rotation
        var gridData = gridVisual.GridData;
        if (!gridData.IsValidPosition(gridItem.GridPosition, gridItem))
        {
            Debug.LogWarning($"Cannot place item {gridItem.ID} at saved position {gridItem.GridPosition} - position invalid");
            // Try to find alternative position
            Vector2Int? altPos = inventoryManager.FindValidPositionForShape(gridItem.itemData);
            if (altPos.HasValue && altPos.Value.x != -1)
            {
                gridItem.SetGridPosition(altPos.Value);
                Debug.Log($"Placed item {gridItem.ID} at alternative position {altPos.Value}");
            }
            else
            {
                Debug.LogWarning($"No alternative position found for item {gridItem.ID}");
                return false;
            }
        }

        // Place in grid
        if (!gridData.PlaceItem(gridItem))
        {
            Debug.LogError($"Failed to place item {gridItem.ID} in grid data");
            return false;
        }

        // Create visual
        GameObject itemObj = CreateItemVisual(gridItem);
        if (itemObj == null)
        {
            Debug.LogError($"Failed to create visual for item {gridItem.ID}");
            return false;
        }

        DraggableGridItem draggableItem = itemObj.GetComponent<DraggableGridItem>();
        if (draggableItem != null && inventoryManager.activeItems != null)
        {
            inventoryManager.activeItems[gridItem.ID] = draggableItem;
            Debug.Log($"Added {gridItem.ID} item to active items");
        }
        else
        {
            if (draggableItem == null)
            {
                Debug.LogWarning($"DraggableGridItem component is missing on item visual for {gridItem.ID}");
            }
            if (inventoryManager.activeItems == null)
            {
                Debug.LogWarning($"Active items dictionary is null - cannot add {gridItem.ID}");
            }
        }

        return true;
    }
}