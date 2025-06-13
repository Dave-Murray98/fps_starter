using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Handles dragging of inventory items - works with data layer
/// </summary>
public class InventoryItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Drag Settings")]
    [SerializeField] private bool canDrag = true;
    [SerializeField] private bool canRotate = true;
    [SerializeField] private float snapAnimationDuration = 0.2f;

    private InventoryItemData itemData;
    private InventoryGridVisual gridVisual;
    private InventoryItemVisualRenderer visualRenderer;
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 originalPosition;
    private Vector2Int originalGridPosition;
    private int originalRotation;
    private bool isDragging = false;
    private bool wasValidPlacement = false;

    private InputManager inputManager;
    private PersistentInventoryManager persistentInventory;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        visualRenderer = GetComponent<InventoryItemVisualRenderer>();
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        inputManager = FindFirstObjectByType<InputManager>();
        persistentInventory = PersistentInventoryManager.Instance;
        SetupRotationInput();
    }

    public void Initialize(InventoryItemData item, InventoryGridVisual visual)
    {
        itemData = item;
        gridVisual = visual;
        UpdatePosition();
    }

    private void SetupRotationInput()
    {
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed += OnRotateInput;
        }
    }

    private void OnDestroy()
    {
        CleanupInput();
    }

    private void CleanupInput()
    {
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed -= OnRotateInput;
        }
    }

    private void OnRotateInput()
    {
        if (isDragging && canRotate && itemData?.CanRotate == true)
        {
            RotateItemDuringDrag();
        }
    }

    private void UpdatePosition()
    {
        if (rectTransform != null && itemData != null && gridVisual != null)
        {
            Vector2 gridPos = gridVisual.GetCellWorldPosition(itemData.GridPosition.x, itemData.GridPosition.y);
            rectTransform.localPosition = gridPos;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!canDrag || itemData == null || persistentInventory == null) return;

        isDragging = true;
        originalPosition = rectTransform.localPosition;
        originalGridPosition = itemData.GridPosition;
        originalRotation = itemData.currentRotation;

        // DON'T remove the item yet - just mark it as being dragged
        // The visual feedback and collision detection will handle the rest

        // Visual feedback
        canvasGroup.alpha = 0.8f;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || itemData == null || gridVisual == null) return;

        // Move the visual with the mouse
        rectTransform.localPosition += (Vector3)(eventData.delta / canvas.scaleFactor);

        // Get grid position under mouse
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        // Check if placement is valid by temporarily removing the item from grid
        // This prevents the item from colliding with itself
        bool wasItemInGrid = gridVisual.GridData.GetItem(itemData.ID) != null;
        if (wasItemInGrid)
        {
            gridVisual.GridData.RemoveItem(itemData.ID);
        }

        // Create temporary item for testing position
        var tempItem = new InventoryItemData(itemData.ID + "_temp", itemData.ItemData, gridPos);
        tempItem.SetRotation(itemData.currentRotation);

        bool isValid = gridVisual.GridData.IsValidPosition(gridPos, tempItem);

        // Restore the item to grid if it was there
        if (wasItemInGrid)
        {
            gridVisual.GridData.PlaceItem(itemData);
        }

        // Show placement preview
        gridVisual.ShowPlacementPreview(gridPos, tempItem, isValid);
        wasValidPlacement = isValid;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging || itemData == null) return;

        isDragging = false;
        canvasGroup.alpha = 1f;

        Vector2Int targetGridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        if (wasValidPlacement)
        {
            // Try to move the item using the persistent inventory manager
            if (persistentInventory.MoveItem(itemData.ID, targetGridPos))
            {
                // Successful placement
                AnimateToGridPosition();
            }
            else
            {
                // Failed to place - revert
                RevertToOriginalState();
            }
        }
        else
        {
            // Invalid placement - revert
            RevertToOriginalState();
        }

        gridVisual.ClearPlacementPreview();
    }

    private void RevertToOriginalState()
    {
        // Revert rotation if changed
        if (itemData.currentRotation != originalRotation)
        {
            itemData.SetRotation(originalRotation);
            visualRenderer?.RefreshVisual();
        }

        // Restore original position using the persistent inventory manager
        persistentInventory.MoveItem(itemData.ID, originalGridPosition);

        // Animate back to original position
        AnimateToOriginalPosition();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Handle click events if needed
    }

    private void RotateItemDuringDrag()
    {
        if (!canRotate || !isDragging || itemData?.CanRotate != true) return;

        // Store current center for rotation pivot
        Vector2 currentCenter = GetVisualCenter();

        // Try to rotate the item using the persistent inventory manager
        if (persistentInventory.RotateItem(itemData.ID))
        {
            // Rotation successful - update visual
            visualRenderer?.RefreshVisual();

            // Adjust position to keep center in same place
            Vector2 newCenter = GetVisualCenter();
            Vector2 offset = currentCenter - newCenter;
            rectTransform.localPosition += (Vector3)offset;

            // Update drag preview
            Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
            var tempItem = new InventoryItemData(itemData.ID, itemData.ItemData, gridPos);
            tempItem.SetRotation(itemData.currentRotation);

            bool isValidPlacement = gridVisual.GridData.IsValidPosition(gridPos, tempItem);
            gridVisual.ShowPlacementPreview(gridPos, tempItem, isValidPlacement);
            wasValidPlacement = isValidPlacement;
        }
        // If rotation failed, the persistent inventory manager handles reverting the state
    }

    private Vector2 GetVisualCenter()
    {
        var shapeData = itemData.CurrentShapeData;
        if (shapeData.cells.Length == 0)
            return rectTransform.localPosition;

        Vector2 center = Vector2.zero;
        foreach (var cell in shapeData.cells)
        {
            center += new Vector2(
                cell.x * (gridVisual.CellSize + gridVisual.CellSpacing),
                -cell.y * (gridVisual.CellSize + gridVisual.CellSpacing)
            );
        }
        center /= shapeData.cells.Length;

        return rectTransform.localPosition + new Vector3(center.x, center.y, 0);
    }

    private void AnimateToGridPosition()
    {
        Vector2 targetPos = gridVisual.GetCellWorldPosition(itemData.GridPosition.x, itemData.GridPosition.y);
        rectTransform.DOLocalMove(targetPos, snapAnimationDuration).SetEase(Ease.OutQuad);
    }

    private void AnimateToOriginalPosition()
    {
        rectTransform.DOLocalMove(originalPosition, snapAnimationDuration).SetEase(Ease.OutQuad);
    }

    // Public methods for external control
    public void SetDraggable(bool draggable)
    {
        canDrag = draggable;
    }

    public void SetRotatable(bool rotatable)
    {
        canRotate = rotatable;
    }
}