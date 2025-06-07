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
    [SerializeField] private float borderWidth = 0f;

    [Header("Image Overlay Settings")]
    [SerializeField] private float imagePadding = 4f; // Padding around the image within the shape bounds

    private List<GameObject> cellObjects = new List<GameObject>();
    private GameObject imageOverlay;
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
        UpdateImageOverlay();
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

        // Clear image overlay
        if (imageOverlay != null)
        {
            if (Application.isPlaying)
                Destroy(imageOverlay);
            else
                DestroyImmediate(imageOverlay);
            imageOverlay = null;
        }
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

            // Set cell color - use item's custom color
            Image cellImage = cell.GetComponent<Image>();
            if (cellImage != null)
            {
                cellImage.color = currentItem.ItemColor; // Now uses custom color
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
    }

    private void UpdateImageOverlay()
    {
        // Only create image overlay if item has a sprite
        if (currentItem?.ItemSprite == null) return;

        // Create image overlay GameObject
        imageOverlay = new GameObject("ImageOverlay");
        imageOverlay.transform.SetParent(transform, false);

        // Add RectTransform
        RectTransform imageRect = imageOverlay.AddComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0, 1);
        imageRect.anchorMax = new Vector2(0, 1);
        imageRect.pivot = new Vector2(0.5f, 0.5f); // Center pivot for easier positioning

        // Add Image component
        Image overlayImage = imageOverlay.AddComponent<Image>();
        overlayImage.sprite = currentItem.ItemSprite;
        overlayImage.raycastTarget = false; // Don't block mouse events

        // Calculate optimal size and position
        CalculateImageSizeAndPosition(imageRect, overlayImage);

        // Apply rotation based on current item rotation
        ApplyImageRotation(imageRect);

        // Ensure image is on top of cells
        imageOverlay.transform.SetAsLastSibling();
    }

    private void ApplyImageRotation(RectTransform imageRect)
    {
        if (currentItem == null) return;

        // Apply rotation based on current rotation state
        // Each rotation state represents a 90-degree clockwise rotation
        float rotationAngle = currentItem.currentRotation * -90f; // Negative for clockwise
        imageRect.localRotation = Quaternion.Euler(0, 0, rotationAngle);
    }

    private void CalculateImageSizeAndPosition(RectTransform imageRect, Image overlayImage)
    {
        if (currentItem?.ItemSprite == null) return;

        var shapeData = currentItem.CurrentShapeData;
        if (shapeData.cells.Length == 0) return;

        // Calculate the visual bounds of the shape
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

        // Calculate available space for the image (with padding)
        float availableWidth = (maxX - minX) - (imagePadding * 2);
        float availableHeight = (maxY - minY) - (imagePadding * 2);

        // Get sprite's natural aspect ratio
        Sprite sprite = currentItem.ItemSprite;
        float spriteAspect = sprite.rect.width / sprite.rect.height;

        // Calculate size maintaining aspect ratio
        float imageWidth, imageHeight;

        if (availableWidth / availableHeight > spriteAspect)
        {
            // Height is the limiting factor
            imageHeight = availableHeight;
            imageWidth = imageHeight * spriteAspect;
        }
        else
        {
            // Width is the limiting factor
            imageWidth = availableWidth;
            imageHeight = imageWidth / spriteAspect;
        }

        // Apply user-defined scale
        imageWidth *= currentItem.SpriteScale;
        imageHeight *= currentItem.SpriteScale;

        // Set size
        imageRect.sizeDelta = new Vector2(imageWidth, imageHeight);

        // Calculate center position of the shape
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;

        // Apply user-defined offset
        Vector2 offset = currentItem.SpriteOffset;
        offset.x *= cellSize; // Scale offset relative to cell size
        offset.y *= cellSize;

        // Position at center of shape with offset
        imageRect.anchoredPosition = new Vector2(centerX + offset.x, centerY + offset.y);
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
        // Update cell alpha
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

        // Update image overlay alpha
        if (imageOverlay != null)
        {
            Image overlayImage = imageOverlay.GetComponent<Image>();
            if (overlayImage != null)
            {
                Color color = overlayImage.color;
                color.a = alpha;
                overlayImage.color = color;
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