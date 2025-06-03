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

    private Dictionary<int, DraggableGridItem> activeItems;
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

    private void Start()
    {
        activeItems = new Dictionary<int, DraggableGridItem>();

        if (gridVisual == null)
        {
            gridVisual = FindFirstObjectByType<GridVisual>();
        }

        // Add some test items
        if (testItems.Count == 0)
        {
            AddDefaultTestItems();
        }

        CreateTestItems();
    }

    private void AddDefaultTestItems()
    {
        testItems.Add(new TestItemData { itemName = "Sword", width = 1, height = 3, itemColor = Color.red, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Shield", width = 2, height = 2, itemColor = Color.blue, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Potion", width = 1, height = 1, itemColor = Color.green, quantity = 3 });
        testItems.Add(new TestItemData { itemName = "Bow", width = 1, height = 2, itemColor = Color.yellow, quantity = 1 });
        testItems.Add(new TestItemData { itemName = "Armor", width = 2, height = 3, itemColor = Color.gray, quantity = 1 });
    }

    private void CreateTestItems()
    {
        int currentX = 0;
        int currentY = 0;

        foreach (var testItem in testItems)
        {
            for (int i = 0; i < testItem.quantity; i++)
            {
                // Find a valid position for the item
                Vector2Int position = FindValidPosition(testItem.width, testItem.height, currentX, currentY);

                if (position.x != -1) // Valid position found
                {
                    CreateItem(testItem.itemName, testItem.width, testItem.height, testItem.itemColor, position);
                    currentX = position.x + testItem.width;

                    // Move to next row if we're getting close to the edge
                    if (currentX >= gridVisual.GridData.Width - 2)
                    {
                        currentX = 0;
                        currentY = position.y + testItem.height;
                    }
                }
                else
                {
                    Debug.LogWarning($"Could not find valid position for {testItem.itemName}");
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

    public DraggableGridItem CreateItem(string itemName, int width, int height, Color itemColor, Vector2Int? position = null)
    {
        // Create grid item data
        GridItem gridItem = new GridItem(nextItemId, width, height, itemColor, itemName);

        // Find position if not provided
        Vector2Int itemPosition = position ?? FindValidPosition(width, height);

        if (itemPosition.x == -1)
        {
            Debug.LogError($"Cannot create item {itemName} - no valid position found");
            return null;
        }

        gridItem.SetGridPosition(itemPosition);

        // Place in grid data
        if (!gridVisual.GridData.PlaceItem(itemPosition.x, itemPosition.y, width, height, gridItem.ID))
        {
            Debug.LogError($"Failed to place item {itemName} in grid data");
            return null;
        }

        // Create visual representation
        GameObject itemObj = Instantiate(draggableItemPrefab, gridVisual.transform);
        DraggableGridItem draggableItem = itemObj.GetComponent<DraggableGridItem>();

        if (draggableItem == null)
        {
            draggableItem = itemObj.AddComponent<DraggableGridItem>();
        }

        // Initialize the draggable item
        draggableItem.Initialize(gridItem, gridVisual);

        // Store reference
        activeItems[gridItem.ID] = draggableItem;
        nextItemId++;

        return draggableItem;
    }

    public bool RemoveItem(int itemId)
    {
        if (activeItems.ContainsKey(itemId))
        {
            DraggableGridItem item = activeItems[itemId];
            GridItem gridItem = item.GridItem;

            // Remove from grid data
            gridVisual.GridData.RemoveItem(gridItem.GridPosition.x, gridItem.GridPosition.y,
                                          gridItem.Width, gridItem.Height);

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
        gridVisual.GridData.ClearGrid();
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
    }

    // Public methods for external use
    public GridVisual GetGridVisual() => gridVisual;
    public Dictionary<int, DraggableGridItem> GetActiveItems() => activeItems;
    public GridData GetGridData() => gridVisual.GridData;
}