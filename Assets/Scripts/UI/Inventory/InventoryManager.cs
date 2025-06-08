using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryManager : MonoBehaviour, IManager
{
    [Header("Grid Reference")]
    [SerializeField] private GridVisual gridVisual;

    [Header("Item Prefab")]
    public GameObject draggableItemPrefab;

    [Header("Item Data Configuration")]
    [SerializeField] private List<ItemInstanceData> testItems = new List<ItemInstanceData>();

    [ShowInInspector] public Dictionary<string, DraggableGridItem> activeItems;
    public int nextItemId = 1;

    [System.Serializable]
    public class ItemInstanceData
    {
        public ItemData itemData;
        [Range(1, 5)] public int quantity = 1;
    }

    [System.Serializable]
    public class TestItemData
    {
        public string itemName;
        public TetrominoType shapeType = TetrominoType.Single;
        [Range(1, 5)] public int quantity = 1;
    }

    public void Initialize()
    {
        activeItems = new Dictionary<string, DraggableGridItem>();

        if (gridVisual == null)
        {
            gridVisual = FindFirstObjectByType<GridVisual>();
        }
    }

    public void RefreshReferences()
    {
        //Debug.Log("Refreshing references in InventoryManager...");
        if (gridVisual == null)
        {
            gridVisual = FindFirstObjectByType<GridVisual>();
            if (gridVisual == null)
            {
                Debug.LogError("GridVisual not found! Please assign it in the inspector.");
                return;
            }
        }
    }

    public void Cleanup()
    {
        //Debug.Log("Cleaning up InventoryManager...");
    }


    private void CreateItemDataItems()
    {
        int currentX = 0;
        int currentY = 0;

        foreach (var itemInstance in testItems)
        {
            if (itemInstance.itemData == null)
            {
                Debug.LogWarning("ItemData is null in test items!");
                continue;
            }

            for (int i = 0; i < itemInstance.quantity; i++)
            {
                // Find a valid position for the item
                Vector2Int position = FindValidPositionForShape(itemInstance.itemData, currentX, currentY);

                if (position.x != -1) // Valid position found
                {
                    CreateItem(itemInstance.itemData, position);

                    // Update position for next item
                    var bounds = itemInstance.itemData.GetBoundingSize();
                    currentX = position.x + bounds.x;

                    // Move to next row if we're getting close to the edge
                    if (currentX >= gridVisual.GridData.Width - 3)
                    {
                        currentX = 0;
                        currentY += bounds.y + 1;
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find valid position for {itemInstance.itemData.itemName}");
                }
            }
        }
    }


    public Vector2Int FindValidPositionForShape(ItemData itemData, int startX = 0, int startY = 0)
    {
        // Debug.Log("Creating tempItem to find valid position for shape: " + itemData.itemName);
        // Create a temporary item to test positioning
        var tempItem = new GridItem($"temp_{nextItemId}", itemData, Vector2Int.zero);

        // Try to find a valid position starting from the given coordinates
        for (int y = startY; y < gridVisual.GridData.Height; y++)
        {
            for (int x = (y == startY ? startX : 0); x < gridVisual.GridData.Width; x++)
            {
                Vector2Int testPos = new Vector2Int(x, y);
                if (gridVisual.GridData.IsValidPosition(testPos, tempItem))
                {
                    return testPos;
                }
            }
        }

        return new Vector2Int(-1, -1); // No valid position found
    }

    // Create item from ItemData ScriptableObject
    public DraggableGridItem CreateItem(ItemData itemData, Vector2Int? position = null)
    {
        if (itemData == null)
        {
            Debug.LogError("Cannot create item - ItemData is null");
            return null;
        }

        // Create grid item data
        string itemId = $"item_{nextItemId}";
        GridItem gridItem = new GridItem(itemId, itemData, Vector2Int.zero);

        // Find position if not provided
        Vector2Int itemPosition = position ?? FindValidPositionForShape(itemData);

        if (itemPosition.x == -1)
        {
            Debug.LogError($"Cannot create item {itemData.itemName} - no valid position found");
            return null;
        }

        gridItem.SetGridPosition(itemPosition);

        // Place in grid data
        if (!gridVisual.GridData.PlaceItem(gridItem))
        {
            Debug.LogError($"Failed to place item {itemData.itemName} in grid data");
            return null;
        }

        // Create visual representation
        GameObject itemObj = CreateItemVisual(gridItem);
        DraggableGridItem draggableItem = itemObj.GetComponent<DraggableGridItem>();

        // Store reference
        activeItems[gridItem.ID] = draggableItem;
        nextItemId++;

        return draggableItem;
    }


    private GameObject CreateItemVisual(GridItem gridItem)
    {
        GameObject itemObj;

        if (draggableItemPrefab != null)
        {
            itemObj = Instantiate(draggableItemPrefab, gridVisual.transform);
        }
        else
        {
            // Create item from scratch
            itemObj = new GameObject($"Item_{gridItem.ID}");
            itemObj.transform.SetParent(gridVisual.transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemShapeRenderer>();
            itemObj.AddComponent<DraggableGridItem>();
        }

        // Initialize the draggable item
        DraggableGridItem draggableItem = itemObj.GetComponent<DraggableGridItem>();
        if (draggableItem != null)
        {
            draggableItem.Initialize(gridItem, gridVisual);
        }

        return itemObj;
    }

    public bool RemoveItem(string itemId)
    {
        if (activeItems.ContainsKey(itemId))
        {
            DraggableGridItem item = activeItems[itemId];
            GridItem gridItem = item.GridItem;

            // Remove from grid data
            gridVisual.GridData.RemoveItem(gridItem.ID);

            // Remove visual
            DestroyImmediate(item.gameObject);

            // Remove from dictionary
            activeItems.Remove(itemId);

            // Refresh visual
            gridVisual.RefreshVisual();

            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if inventory has any items
    /// </summary>
    public bool HasItems()
    {
        return activeItems.Count > 0;
    }

    /// <summary>
    /// Get total number of items in inventory
    /// </summary>
    public int GetItemCount()
    {
        return activeItems.Count;
    }

    public void ClearInventory()
    {
        Debug.Log("Clearing inventory...");
        if (activeItems != null)
        {
            foreach (var item in activeItems.Values)
            {
                if (item != null && item.gameObject != null)
                {
                    DestroyImmediate(item.gameObject);
                }
            }
            activeItems.Clear();

        }
        else
        {
            Debug.LogWarning("Active items dictionary is null, cannot clear inventory.");
            return;
        }

        gridVisual.GridData.Clear();
        gridVisual.RefreshVisual();
        nextItemId = 1;
    }

    private void Update()
    {
        // Debug controls
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearInventory();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            if (testItems.Count > 0)
                CreateItemDataItems();

        }

        // Add some complex shapes directly
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            CreateRandomItem();
        }
    }



    private void CreateRandomItem()
    {
        int randomIndex = UnityEngine.Random.Range(0, testItems.Count);
        ItemInstanceData randomItemData = testItems[randomIndex];
        ItemData randomItem = randomItemData.itemData;

        CreateItem(randomItem);
    }

    // Public methods for external use
    public GridVisual GetGridVisual() => gridVisual;
    public Dictionary<string, DraggableGridItem> GetActiveItems() => activeItems;
    public GridData GetGridData() => gridVisual.GridData;

    // Helper method to create ItemData at runtime (useful for testing)
    public ItemData CreateRuntimeItemData(string itemName, TetrominoType shapeType, Sprite sprite = null)
    {
        ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
        itemData.itemName = itemName;
        itemData.shapeType = shapeType;
        itemData.itemSprite = sprite;
        itemData.spriteScale = Mathf.Clamp(itemData.spriteScale, 0.5f, 4.0f);
        itemData.spriteOffset = Vector2.zero;
        itemData.isRotatable = true;
        return itemData;
    }


}