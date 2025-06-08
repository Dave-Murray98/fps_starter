using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Sirenix.OdinInspector;

public class GridVisual : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float cellSize = 50f;
    [SerializeField] private float cellSpacing = 2f;

    [Header("Visual Settings")]
    [SerializeField] private Color gridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    [SerializeField] private Color validPreviewColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidPreviewColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("Prefabs")]
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private GameObject previewCellPrefab;

    private GridData gridData;
    private RectTransform rectTransform;
    private List<GameObject> itemVisuals = new List<GameObject>();
    private List<GameObject> previewCells = new List<GameObject>();
    private List<Image> gridLines = new List<Image>();

    public GridData GridData => gridData;
    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        gridData = new GridData(gridWidth, gridHeight);

        CreateGridLines();
        CreatePreviewCellPrefab();
        SetupGridSize();
    }

    private void CreatePreviewCellPrefab()
    {
        if (previewCellPrefab == null)
        {
            GameObject cell = new GameObject("PreviewCell");
            RectTransform cellRect = cell.AddComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(cellSize, cellSize);

            //set the anchor to top-left
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);

            Image cellImage = cell.AddComponent<Image>();
            cellImage.color = validPreviewColor;
            cellImage.raycastTarget = false; // Don't block mouse events

            previewCellPrefab = cell;
        }
    }

    private void SetupGridSize()
    {
        float totalWidth = gridWidth * cellSize + (gridWidth - 1) * cellSpacing;
        float totalHeight = gridHeight * cellSize + (gridHeight - 1) * cellSpacing;
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

        // Create vertical lines
        for (int x = 0; x <= gridWidth; x++)
        {
            CreateGridLine(
                new Vector2(x * (cellSize + cellSpacing) - cellSpacing * 0.5f, 0),
                new Vector2(1, gridHeight * cellSize + (gridHeight - 1) * cellSpacing)
            );
        }

        // Create horizontal lines - FIXED: Back to negative Y for UI coordinates
        for (int y = 0; y <= gridHeight; y++)
        {
            CreateGridLine(
                new Vector2(0, -y * (cellSize + cellSpacing) + cellSpacing * 0.5f),
                new Vector2(gridWidth * cellSize + (gridWidth - 1) * cellSpacing, 1)
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

    public void AddItem(ItemData itemData, Vector2Int gridPosition)
    {
        string itemID = System.Guid.NewGuid().ToString();
        GridItem item = new GridItem(itemID, itemData, gridPosition);

        if (gridData.PlaceItem(item))
        {
            CreateItemVisual(item);
        }
    }

    private void CreateItemVisual(GridItem item)
    {
        GameObject itemObj;

        if (itemPrefab != null)
        {
            itemObj = Instantiate(itemPrefab, transform);
        }
        else
        {
            itemObj = new GameObject($"Item_{item.ID}");
            itemObj.transform.SetParent(transform, false);
            itemObj.AddComponent<RectTransform>();
            itemObj.AddComponent<InventoryItemShapeRenderer>();
            itemObj.AddComponent<DraggableGridItem>();
        }

        // Initialize the draggable component
        DraggableGridItem draggable = itemObj.GetComponent<DraggableGridItem>();
        if (draggable != null)
        {
            draggable.Initialize(item, this);
        }

        itemVisuals.Add(itemObj);
    }

    public void RefreshVisual()
    {
        // FIXED: Only refresh if we're not in the middle of a drag operation
        bool anyItemDragging = false;
        foreach (var visual in itemVisuals)
        {
            if (visual != null)
            {
                var draggable = visual.GetComponent<DraggableGridItem>();
                if (draggable != null && draggable.GridItem != null)
                {
                    // Check if any item is currently being dragged (not in grid)
                    if (!gridData.GetAllItems().Contains(draggable.GridItem))
                    {
                        anyItemDragging = true;
                        break;
                    }
                }
            }
        }

        // Only do full refresh if nothing is being dragged
        if (!anyItemDragging)
        {
            // Clear existing item visuals
            foreach (var visual in itemVisuals)
            {
                if (visual != null)
                    Destroy(visual);
            }
            itemVisuals.Clear();

            // Recreate visuals for all items
            var allItems = gridData.GetAllItems();
            foreach (var item in allItems)
            {
                CreateItemVisual(item);
            }
        }
    }

    public void ShowPlacementPreview(Vector2Int gridPosition, GridItem item, bool isValid)
    {
        ClearPlacementPreview();

        var previewPositions = item.GetOccupiedPositionsAt(gridPosition);
        Color previewColor = isValid ? validPreviewColor : invalidPreviewColor;

        foreach (var pos in previewPositions)
        {
            if (pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight)
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

    public Vector2 GetCellWorldPosition(int x, int y)
    {
        return new Vector2(
            x * (cellSize + cellSpacing),
            -y * (cellSize + cellSpacing) // BACK to negative Y for UI coordinates
        );
    }

    public Vector2Int GetGridPosition(Vector2 localPosition)
    {
        // Convert local position (relative to GridVisual) to grid coordinates
        int gridX = Mathf.FloorToInt(localPosition.x / (cellSize + cellSpacing));
        int gridY = Mathf.FloorToInt(-localPosition.y / (cellSize + cellSpacing));

        return new Vector2Int(gridX, gridY);
    }

    [Button("Clear All Items")]
    public void ClearAllItems()
    {
        gridData.Clear();
        RefreshVisual();
    }
}