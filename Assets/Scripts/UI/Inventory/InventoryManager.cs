using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Core inventory system managing the tetris-style grid-based item placement.
/// Handles item addition, removal, movement, and rotation independently of UI.
/// Persists across scenes and fires events for UI synchronization.
/// Does not handle its own saving - InventorySaveComponent manages persistence.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;

    [Header("Test Items")]
    [SerializeField] private List<ItemData> testItems = new List<ItemData>();

    private InventoryGridData inventoryData;
    private int nextItemId = 1;

    // Events for UI synchronization
    public event Action<InventoryItemData> OnItemAdded;
    public event Action<string> OnItemRemoved;
    public event Action OnInventoryCleared;
    public event Action<InventoryGridData> OnInventoryDataChanged;

    // Public properties
    public InventoryGridData InventoryData => inventoryData;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public int NextItemId { get => nextItemId; set => nextItemId = value; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        inventoryData = new InventoryGridData(gridWidth, gridHeight);
    }

    /// <summary>
    /// Adds an item to the inventory at the specified position or finds a valid position automatically.
    /// Generates a unique ID for the item and handles tetris-style placement validation.
    /// </summary>
    public bool AddItem(ItemData itemData, Vector2Int? preferredPosition = null)
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot add item - ItemData is null");
            return false;
        }

        string itemId = $"item_{nextItemId}";
        var inventoryItem = new InventoryItemData(itemId, itemData, Vector2Int.zero);

        Vector2Int? position = preferredPosition;
        if (position == null || !inventoryData.IsValidPosition(position.Value, inventoryItem))
        {
            position = inventoryData.FindValidPositionForItem(inventoryItem);
        }

        if (position == null)
        {
            InventoryDebugSystem.LogItemPlacementAttempt(itemId, preferredPosition ?? Vector2Int.zero, false, "No valid position found");
            Debug.LogWarning($"Cannot add item {itemData.itemName} - no valid position found");
            return false;
        }

        inventoryItem.SetGridPosition(position.Value);

        if (inventoryData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            InventoryDebugSystem.LogItemPlacementAttempt(itemId, position.Value, true, "Item added successfully");
            OnItemAdded?.Invoke(inventoryItem);
            OnInventoryDataChanged?.Invoke(inventoryData);
            Debug.Log($"Added item {itemData.itemName} at position {position.Value}");
            return true;
        }

        InventoryDebugSystem.LogItemPlacementAttempt(itemId, position.Value, false, "Failed to place in inventory data");
        Debug.LogError($"Failed to place item {itemData.itemName} in inventory data");
        return false;
    }

    /// <summary>
    /// Removes an item from the inventory by its unique ID.
    /// Frees up the grid space and fires removal events.
    /// </summary>
    public bool RemoveItem(string itemId)
    {
        Debug.Log($"[InventoryManager] RemoveItem called for: {itemId}");

        // Check if item exists
        var item = inventoryData.GetItem(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[InventoryManager] RemoveItem failed: Item {itemId} not found in inventory");
            return false;
        }

        Debug.Log($"[InventoryManager] Found item {itemId} ({item.ItemData?.itemName}) at position {item.GridPosition}");

        // Attempt removal
        bool success = inventoryData.RemoveItem(itemId);

        if (success)
        {
            Debug.Log($"[InventoryManager] Successfully removed {itemId} from inventory grid");
            OnItemRemoved?.Invoke(itemId);
            OnInventoryDataChanged?.Invoke(inventoryData);
            Debug.Log($"[InventoryManager] Events fired for item removal: {itemId}");
            return true;
        }
        else
        {
            Debug.LogError($"[InventoryManager] Failed to remove {itemId} from inventory grid - item may not exist in grid");
            return false;
        }
    }
    /// <summary>
    /// Moves an item to a new grid position with collision validation.
    /// Temporarily removes item to test placement, then restores if invalid.
    /// </summary>
    public bool MoveItem(string itemId, Vector2Int newPosition)
    {
        var item = inventoryData.GetItem(itemId);
        if (item == null)
        {
            InventoryDebugSystem.LogItemPlacementAttempt(itemId, newPosition, false, "Item not found");
            return false;
        }

        var originalPosition = item.GridPosition;

        // Temporarily remove for collision testing
        inventoryData.RemoveItem(itemId);
        item.SetGridPosition(newPosition);

        if (inventoryData.IsValidPosition(newPosition, item))
        {
            inventoryData.PlaceItem(item);
            InventoryDebugSystem.LogItemPlacementAttempt(itemId, newPosition, true);
            OnInventoryDataChanged?.Invoke(inventoryData);
            return true;
        }
        else
        {
            // Restore to original position
            item.SetGridPosition(originalPosition);
            inventoryData.PlaceItem(item);
            InventoryDebugSystem.LogItemPlacementAttempt(itemId, newPosition, false, "Position invalid or occupied");
            return false;
        }
    }

    /// <summary>
    /// Rotates an item clockwise with proper grid state management.
    /// Handles collision detection and reverts rotation if new orientation doesn't fit.
    /// </summary>
    public bool RotateItem(string itemId)
    {
        var item = inventoryData.GetItem(itemId);
        if (item == null)
        {
            InventoryDebugSystem.LogItemRotationAttempt(itemId, -1, -1, false, "Item not found");
            return false;
        }

        if (!item.CanRotate)
        {
            InventoryDebugSystem.LogItemRotationAttempt(itemId, item.currentRotation, -1, false, "Item cannot rotate");
            return false;
        }

        var originalRotation = item.currentRotation;
        var originalPosition = item.GridPosition;

        // Calculate next rotation
        int maxRotations = TetrominoDefinitions.GetRotationCount(item.shapeType);
        int newRotation = (originalRotation + 1) % maxRotations;

        // Remove from grid before testing rotation
        bool wasInGrid = inventoryData.GetItem(itemId) != null;
        if (wasInGrid)
        {
            inventoryData.RemoveItem(itemId);
        }

        // Apply new rotation and test
        item.SetRotation(newRotation);

        if (inventoryData.IsValidPosition(originalPosition, item))
        {
            // Place back with new rotation
            if (inventoryData.PlaceItem(item))
            {
                InventoryDebugSystem.LogItemRotationAttempt(itemId, originalRotation, newRotation, true);
                OnInventoryDataChanged?.Invoke(inventoryData);
                return true;
            }
            else
            {
                // Failed to place - revert and restore
                item.SetRotation(originalRotation);
                inventoryData.PlaceItem(item);
                InventoryDebugSystem.LogItemRotationAttempt(itemId, originalRotation, newRotation, false, "Failed to place after rotation");
                return false;
            }
        }
        else
        {
            // New rotation invalid - revert and restore
            item.SetRotation(originalRotation);
            if (wasInGrid)
            {
                inventoryData.PlaceItem(item);
            }
            InventoryDebugSystem.LogItemRotationAttempt(itemId, originalRotation, newRotation, false, "New rotation invalid at current position");
            return false;
        }
    }

    /// <summary>
    /// Clears all items from the inventory and resets the ID counter.
    /// </summary>
    public void ClearInventory()
    {
        inventoryData.Clear();
        nextItemId = 1;
        OnInventoryCleared?.Invoke();
        OnInventoryDataChanged?.Invoke(inventoryData);
        Debug.Log("Inventory cleared");
    }

    /// <summary>
    /// Checks if the inventory has space for a new item by testing placement.
    /// </summary>
    public bool HasSpaceForItem(ItemData itemData)
    {
        if (itemData == null) return false;

        var tempItem = new InventoryItemData($"temp_{nextItemId}", itemData, Vector2Int.zero);
        return inventoryData.FindValidPositionForItem(tempItem) != null;
    }

    /// <summary>
    /// Returns inventory statistics for UI display and debugging.
    /// </summary>
    public (int itemCount, int occupiedCells, int totalCells) GetInventoryStats()
    {
        int occupiedCells = 0;
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (inventoryData.IsOccupied(x, y))
                    occupiedCells++;
            }
        }

        return (inventoryData.ItemCount, occupiedCells, gridWidth * gridHeight);
    }

    /// <summary>
    /// Directly sets inventory data and ID counter. Used by InventorySaveComponent for data restoration.
    /// </summary>
    public void SetInventoryData(InventoryGridData newData, int newNextItemId)
    {
        inventoryData = newData;
        nextItemId = newNextItemId;

        // Trigger events for UI updates
        OnInventoryDataChanged?.Invoke(inventoryData);

        var allItems = inventoryData.GetAllItems();
        foreach (var item in allItems)
        {
            OnItemAdded?.Invoke(item);
        }
    }

    #region Debug Methods

    [Button("Add Test Item")]
    private void AddTestItem()
    {
        if (testItems.Count > 0)
        {
            var randomItem = testItems[UnityEngine.Random.Range(0, testItems.Count)];
            AddItem(randomItem);
        }
    }

    [Button("Clear Inventory")]
    private void DebugClearInventory()
    {
        ClearInventory();
    }

    [Button("Debug Grid State")]
    private void DebugGridState()
    {
        Debug.Log("=== GRID DEBUG INFO ===");
        for (int y = 0; y < gridHeight; y++)
        {
            string row = $"Row {y}: ";
            for (int x = 0; x < gridWidth; x++)
            {
                var item = inventoryData.GetItemAt(x, y);
                row += (item != null ? "X" : ".") + " ";
            }
            Debug.Log(row);
        }

        Debug.Log($"Total items: {inventoryData.ItemCount}");
        foreach (var item in inventoryData.GetAllItems())
        {
            Debug.Log($"Item {item.ID}: {item.ItemData?.itemName} at {item.GridPosition} rotation {item.currentRotation}");
        }
    }

    private void Update()
    {
        // Debug controls
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearInventory();
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            AddTestItem();
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            DebugGridState();
        }
    }

    #endregion
}