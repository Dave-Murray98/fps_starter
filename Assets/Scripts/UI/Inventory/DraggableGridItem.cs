using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.InputSystem;

public class DraggableGridItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Settings")]
    [SerializeField] private bool canRotate = true;
    [SerializeField] private float snapAnimationDuration = 0.2f;

    private GridItem gridItem;
    private GridVisual gridVisual;
    private RectTransform rectTransform;
    private Image itemImage;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Vector2 originalPosition;
    private Vector2Int originalGridPosition;
    private bool originalRotationState; // Store original rotation state
    private bool isDragging = false;
    private bool wasValidPlacement = false;

    // Input System
    private InputAction rotateAction;

    public GridItem GridItem => gridItem;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        itemImage = GetComponent<Image>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvas = GetComponentInParent<Canvas>();

        // Setup input action for rotation
        SetupRotationInput();
    }

    private void SetupRotationInput()
    {
        // Find the InputManager and get the UI action map
        var inputManager = FindObjectOfType<InputManager>();
        if (inputManager?.inputActions != null)
        {
            var uiActionMap = inputManager.inputActions.FindActionMap("UI");
            if (uiActionMap != null)
            {
                rotateAction = uiActionMap.FindAction("RotateInventoryItem");
                if (rotateAction != null)
                {
                    rotateAction.performed += OnRotateInput;
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (rotateAction != null)
        {
            rotateAction.performed -= OnRotateInput;
        }
    }

    private void OnRotateInput(InputAction.CallbackContext context)
    {
        // Only rotate if this item is being dragged
        if (isDragging)
        {
            RotateItem();
        }
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

        UpdateVisual();
        UpdateSize();
        UpdatePosition();
    }

    private void UpdateVisual()
    {
        if (itemImage != null && gridItem != null)
        {
            itemImage.color = gridItem.ItemColor;
        }
    }

    private void UpdateSize()
    {
        if (rectTransform != null && gridItem != null)
        {
            float width = gridItem.Width * gridVisual.CellSize + (gridItem.Width - 1) * gridVisual.CellSpacing;
            float height = gridItem.Height * gridVisual.CellSize + (gridItem.Height - 1) * gridVisual.CellSpacing;
            rectTransform.sizeDelta = new Vector2(width, height);
        }
    }

    private void UpdatePosition()
    {
        if (rectTransform != null && gridItem != null)
        {
            Vector2 worldPos = gridVisual.GetCellWorldPosition(gridItem.GridPosition.x, gridItem.GridPosition.y);
            rectTransform.localPosition = worldPos;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        originalPosition = rectTransform.localPosition;
        originalGridPosition = gridItem.GridPosition;
        originalRotationState = gridItem.IsRotated; // Store original rotation state

        // Remove item from grid data
        gridVisual.GridData.RemoveItem(gridItem.GridPosition.x, gridItem.GridPosition.y,
                                      gridItem.Width, gridItem.Height);

        // Visual feedback
        canvasGroup.alpha = 0.8f;
        transform.SetAsLastSibling(); // Bring to front

        gridVisual.RefreshVisual();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        // Move the item with the mouse
        rectTransform.localPosition += (Vector3)(eventData.delta / canvas.scaleFactor);

        // Get grid position under mouse
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        // Check if placement is valid
        bool isValid = gridVisual.GridData.IsValidPosition(gridPos.x, gridPos.y,
                                                          gridItem.Width, gridItem.Height);

        // Show placement preview
        gridVisual.ShowPlacementPreview(gridPos, gridItem.Width, gridItem.Height, isValid);
        wasValidPlacement = isValid;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;
        canvasGroup.alpha = 1f;

        Vector2Int targetGridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        // Try to place the item
        if (wasValidPlacement && gridVisual.GridData.PlaceItem(targetGridPos.x, targetGridPos.y,
                                                               gridItem.Width, gridItem.Height, gridItem.ID))
        {
            // Successful placement
            gridItem.SetGridPosition(targetGridPos);
            AnimateToGridPosition();
        }
        else
        {
            // Invalid placement - revert to original state completely
            RevertToOriginalState();
        }

        gridVisual.ClearPlacementPreview();
        gridVisual.RefreshVisual();
    }

    private void RevertToOriginalState()
    {
        // Revert rotation if it was changed during drag
        if (gridItem.IsRotated != originalRotationState)
        {
            gridItem.RotateItem(); // This will toggle it back to original state
            UpdateSize(); // Update visual size to match reverted rotation
        }

        // Restore original grid position
        gridVisual.GridData.PlaceItem(originalGridPosition.x, originalGridPosition.y,
                                     gridItem.Width, gridItem.Height, gridItem.ID);
        gridItem.SetGridPosition(originalGridPosition);

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

        // Store the current cursor position relative to the grid
        Vector2 mousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridVisual.GetComponent<RectTransform>(),
            Input.mousePosition,
            canvas.worldCamera,
            out mousePos);

        // Store current item center position before rotation
        Vector2 currentCenter = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, 0) + new Vector3(rectTransform.sizeDelta.x * 0.5f, -rectTransform.sizeDelta.y * 0.5f, 0);

        // Rotate the item data
        gridItem.RotateItem();
        UpdateSize();

        // Calculate the new position to keep the center in the same place
        Vector2 newTopLeft = currentCenter - new Vector2(
            rectTransform.sizeDelta.x * 0.5f,
            -rectTransform.sizeDelta.y * 0.5f);

        rectTransform.localPosition = newTopLeft;

        // Update the drag preview based on current mouse position
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
        bool isValidPlacement = gridVisual.GridData.IsValidPosition(gridPos.x, gridPos.y, gridItem.Width, gridItem.Height);
        gridVisual.ShowPlacementPreview(gridPos, gridItem.Width, gridItem.Height, isValidPlacement);
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
}