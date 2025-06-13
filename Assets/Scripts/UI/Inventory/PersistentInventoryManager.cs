using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Core inventory system that manages data independently of visuals
/// This is a singleton that persists across scenes
/// </summary>
public class PersistentInventoryManager : MonoBehaviour
{
    public static PersistentInventoryManager Instance { get; private set; }

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
        Debug.Log($"PersistentInventoryManager initialized with {gridWidth}x{gridHeight} grid");
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
            Debug.LogWarning($"Cannot add item {itemData.itemName} - no valid position found");
            return false;
        }

        inventoryItem.SetGridPosition(position.Value);

        if (inventoryData.PlaceItem(inventoryItem))
        {
            nextItemId++;
            OnItemAdded?.Invoke(inventoryItem);
            OnInventoryDataChanged?.Invoke(inventoryData);
            Debug.Log($"Added item {itemData.itemName} at position {position.Value}");
            return true;
        }

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
            Debug.Log($"Removed item {itemId}");
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
            Debug.LogWarning($"Cannot move item {itemId} - item not found");
            return false;
        }

        // Temporarily remove item to test new position
        inventoryData.RemoveItem(itemId);
        item.SetGridPosition(newPosition);

        if (inventoryData.IsValidPosition(newPosition, item))
        {
            inventoryData.PlaceItem(item);
            OnInventoryDataChanged?.Invoke(inventoryData);
            return true;
        }
        else
        {
            // Restore item to original position
            inventoryData.PlaceItem(item);
            return false;
        }
    }

    /// <summary>
    /// Rotate an item
    /// </summary>
    public bool RotateItem(string itemId)
    {
        var item = inventoryData.GetItem(itemId);
        if (item == null || !item.CanRotate)
            return false;

        // Test rotation
        var originalRotation = item.currentRotation;
        item.RotateItem();

        // Check if new rotation is valid at current position
        if (inventoryData.IsValidPosition(item.GridPosition, item))
        {
            OnInventoryDataChanged?.Invoke(inventoryData);
            return true;
        }
        else
        {
            // Revert rotation
            item.SetRotation(originalRotation);
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
        var saveData = new InventorySaveData(gridWidth, gridHeight);
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

    private void Update()
    {
        // Debug controls
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearInventory();
        }

        // Add some complex shapes directly
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            AddTestItem();
        }
    }
}