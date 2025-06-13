using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Visual representation of the inventory grid - subscribes to data changes
/// This only handles visuals and can be destroyed/recreated without losing data
/// </summary>
public class InventoryGridVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float cellSize = 50f;
    [SerializeField] private float cellSpacing = 2f;
    [SerializeField] private Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color validPreviewColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Prefabs")]
    [SerializeField] private GameObject itemVisualPrefab;
    [SerializeField] private GameObject previewCellPrefab;

    private RectTransform rectTransform;
    private Dictionary<string, GameObject> itemVisuals = new Dictionary<string, GameObject>();
    private List<GameObject> previewCells = new List<GameObject>();
    private List<Image> gridLines = new List<Image>();

    // Reference to persistent data
    private PersistentInventoryManager persistentInventory;
    private InventoryGridData currentGridData;

    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;
    public InventoryGridData GridData => currentGridData;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        CreatePreviewCellPrefab();
    }

    private void Start()
    {
        InitializeFromPersistentData();
    }

    private void OnEnable()
    {
        // Re-subscribe when enabled
        SubscribeToDataEvents();
        RefreshFromPersistentData();
    }

    private void OnDisable()
    {
        UnsubscribeFromDataEvents();
    }

    private void InitializeFromPersistentData()
    {
        persistentInventory = PersistentInventoryManager.Instance;
        if (persistentInventory == null)
        {
            Debug.LogError("PersistentInventoryManager not found! Make sure it exists in the scene.");
            return;
        }

        currentGridData = persistentInventory.InventoryData;
        SetupGrid();
        SubscribeToDataEvents();
        RefreshAllVisuals();
    }

    private void SubscribeToDataEvents()
    {
        if (persistentInventory != null)
        {
            persistentInventory.OnItemAdded -= OnItemAdded;
            persistentInventory.OnItemRemoved -= OnItemRemoved;
            persistentInventory.OnInventoryCleared -= OnInventoryCleared;
            persistentInventory.OnInventoryDataChanged -= OnInventoryDataChanged;

            persistentInventory.OnItemAdded += OnItemAdded;
            persistentInventory.OnItemRemoved += OnItemRemoved;
            persistentInventory.OnInventoryCleared += OnInventoryCleared;
            persistentInventory.OnInventoryDataChanged += OnInventoryDataChanged;
        }
    }

    private void UnsubscribeFromDataEvents()
    {
        if (persistentInventory != null)
        {
            persistentInventory.OnItemAdded -= OnItemAdded;
            persistentInventory.OnItemRemoved -= OnItemRemoved;
            persistentInventory.OnInventoryCleared -= OnInventoryCleared;
            persistentInventory.OnInventoryDataChanged -= OnInventoryDataChanged;
        }
    }

    private void RefreshFromPersistentData()
    {
        if (persistentInventory != null)
        {
            currentGridData = persistentInventory.InventoryData;
            RefreshAllVisuals();
        }
    }

    private void SetupGrid()
    {
        if (currentGridData == null) return;

        SetupGridSize();
        CreateGridLines();
    }

    private void SetupGridSize()
    {
        float totalWidth = currentGridData.Width * cellSize + (currentGridData.Width - 1) * cellSpacing;
        float totalHeight = currentGridData.Height * cellSize + (currentGridData.Height - 1) * cellSpacing;
        rectTransform.sizeDelta = new Vector2(totalWidth, totalHeight);
    }

    private void CreateGridLines()
    {
        // Clear existing grid lines
        foreach (var line in gridLines)
        {
            if (line != null)
                DestroyImmediate(line.gameObject);
        }
        gridLines.Clear();

        if (currentGridData == null) return;

        // Create vertical lines
        for (int x = 0; x <= currentGridData.Width; x++)
        {
            CreateGridLine(
                new Vector2(x * (cellSize + cellSpacing) - cellSpacing * 0.5f, 0),
                new Vector2(1, currentGridData.Height * cellSize + (currentGridData.Height - 1) * cellSpacing)
            );
        }

        // Create horizontal lines
        for (int y = 0; y <= currentGridData.Height; y++)
        {
            CreateGridLine(
                new Vector2(0, -y * (cellSize + cellSpacing) + cellSpacing * 0.5f),
                new Vector2(currentGridData.Width * cellSize + (currentGridData.Width - 1) * cellSpacing, 1)
            );
        }
    }

    private void CreateGridLine(Vector2 position, Vector2 size)
    {
        GameObject lineObj = new GameObject("GridLine");
        lineObj.transform.SetParent(transform, false);

        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0, 1);
        lineRect.anchorMax = new Vector2(0, 1);
        lineRect.pivot = new Vector2(0, 1);
        lineRect.anchoredPosition = position;
        lineRect.sizeDelta = size;

        Image lineImage = lineObj.AddComponent<Image>();
        lineImage.color = gridLineColor;
        lineImage.raycastTarget = false;

        gridLines.Add(lineImage);
    }

    private void CreatePreviewCellPrefab()
    {
        if (previewCellPrefab == null)
        {
            GameObject cell = new GameObject("PreviewCell");
            RectTransform cellRect = cell.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(cellSize, cellSize);
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);

            Image cellImage = cell.AddComponent<Image>();
            cellImage.color = validPreviewColor;
            cellImage.raycastTarget = false;

            previewCellPrefab = cell;
        }
    }

    // Data event handlers
    private void OnItemAdded(InventoryItemData item)
    {
        CreateItemVisual(item);
    }

    private void OnItemRemoved(string itemId)
    {
        if (itemVisuals.ContainsKey(itemId))
        {
            Destroy(itemVisuals[itemId]);
            itemVisuals.Remove(itemId);
        }
    }

    private void OnInventoryCleared()
    {
        foreach (var visual in itemVisuals.Values)
        {
            if (visual != null)
                Destroy(visual);
        }
        itemVisuals.Clear();
    }

    private void OnInventoryDataChanged(InventoryGridData newData)
    {
        currentGridData = newData;
        // We could add more sophisticated updates here if needed
    }

    private void RefreshAllVisuals()
    {
        // Clear existing visuals
        OnInventoryCleared();

        if (currentGridData == null) return;

        // Create visuals for all items
        var allItems = currentGridData.GetAllItems();
        foreach (var item in allItems)
        {
            CreateItemVisual(item);
        }
    }

    private void CreateItemVisual(InventoryItemData item)
    {
        if (itemVisuals.ContainsKey(item.ID))
        {
            Debug.LogWarning($"Visual already exists for item {item.ID}");
            return;
        }

        GameObject itemObj;

        if (itemVisualPrefab != null)
        {
            itemObj = Instantiate(itemVisualPrefab, transform);
        }
        else
        {
            itemObj = new GameObject($"Item_{item.ID}");
            itemObj.transform.SetParent(transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemVisualRenderer>();
            itemObj.AddComponent<InventoryItemDragHandler>();
        }

        // Initialize the visual components
        var renderer = itemObj.GetComponent<InventoryItemVisualRenderer>();
        if (renderer != null)
        {
            renderer.Initialize(item, this);
        }

        var dragHandler = itemObj.GetComponent<InventoryItemDragHandler>();
        if (dragHandler != null)
        {
            dragHandler.Initialize(item, this);
        }

        itemVisuals[item.ID] = itemObj;
    }

    // Preview system
    public void ShowPlacementPreview(Vector2Int gridPosition, InventoryItemData item, bool isValid)
    {
        ClearPlacementPreview();

        var previewPositions = item.GetOccupiedPositionsAt(gridPosition);
        Color previewColor = isValid ? validPreviewColor : invalidPreviewColor;

        foreach (var pos in previewPositions)
        {
            if (pos.x >= 0 && pos.x < currentGridData.Width && pos.y >= 0 && pos.y < currentGridData.Height)
            {
                GameObject previewCell = Instantiate(previewCellPrefab, transform);
                previewCell.name = $"Preview_{pos.x}_{pos.y}";

                RectTransform cellRect = previewCell.GetComponent<RectTransform>();
                cellRect.anchorMin = new Vector2(0, 1);
                cellRect.anchorMax = new Vector2(0, 1);
                cellRect.pivot = new Vector2(0, 1);

                Vector2 cellPos = GetCellWorldPosition(pos.x, pos.y);
                cellRect.anchoredPosition = cellPos;
                cellRect.sizeDelta = new Vector2(cellSize, cellSize);

                Image cellImage = previewCell.GetComponent<Image>();
                cellImage.color = previewColor;
                cellImage.raycastTarget = false;

                previewCells.Add(previewCell);
            }
        }
    }

    public void ClearPlacementPreview()
    {
        foreach (var cell in previewCells)
        {
            if (cell != null)
                Destroy(cell);
        }
        previewCells.Clear();
    }

    // Coordinate conversion
    public Vector2 GetCellWorldPosition(int x, int y)
    {
        return new Vector2(
            x * (cellSize + cellSpacing),
            -y * (cellSize + cellSpacing)
        );
    }

    public Vector2Int GetGridPosition(Vector2 localPosition)
    {
        int gridX = Mathf.FloorToInt(localPosition.x / (cellSize + cellSpacing));
        int gridY = Mathf.FloorToInt(-localPosition.y / (cellSize + cellSpacing));
        return new Vector2Int(gridX, gridY);
    }

    // Public methods for external use
    public bool TryAddItemAt(ItemData itemData, Vector2Int position)
    {
        return persistentInventory?.AddItem(itemData, position) ?? false;
    }

    public bool TryMoveItem(string itemId, Vector2Int newPosition)
    {
        return persistentInventory?.MoveItem(itemId, newPosition) ?? false;
    }

    public bool TryRotateItem(string itemId)
    {
        return persistentInventory?.RotateItem(itemId) ?? false;
    }

    [Button("Refresh Visuals")]
    public void ForceRefreshVisuals()
    {
        RefreshAllVisuals();
    }
}

