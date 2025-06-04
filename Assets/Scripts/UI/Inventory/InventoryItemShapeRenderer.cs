using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryItemShapeRenderer : MonoBehaviour
{
    [Header("Cell Prefab")]
    [SerializeField] private GameObject cellPrefab;

    [Header("Visual Settings")]
    [SerializeField] private float cellSize = 50f;
    [SerializeField] private float cellSpacing = 2f;
    [SerializeField] private Color borderColor = Color.black;
    [SerializeField] private float borderWidth = 2f;

    private List<GameObject> cellObjects = new List<GameObject>();
    private GridItem currentItem;
    private RectTransform rectTransform;
    private Canvas parentCanvas;

    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        // Create cell prefab if not assigned
        if (cellPrefab == null)
        {
            CreateDefaultCellPrefab();
        }
    }

    private void CreateDefaultCellPrefab()
    {
        // Create a simple cell prefab programmatically
        GameObject cell = new GameObject("Cell");

        // Add RectTransform
        RectTransform cellRect = cell.AddComponent<RectTransform>();
        cellRect.sizeDelta = new Vector2(cellSize, cellSize);

        //set the anchor to top-left
        cellRect.anchorMin = new Vector2(0, 1);
        cellRect.anchorMax = new Vector2(0, 1);
        cellRect.pivot = new Vector2(0, 1);

        // Add Image component
        Image cellImage = cell.AddComponent<Image>();
        cellImage.color = Color.white;

        // Add border (using Outline component)
        Outline outline = cell.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(borderWidth, borderWidth);

        cellPrefab = cell;
    }

    public void Initialize(GridItem item)
    {
        currentItem = item;

        // Set the cell size and spacing from the grid visual
        var gridVisual = GetComponentInParent<GridVisual>();
        if (gridVisual != null)
        {
            cellSize = gridVisual.CellSize;
            cellSpacing = gridVisual.CellSpacing;
        }

        RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (currentItem == null) return;

        ClearCells();
        CreateCells();
        UpdateLayout();
    }

    private void ClearCells()
    {
        foreach (var cell in cellObjects)
        {
            if (cell != null)
            {
                if (Application.isPlaying)
                    Destroy(cell);
                else
                    DestroyImmediate(cell);
            }
        }
        cellObjects.Clear();
    }

    private void CreateCells()
    {
        var shapeData = currentItem.CurrentShapeData;

        foreach (var cellPos in shapeData.cells)
        {
            GameObject cell = Instantiate(cellPrefab, transform);
            cell.name = $"Cell_{cellPos.x}_{cellPos.y}";

            // Set up the cell
            RectTransform cellRect = cell.GetComponent<RectTransform>();
            cellRect.sizeDelta = new Vector2(cellSize, cellSize);

            // Ensure cell uses same anchoring
            cellRect.anchorMin = new Vector2(0, 1);
            cellRect.anchorMax = new Vector2(0, 1);
            cellRect.pivot = new Vector2(0, 1);

            // Set cell color
            Image cellImage = cell.GetComponent<Image>();
            if (cellImage != null)
            {
                cellImage.color = shapeData.color;
            }

            // Position the cell - BACK to negative Y for UI coordinates
            Vector2 position = new Vector2(
                cellPos.x * (cellSize + cellSpacing),
                -cellPos.y * (cellSize + cellSpacing) // BACK to negative Y
            );
            cellRect.anchoredPosition = position;

            cellObjects.Add(cell);
        }
    }

    private void UpdateLayout()
    {
        if (currentItem == null) return;

        var shapeData = currentItem.CurrentShapeData;
        if (shapeData.cells.Length == 0) return;

        // Calculate bounds based on actual cell positions
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var cellPos in shapeData.cells)
        {
            float cellX = cellPos.x * (cellSize + cellSpacing);
            float cellY = -cellPos.y * (cellSize + cellSpacing);

            minX = Mathf.Min(minX, cellX);
            maxX = Mathf.Max(maxX, cellX + cellSize);
            minY = Mathf.Min(minY, cellY);
            maxY = Mathf.Max(maxY, cellY + cellSize);
        }

        // Update container size
        Vector2 totalSize = new Vector2(maxX - minX, maxY - minY);
        rectTransform.sizeDelta = totalSize;
        rectTransform.pivot = new Vector2(0, 1);

        // DO NOT reposition cells - they should stay at their grid-relative positions
        // The container positioning will be handled by UpdatePosition() to match the preview exactly
    }

    public void UpdateCellColor(Color color)
    {
        foreach (var cell in cellObjects)
        {
            if (cell != null)
            {
                Image cellImage = cell.GetComponent<Image>();
                if (cellImage != null)
                {
                    cellImage.color = color;
                }
            }
        }
    }

    public void SetAlpha(float alpha)
    {
        foreach (var cell in cellObjects)
        {
            if (cell != null)
            {
                Image cellImage = cell.GetComponent<Image>();
                if (cellImage != null)
                {
                    Color color = cellImage.color;
                    color.a = alpha;
                    cellImage.color = color;
                }
            }
        }
    }

    // Get the world position of a specific cell
    public Vector2 GetCellWorldPosition(int x, int y)
    {
        Vector2 position = new Vector2(
            x * (cellSize + cellSpacing),
            -y * (cellSize + cellSpacing) // BACK to negative Y
        );

        return rectTransform.TransformPoint(position);
    }

    // Convert world position to grid position
    public Vector2Int GetGridPosition(Vector2 worldPos)
    {
        Vector2 localPos = rectTransform.InverseTransformPoint(worldPos);

        int gridX = Mathf.FloorToInt(localPos.x / (cellSize + cellSpacing));
        int gridY = Mathf.FloorToInt(-localPos.y / (cellSize + cellSpacing)); // BACK to negative Y

        return new Vector2Int(gridX, gridY);
    }

    // Update the item being rendered
    public void SetItem(GridItem item)
    {
        currentItem = item;
        RefreshVisual();
    }

    public GridItem GetItem()
    {
        return currentItem;
    }
}