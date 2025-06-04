using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("Grid Reference")]
    [SerializeField] private GridVisual gridVisual;

    [Header("Item Prefab")]
    [SerializeField] private GameObject draggableItemPrefab;

    [Header("Test Items")]
    [SerializeField] private List<TestItemData> testItems = new List<TestItemData>();

    [Header("Complex Shape Test Items")]
    [SerializeField] private List<ComplexTestItemData> complexTestItems = new List<ComplexTestItemData>();

    private Dictionary<string, DraggableGridItem> activeItems;
    private int nextItemId = 1;

    [System.Serializable]
    public class TestItemData
    {
        public string itemName;
        public int width = 1;
        public int height = 1;
        public Color itemColor = Color.white;
        [Range(1, 5)] public int quantity = 1;
    }

    [System.Serializable]
    public class ComplexTestItemData
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
        if (testItems.Count == 0 && complexTestItems.Count == 0)
        {
            AddDefaultTestItems();
        }

        CreateTestItems();
    }

    private void AddDefaultTestItems()
    {
        // Add some traditional rectangular items for backwards compatibility
        testItems.Add(new TestItemData { itemName = "Sword", width = 1, height = 3, itemColor = Color.red, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Shield", width = 2, height = 2, itemColor = Color.blue, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Potion", width = 1, height = 1, itemColor = Color.green, quantity = 2 });

        // Add complex shape items
        complexTestItems.Add(new ComplexTestItemData { itemName = "Magic Crystal", shapeType = TetrominoType.Single, quantity = 2 });
        complexTestItems.Add(new ComplexTestItemData { itemName = "Spell Scroll", shapeType = TetrominoType.Line2, quantity = 1 });
        complexTestItems.Add(new ComplexTestItemData { itemName = "Tome", shapeType = TetrominoType.Square, quantity = 1 });
        complexTestItems.Add(new ComplexTestItemData { itemName = "Staff", shapeType = TetrominoType.Line4, quantity = 1 });
        complexTestItems.Add(new ComplexTestItemData { itemName = "Boomerang", shapeType = TetrominoType.LShape, quantity = 1 });
        complexTestItems.Add(new ComplexTestItemData { itemName = "Holy Symbol", shapeType = TetrominoType.Cross, quantity = 1 });
        complexTestItems.Add(new ComplexTestItemData { itemName = "Lightning Bolt", shapeType = TetrominoType.ZShape, quantity = 1 });
    }

    private void CreateTestItems()
    {
        int currentX = 0;
        int currentY = 0;

        // Create traditional rectangular items
        foreach (var testItem in testItems)
        {
            for (int i = 0; i < testItem.quantity; i++)
            {
                // Find a valid position for the item
                Vector2Int position = FindValidPosition(testItem.width, testItem.height, currentX, currentY);

                if (position.x != -1) // Valid position found
                {
                    CreateRectangularItem(testItem.itemName, testItem.width, testItem.height, testItem.itemColor, position);
                    currentX = position.x + testItem.width;

                    // Move to next row if we're getting close to the edge
                    if (currentX >= gridVisual.GridData.Width - 3)
                    {
                        currentX = 0;
                        currentY += testItem.height + 1;
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find valid position for {testItem.itemName}");
                }
            }
        }

        // Create complex shape items
        foreach (var complexItem in complexTestItems)
        {
            for (int i = 0; i < complexItem.quantity; i++)
            {
                // Find a valid position for the complex shape
                Vector2Int position = FindValidPositionForShape(complexItem.shapeType, currentX, currentY);

                if (position.x != -1) // Valid position found
                {
                    CreateComplexItem(complexItem.itemName, complexItem.shapeType, position);

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

    private Vector2Int FindValidPosition(int width, int height, int startX = 0, int startY = 0)
    {
        // Try to find a valid position starting from the given coordinates
        for (int y = startY; y < gridVisual.GridData.Height; y++)
        {
            for (int x = (y == startY ? startX : 0); x < gridVisual.GridData.Width; x++)
            {
                if (gridVisual.GridData.IsValidPosition(x, y, width, height))
                {
                    return new Vector2Int(x, y);
                }
            }
        }

        return new Vector2Int(-1, -1); // No valid position found
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

    // Create traditional rectangular items (backwards compatibility)
    public DraggableGridItem CreateRectangularItem(string itemName, int width, int height, Color itemColor, Vector2Int? position = null)
    {
        // Create grid item data using the legacy rectangular approach
        string itemId = $"item_{nextItemId}";
        GridItem gridItem = GridItem.CreateRectangular(itemId, width, height, itemColor, Vector2Int.zero, itemName);

        // Find position if not provided
        Vector2Int itemPosition = position ?? FindValidPosition(width, height);

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

    // Create complex shape items
    public DraggableGridItem CreateComplexItem(string itemName, TetrominoType shapeType, Vector2Int? position = null)
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
            CreateComplexItem("Test Single", TetrominoType.Single);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            CreateComplexItem("Test Line2", TetrominoType.Line2);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            CreateComplexItem("Test Square", TetrominoType.Square);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            CreateComplexItem("Test Line4", TetrominoType.Line4);
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            CreateComplexItem("Test L-Shape", TetrominoType.LShape);
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            CreateComplexItem("Test Cross", TetrominoType.Cross);
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            CreateComplexItem("Test Z-Shape", TetrominoType.ZShape);
        }
    }

    // Public methods for external use
    public GridVisual GetGridVisual() => gridVisual;
    public Dictionary<string, DraggableGridItem> GetActiveItems() => activeItems;
    public GridData GetGridData() => gridVisual.GridData;
}