using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Core inventory system that manages data independently of visuals
/// This is a singleton that persists across scenes
/// FIXED: Improved rotation handling to prevent cell occupation issues
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

    public InventoryGridData InventoryData => inventoryData;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;

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
        //Debug.Log($"PersistentInventoryManager initialized with {gridWidth}x{gridHeight} grid");
    }

    /// <summary>
    /// Add an item to the inventory (finds a valid position automatically)
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
    /// Remove an item from the inventory
    /// </summary>
    public bool RemoveItem(string itemId)
    {
        if (inventoryData.RemoveItem(itemId))
        {
            OnItemRemoved?.Invoke(itemId);
            OnInventoryDataChanged?.Invoke(inventoryData);
            //            Debug.Log($"Removed item {itemId}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Move an item to a new position
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

        // Temporarily remove item to test new position
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
            // Restore item to original position
            item.SetGridPosition(originalPosition);
            inventoryData.PlaceItem(item);
            InventoryDebugSystem.LogItemPlacementAttempt(itemId, newPosition, false, "Position invalid or occupied");
            return false;
        }
    }

    /// <summary>
    /// Rotate an item - FIXED version that properly handles grid state
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

        // Store original state
        var originalRotation = item.currentRotation;
        var originalPosition = item.GridPosition;

        // Calculate next rotation
        int maxRotations = TetrominoDefinitions.GetRotationCount(item.shapeType);
        int newRotation = (originalRotation + 1) % maxRotations;

        // IMPORTANT: Remove item from grid before testing rotation
        bool wasInGrid = inventoryData.GetItem(itemId) != null;
        if (wasInGrid)
        {
            inventoryData.RemoveItem(itemId);
        }

        // Apply new rotation
        item.SetRotation(newRotation);

        // Test if new rotation is valid at current position
        if (inventoryData.IsValidPosition(originalPosition, item))
        {
            // Place item back with new rotation
            if (inventoryData.PlaceItem(item))
            {
                InventoryDebugSystem.LogItemRotationAttempt(itemId, originalRotation, newRotation, true);
                OnInventoryDataChanged?.Invoke(inventoryData);
                return true;
            }
            else
            {
                // Failed to place back - revert rotation and restore
                item.SetRotation(originalRotation);
                inventoryData.PlaceItem(item);
                InventoryDebugSystem.LogItemRotationAttempt(itemId, originalRotation, newRotation, false, "Failed to place after rotation");
                return false;
            }
        }
        else
        {
            // New rotation invalid - revert rotation and restore item
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
    /// Clear all items from inventory
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
    /// Get save data for persistence system
    /// </summary>
    public InventorySaveData GetSaveData()
    {
        var saveData = new InventorySaveData(gridWidth, gridHeight); // Return a copy of the data, so if it's a doorway transition, we don't clear it when we load;
        saveData.nextItemId = nextItemId;

        foreach (var item in inventoryData.GetAllItems())
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
    /// Load from save data
    /// </summary>
    public void LoadFromSaveData(InventorySaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            Debug.LogWarning("Invalid inventory save data");
            return;
        }

        ClearInventory();
        nextItemId = saveData.nextItemId;

        foreach (var itemSaveData in saveData.items)
        {
            var item = InventoryItemData.FromSaveData(itemSaveData);
            if (item != null)
            {
                if (inventoryData.PlaceItem(item))
                {
                    OnItemAdded?.Invoke(item);
                }
                else
                {
                    Debug.LogWarning($"Failed to restore item {item.ID} at position {item.GridPosition}");
                }
            }
        }

        OnInventoryDataChanged?.Invoke(inventoryData);
        Debug.Log($"Loaded inventory: {inventoryData.ItemCount} items");
    }

    /// <summary>
    /// Check if inventory has space for an item
    /// </summary>
    public bool HasSpaceForItem(ItemData itemData)
    {
        if (itemData == null) return false;

        var tempItem = new InventoryItemData($"temp_{nextItemId}", itemData, Vector2Int.zero);
        return inventoryData.FindValidPositionForItem(tempItem) != null;
    }

    /// <summary>
    /// Get inventory statistics
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

    // Debug methods
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
}