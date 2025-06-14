using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Handles dragging of inventory items - works with data layer
/// FIXED: Proper handling of rotation during drag to prevent cell occupation issues
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
    private bool itemRemovedFromGrid = false; // Track if item is temporarily removed

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
        itemRemovedFromGrid = false;

        // Remove the item from grid to prevent self-collision during drag
        if (gridVisual.GridData.GetItem(itemData.ID) != null)
        {
            gridVisual.GridData.RemoveItem(itemData.ID);
            itemRemovedFromGrid = true;
            Debug.Log($"[DragHandler] Removed item {itemData.ID} from grid for dragging");
        }

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

        // Create temporary item for testing position (use current rotation state)
        var tempItem = new InventoryItemData(itemData.ID + "_temp", itemData.ItemData, gridPos);
        tempItem.SetRotation(itemData.currentRotation);

        bool isValid = gridVisual.GridData.IsValidPosition(gridPos, tempItem);

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
            // Update item position and place it back in grid
            itemData.SetGridPosition(targetGridPos);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                AnimateToGridPosition();
                Debug.Log($"[DragHandler] Successfully placed item {itemData.ID} at {targetGridPos}");
            }
            else
            {
                Debug.LogError($"[DragHandler] Failed to place item {itemData.ID} at {targetGridPos} - reverting");
                RevertToOriginalState();
            }
        }
        else
        {
            Debug.Log($"[DragHandler] Invalid placement for item {itemData.ID} - reverting");
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
            Debug.Log($"[DragHandler] Reverted rotation for item {itemData.ID} to {originalRotation}");
        }

        // Restore original position
        itemData.SetGridPosition(originalGridPosition);

        // Place item back in grid at original position
        if (itemRemovedFromGrid)
        {
            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                Debug.Log($"[DragHandler] Restored item {itemData.ID} to original position {originalGridPosition}");
            }
            else
            {
                Debug.LogError($"[DragHandler] Failed to restore item {itemData.ID} to original position!");
            }
        }

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

        Debug.Log($"[DragHandler] Attempting to rotate item {itemData.ID} from rotation {itemData.currentRotation}");

        // Store current state for rollback
        var currentRotation = itemData.currentRotation;
        var currentCenter = GetVisualCenter();

        // Calculate next rotation
        int maxRotations = TetrominoDefinitions.GetRotationCount(itemData.shapeType);
        int newRotation = (currentRotation + 1) % maxRotations;

        // Apply the rotation directly to the item data
        itemData.SetRotation(newRotation);

        // Test if the new rotation is valid at current mouse position
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        if (gridVisual.GridData.IsValidPosition(gridPos, itemData))
        {
            // Rotation is valid - update visual and preview
            visualRenderer?.RefreshVisual();

            // Adjust position to keep center in same place
            Vector2 newCenter = GetVisualCenter();
            Vector2 offset = currentCenter - newCenter;
            rectTransform.localPosition += (Vector3)offset;

            // Update preview with new rotation and adjusted position
            gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
            bool isValidPlacement = gridVisual.GridData.IsValidPosition(gridPos, itemData);
            gridVisual.ShowPlacementPreview(gridPos, itemData, isValidPlacement);
            wasValidPlacement = isValidPlacement;

            Debug.Log($"[DragHandler] Successfully rotated item {itemData.ID} to rotation {newRotation}");
        }
        else
        {
            // Rotation is invalid - revert
            itemData.SetRotation(currentRotation);
            Debug.Log($"[DragHandler] Rotation blocked for item {itemData.ID} - reverted to {currentRotation}");
        }
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