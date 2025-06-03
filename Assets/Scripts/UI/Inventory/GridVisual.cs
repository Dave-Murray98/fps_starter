using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GridVisual : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 10;
    [SerializeField] private int gridHeight = 8;
    [SerializeField] private float cellSize = 50f;
    [SerializeField] private float cellSpacing = 2f;

    [Header("Visual Settings")]
    [SerializeField] private Color emptyCellColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color validPlacementColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color invalidPlacementColor = new Color(1f, 0f, 0f, 0.3f);

    [Header("Prefabs")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private GameObject itemPrefab;

    private GridData gridData;
    private Transform gridParent;
    private Transform itemParent;
    private Dictionary<Vector2Int, Image> cellImages;
    private Dictionary<int, GameObject> itemVisuals;

    public GridData GridData => gridData;
    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;

    private void Awake()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        gridData = new GridData(gridWidth, gridHeight);
        cellImages = new Dictionary<Vector2Int, Image>();
        itemVisuals = new Dictionary<int, GameObject>();

        CreateGridParents();
        CreateGridCells();
    }

    private void CreateGridParents()
    {
        // Create parent for grid cells
        GameObject gridParentObj = new GameObject("GridCells");
        gridParentObj.AddComponent<RectTransform>();
        gridParentObj.transform.SetParent(transform, false);
        gridParent = gridParentObj.transform;

        // Create parent for items
        GameObject itemParentObj = new GameObject("GridItems");
        itemParentObj.AddComponent<RectTransform>();
        itemParentObj.transform.SetParent(transform, false);
        itemParent = itemParentObj.transform;
    }

    private void CreateGridCells()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                CreateCell(x, y);
            }
        }
    }

    private void CreateCell(int x, int y)
    {
        GameObject cell = cellPrefab != null ? Instantiate(cellPrefab, gridParent) : CreateDefaultCell();

        // Position the cell using RectTransform
        RectTransform cellRect = cell.GetComponent<RectTransform>();
        if (cellRect == null)
        {
            cellRect = cell.AddComponent<RectTransform>();
        }

        // Set anchor to top-left to match draggable items
        cellRect.anchorMin = new Vector2(0, 1);
        cellRect.anchorMax = new Vector2(0, 1);
        cellRect.pivot = new Vector2(0, 1);

        Vector2 position = GetCellWorldPosition(x, y);
        cellRect.localPosition = position;

        // Setup cell visual
        Image cellImage = cell.GetComponent<Image>();
        if (cellImage == null)
        {
            cellImage = cell.AddComponent<Image>();
        }

        cellImage.color = emptyCellColor;
        cellImages[new Vector2Int(x, y)] = cellImage;

        // Set cell size
        cellRect.sizeDelta = new Vector2(cellSize, cellSize);

        cell.name = $"Cell_{x}_{y}";
    }

    private GameObject CreateDefaultCell()
    {
        GameObject cell = new GameObject("Cell");
        cell.AddComponent<RectTransform>();
        cell.AddComponent<Image>();
        return cell;
    }

    public Vector2 GetCellWorldPosition(int x, int y)
    {
        float posX = x * (cellSize + cellSpacing);
        float posY = -y * (cellSize + cellSpacing); // Negative because UI Y grows downward
        return new Vector2(posX, posY);
    }

    public Vector2Int GetGridPosition(Vector2 worldPosition)
    {
        int x = Mathf.FloorToInt(worldPosition.x / (cellSize + cellSpacing));
        int y = Mathf.FloorToInt(-worldPosition.y / (cellSize + cellSpacing));
        return new Vector2Int(x, y);
    }

    public void ShowPlacementPreview(Vector2Int position, int width, int height, bool isValid)
    {
        ClearPlacementPreview();

        Color previewColor = isValid ? validPlacementColor : invalidPlacementColor;

        for (int x = position.x; x < position.x + width; x++)
        {
            for (int y = position.y; y < position.y + height; y++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                if (cellImages.ContainsKey(cellPos))
                {
                    cellImages[cellPos].color = previewColor;
                }
            }
        }
    }

    public void ClearPlacementPreview()
    {
        foreach (var kvp in cellImages)
        {
            Vector2Int pos = kvp.Key;
            Image cellImage = kvp.Value;

            if (gridData.GetCellValue(pos.x, pos.y) == 0)
            {
                cellImage.color = emptyCellColor;
            }
        }
    }

    public void RefreshVisual()
    {
        foreach (var kvp in cellImages)
        {
            Vector2Int pos = kvp.Key;
            Image cellImage = kvp.Value;

            int cellValue = gridData.GetCellValue(pos.x, pos.y);
            if (cellValue == 0)
            {
                cellImage.color = emptyCellColor;
            }
        }
    }
}