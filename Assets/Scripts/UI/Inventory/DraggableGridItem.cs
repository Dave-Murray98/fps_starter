using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.InputSystem;
using BehaviorDesigner.Runtime.Tasks.Unity.UnityGameObject;

public class DraggableGridItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Settings")]
    [SerializeField] private bool canRotate = true;
    [SerializeField] private float snapAnimationDuration = 0.2f;

    private GridItem gridItem;
    private GridVisual gridVisual;
    private RectTransform rectTransform;
    private InventoryItemShapeRenderer shapeRenderer;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 originalPosition;
    private Vector2Int originalGridPosition;
    private int originalRotation; // Store original rotation state
    private bool isDragging = false;
    private bool wasValidPlacement = false;

    InputManager inputManager;

    public GridItem GridItem => gridItem;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        shapeRenderer = GetComponent<InventoryItemShapeRenderer>();
        canvasGroup = GetComponent<CanvasGroup>();
        inputManager = FindFirstObjectByType<InputManager>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvas = GetComponentInParent<Canvas>();
    }

    public void Initialize(GridItem item, GridVisual visual)
    {
        gridItem = item;
        gridVisual = visual;

        // Set consistent anchor to top-left
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);

        // Reset rotation transform
        transform.rotation = Quaternion.identity;

        // Initialize the shape renderer
        if (shapeRenderer == null)
        {
            shapeRenderer = gameObject.AddComponent<InventoryItemShapeRenderer>();
        }

        shapeRenderer.Initialize(gridItem);
        UpdatePosition();
        inputManager = FindFirstObjectByType<InputManager>();

        // Setup input action for rotation
        SetupRotationInput();
    }

    private void SetupRotationInput()
    {
        // Find the InputManager and get the UI action map
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed += OnRotateInput;
        }

    }


    private void OnDestroy()
    {
        CleanUp();
    }

    private void OnRotateInput()
    {
        // Only rotate if this item is being dragged
        if (isDragging && canRotate)
        {
            RotateItem();
        }
    }


    private void UpdatePosition()
    {
        if (rectTransform != null && gridItem != null && gridVisual != null)
        {
            // Position the item exactly where the preview showed it would be
            Vector2 gridPos = gridVisual.GetCellWorldPosition(gridItem.GridPosition.x, gridItem.GridPosition.y);
            rectTransform.localPosition = gridPos;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        originalPosition = rectTransform.localPosition;
        originalGridPosition = gridItem.GridPosition;
        originalRotation = gridItem.currentRotation; // Store original rotation state

        // Remove item from grid data
        gridVisual.GridData.RemoveItem(gridItem.ID);

        // Visual feedback
        canvasGroup.alpha = 0.8f;
        transform.SetAsLastSibling(); // Bring to front

        // DON'T call RefreshVisual here - it creates duplicates!
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Move the item with the mouse
        rectTransform.localPosition += (Vector3)(eventData.delta / canvas.scaleFactor);

        // Get grid position under mouse - use the GridVisual's coordinate system directly
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        // Check if placement is valid
        bool isValid = gridVisual.GridData.IsValidPosition(gridPos, gridItem);

        // Show placement preview
        gridVisual.ShowPlacementPreview(gridPos, gridItem, isValid);
        wasValidPlacement = isValid;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.alpha = 1f;

        Vector2Int targetGridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        // Try to place the item
        if (wasValidPlacement)
        {
            // Update item position and place it
            gridItem.SetGridPosition(targetGridPos);
            if (gridVisual.GridData.PlaceItem(gridItem))
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
            // Invalid placement - revert to original state
            RevertToOriginalState();
        }

        gridVisual.ClearPlacementPreview();
        // DON'T call RefreshVisual here either - causes duplicates!
    }

    private void RevertToOriginalState()
    {
        // Revert rotation if it was changed during drag
        if (gridItem.currentRotation != originalRotation)
        {
            gridItem.SetRotation(originalRotation);
            shapeRenderer.RefreshVisual(); // Update visual to match reverted rotation
        }

        // Restore original grid position
        gridItem.SetGridPosition(originalGridPosition);
        gridVisual.GridData.PlaceItem(gridItem);

        // Animate back to original position
        AnimateToOriginalPosition();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Click functionality can be added here if needed
        // Rotation is now handled by input system (R key)
    }

    private void RotateItem()
    {
        if (!canRotate || !isDragging) return;

        // Store the current center position before rotation
        Vector2 currentCenter = GetShapeCenter();

        // Rotate the item data
        gridItem.RotateItem();
        shapeRenderer.RefreshVisual();

        // Calculate the new position to keep the center in the same place
        Vector2 newCenter = GetShapeCenter();
        Vector2 offset = currentCenter - newCenter;
        rectTransform.localPosition += (Vector3)offset;

        // Update the drag preview based on current mouse position
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
        bool isValidPlacement = gridVisual.GridData.IsValidPosition(gridPos, gridItem);
        gridVisual.ShowPlacementPreview(gridPos, gridItem, isValidPlacement);
        wasValidPlacement = isValidPlacement;
        wasValidPlacement = isValidPlacement;
    }

    private Vector2 GetShapeCenter()
    {
        // Calculate the center of the current shape
        var shapeData = gridItem.CurrentShapeData;
        if (shapeData.cells.Length == 0)
            return rectTransform.localPosition;

        Vector2 center = Vector2.zero;
        foreach (var cell in shapeData.cells)
        {
            center += new Vector2(
                cell.x * (shapeRenderer.CellSize + shapeRenderer.CellSpacing),
                -cell.y * (shapeRenderer.CellSize + shapeRenderer.CellSpacing)
            );
        }
        center /= shapeData.cells.Length;

        return rectTransform.localPosition + new Vector3(center.x, center.y, 0);
    }

    private void AnimateToGridPosition()
    {
        Vector2 targetPos = gridVisual.GetCellWorldPosition(gridItem.GridPosition.x, gridItem.GridPosition.y);
        rectTransform.DOLocalMove(targetPos, snapAnimationDuration).SetEase(Ease.OutQuad);
    }

    private void AnimateToOriginalPosition()
    {
        rectTransform.DOLocalMove(originalPosition, snapAnimationDuration).SetEase(Ease.OutQuad);
    }

    private void CleanUp()
    {
        // Cleanup any references or listeners if needed
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed -= OnRotateInput;
        }
    }


}
