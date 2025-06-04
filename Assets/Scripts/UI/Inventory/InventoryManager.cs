using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("Grid Reference")]
    [SerializeField] private GridVisual gridVisual;

    [Header("Item Prefab")]
    [SerializeField] private GameObject draggableItemPrefab;

    [Header("Complex Shape Test Items")]
    [SerializeField] private List<TestItemData> testItems = new List<TestItemData>();

    private Dictionary<string, DraggableGridItem> activeItems;
    private int nextItemId = 1;

    [System.Serializable]
    public class TestItemData
    {
        public string itemName;
        public TetrominoType shapeType = TetrominoType.Single;
        [Range(1, 5)] public int quantity = 1;
    }

    private void Start()
    {
        activeItems = new Dictionary<string, DraggableGridItem>();

        if (gridVisual == null)
        {
            gridVisual = FindFirstObjectByType<GridVisual>();
        }

        // Add default test items if none are configured
        if (testItems.Count == 0)
        {
            AddDefaultTestItems();
        }

        CreateTestItems();
    }

    private void AddDefaultTestItems()
    {

        // Add complex shape items
        testItems.Add(new TestItemData { itemName = "Magic Crystal", shapeType = TetrominoType.Single, quantity = 2 });
        testItems.Add(new TestItemData { itemName = "Spell Scroll", shapeType = TetrominoType.Line2, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Tome", shapeType = TetrominoType.Square, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Staff", shapeType = TetrominoType.Line4, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Boomerang", shapeType = TetrominoType.LShape, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Holy Symbol", shapeType = TetrominoType.Comb, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Lightning Bolt", shapeType = TetrominoType.Corner, quantity = 1 });
    }

    private void CreateTestItems()
    {
        int currentX = 0;
        int currentY = 0;

        // Create complex shape items
        foreach (var complexItem in testItems)
        {
            for (int i = 0; i < complexItem.quantity; i++)
            {
                // Find a valid position for the complex shape
                Vector2Int position = FindValidPositionForShape(complexItem.shapeType, currentX, currentY);

                if (position.x != -1) // Valid position found
                {
                    CreateItem(complexItem.itemName, complexItem.shapeType, position);

                    // Update position for next item
                    var tempItem = new GridItem($"temp_{nextItemId}", complexItem.shapeType, position);
                    var bounds = tempItem.GetBoundingSize();
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
                    Debug.LogWarning($"Could not find valid position for {complexItem.itemName}");
                }
            }
        }
    }

    private Vector2Int FindValidPositionForShape(TetrominoType shapeType, int startX = 0, int startY = 0)
    {
        // Create a temporary item to test positioning
        var tempItem = new GridItem($"temp_{nextItemId}", shapeType, Vector2Int.zero);

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

    // Create complex shape items
    public DraggableGridItem CreateItem(string itemName, TetrominoType shapeType, Vector2Int? position = null)
    {
        // Create grid item data
        string itemId = $"item_{nextItemId}";
        GridItem gridItem = new GridItem(itemId, shapeType, Vector2Int.zero, itemName);

        // Find position if not provided
        Vector2Int itemPosition = position ?? FindValidPositionForShape(shapeType);

        if (itemPosition.x == -1)
        {
            Debug.LogError($"Cannot create item {itemName} - no valid position found");
            return null;
        }

        gridItem.SetGridPosition(itemPosition);

        // Place in grid data
        if (!gridVisual.GridData.PlaceItem(gridItem))
        {
            Debug.LogError($"Failed to place item {itemName} in grid data");
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

    // Legacy method for backwards compatibility
    public bool RemoveItem(int itemId)
    {
        return RemoveItem($"item_{itemId}");
    }

    public void ClearInventory()
    {
        foreach (var item in activeItems.Values)
        {
            if (item != null && item.gameObject != null)
            {
                DestroyImmediate(item.gameObject);
            }
        }

        activeItems.Clear();
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
            CreateTestItems();
        }

        // Add some complex shapes directly
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            CreateItem("Test Single", TetrominoType.Single);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            CreateItem("Test Line2", TetrominoType.Line2);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            CreateItem("Test Square", TetrominoType.Square);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            CreateItem("Test Line4", TetrominoType.Line4);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            CreateItem("Test L-Shape", TetrominoType.LShape);
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            CreateItem("Test Cross", TetrominoType.Comb);
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            CreateItem("Test Z-Shape", TetrominoType.Corner);
        }
    }

    // Public methods for external use
    public GridVisual GetGridVisual() => gridVisual;
    public Dictionary<string, DraggableGridItem> GetActiveItems() => activeItems;
    public GridData GetGridData() => gridVisual.GridData;
}