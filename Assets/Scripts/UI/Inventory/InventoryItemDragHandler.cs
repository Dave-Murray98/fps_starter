using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Enhanced drag handler with stats display integration and drop-outside-inventory functionality
/// UPDATED: Now triggers stats display on click/drag and handles dropping outside inventory
/// </summary>
public class InventoryItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Drag Settings")]
    [SerializeField] private bool canDrag = true;
    [SerializeField] private bool canRotate = true;
    [SerializeField] private float snapAnimationDuration = 0.2f;

    [Header("Dropdown Menu")]
    [SerializeField] private InventoryDropdownMenu dropdownMenu;

    [Header("Drop Outside Settings")]
    [SerializeField] private float dropOutsideBuffer = 50f; // How far outside inventory counts as "drop outside"

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
    private bool itemRemovedFromGrid = false;

    private InputManager inputManager;
    private InventoryManager persistentInventory;

    // NEW: Events for stats display
    public System.Action<InventoryItemData> OnItemSelected;
    public System.Action OnItemDeselected;

    // NEW: Track if we're outside inventory bounds during drag
    private bool isDraggedOutsideInventory = false;

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
        persistentInventory = InventoryManager.Instance;

        GetDropdownMenuFromManager();
        SetupRotationInput();
        SetupDropdownEvents();
        RegisterWithStatsDisplay();
    }

    /// <summary>
    /// Register this drag handler with the stats display system
    /// </summary>
    private void RegisterWithStatsDisplay()
    {
        if (ItemStatsDisplay.Instance != null)
        {
            ItemStatsDisplay.Instance.RegisterDragHandler(this);
        }
    }

    private void GetDropdownMenuFromManager()
    {
        if (dropdownMenu == null)
        {
            var dropdownManager = FindFirstObjectByType<InventoryDropdownManager>();
            if (dropdownManager != null)
            {
                dropdownMenu = dropdownManager.DropdownMenu;
            }
            else
            {
                Debug.LogWarning("[DragHandler] InventoryDropdownManager not found! Make sure it's added to your inventory UI.");
            }
        }
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

    private void SetupDropdownEvents()
    {
        if (dropdownMenu != null)
        {
            dropdownMenu.OnActionSelected += OnDropdownActionSelected;
        }
    }

    private void OnDestroy()
    {
        CleanupInput();
        CleanupDropdownEvents();
        UnregisterFromStatsDisplay();
    }

    private void CleanupInput()
    {
        if (inputManager != null)
        {
            inputManager.OnRotateInventoryItemPressed -= OnRotateInput;
        }
    }

    private void CleanupDropdownEvents()
    {
        if (dropdownMenu != null)
        {
            dropdownMenu.OnActionSelected -= OnDropdownActionSelected;
        }
    }

    /// <summary>
    /// Unregister from the stats display system
    /// </summary>
    private void UnregisterFromStatsDisplay()
    {
        if (ItemStatsDisplay.Instance != null)
        {
            ItemStatsDisplay.Instance.UnregisterDragHandler(this);
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            // NEW: Trigger stats display on left click
            TriggerStatsDisplay();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Show dropdown menu on right click
            ShowDropdownMenu(eventData.position);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!canDrag || itemData == null || persistentInventory == null) return;

        isDragging = true;
        isDraggedOutsideInventory = false; // Reset flag
        originalPosition = rectTransform.localPosition;
        originalGridPosition = itemData.GridPosition;
        originalRotation = itemData.currentRotation;
        itemRemovedFromGrid = false;

        // NEW: Show stats display while dragging
        TriggerStatsDisplay();

        // Remove the item from grid to prevent self-collision during drag
        if (gridVisual.GridData.GetItem(itemData.ID) != null)
        {
            gridVisual.GridData.RemoveItem(itemData.ID);
            itemRemovedFromGrid = true;
        }

        // Visual feedback
        canvasGroup.alpha = 0.8f;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isDragging || itemData == null || gridVisual == null) return;

        // Move the visual with the mouse
        rectTransform.localPosition += (Vector3)(eventData.delta / canvas.scaleFactor);

        // NEW: Check if we're outside inventory bounds
        CheckIfOutsideInventoryBounds();

        // Get grid position under mouse
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        // Create temporary item for testing position
        var tempItem = new InventoryItemData(itemData.ID + "_temp", itemData.ItemData, gridPos);
        tempItem.SetRotation(itemData.currentRotation);

        bool isValid = !isDraggedOutsideInventory && gridVisual.GridData.IsValidPosition(gridPos, tempItem);

        // Show placement preview
        if (!isDraggedOutsideInventory)
        {
            gridVisual.ShowPlacementPreview(gridPos, tempItem, isValid);
        }
        else
        {
            gridVisual.ClearPlacementPreview();
        }

        wasValidPlacement = isValid;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isDragging || itemData == null) return;

        isDragging = false;
        canvasGroup.alpha = 1f;

        // NEW: Handle drop outside inventory
        if (isDraggedOutsideInventory)
        {
            HandleDropOutsideInventory();
            return;
        }

        Vector2Int targetGridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        if (wasValidPlacement)
        {
            // Update item position and place it back in grid
            itemData.SetGridPosition(targetGridPos);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                AnimateToGridPosition();
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

    /// <summary>
    /// NEW: Check if the item is being dragged outside inventory bounds
    /// </summary>
    private void CheckIfOutsideInventoryBounds()
    {
        if (gridVisual == null) return;

        // Get the inventory grid's world bounds
        RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
        if (gridRect == null) return;

        // Convert item position to grid's local space
        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPosition);

        // Check if position is outside grid bounds (with buffer)
        Rect gridBounds = gridRect.rect;
        gridBounds.xMin -= dropOutsideBuffer;
        gridBounds.xMax += dropOutsideBuffer;
        gridBounds.yMin -= dropOutsideBuffer;
        gridBounds.yMax += dropOutsideBuffer;

        isDraggedOutsideInventory = !gridBounds.Contains(localPosition);

        // Visual feedback for dragging outside
        if (isDraggedOutsideInventory)
        {
            // Make item slightly more transparent when outside
            canvasGroup.alpha = 0.6f;
        }
        else
        {
            canvasGroup.alpha = 0.8f;
        }
    }

    /// <summary>
    /// NEW: Handle dropping item outside inventory (triggers item drop)
    /// FIXED: Properly restore item to inventory before dropping
    /// </summary>
    private void HandleDropOutsideInventory()
    {
        Debug.Log($"[DragHandler] Item {itemData.ItemData?.itemName} dropped outside inventory - attempting to drop into scene");

        // Check if item can be dropped
        if (itemData?.ItemData?.CanDrop != true)
        {
            Debug.LogWarning($"Cannot drop {itemData.ItemData?.itemName} - it's a key item");
            RevertToOriginalState();
            return;
        }

        // CRITICAL FIX: Restore item to inventory first if it was removed during drag
        if (itemRemovedFromGrid)
        {
            Debug.Log($"[DragHandler] Restoring item {itemData.ID} to inventory before dropping");

            // Restore to original position temporarily
            itemData.SetGridPosition(originalGridPosition);
            itemData.SetRotation(originalRotation);

            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                Debug.Log($"[DragHandler] Item {itemData.ID} restored to inventory successfully");
            }
            else
            {
                Debug.LogError($"[DragHandler] Failed to restore item {itemData.ID} to inventory before dropping!");
                // Try to force it back anyway
                gridVisual.GridData.RemoveItem(itemData.ID); // Clear any conflicts
                if (!gridVisual.GridData.PlaceItem(itemData))
                {
                    Debug.LogError($"[DragHandler] Could not restore item to inventory - aborting drop");
                    return;
                }
                itemRemovedFromGrid = false;
            }
        }

        // Now try to drop the item using ItemDropSystem
        bool success = ItemDropSystem.DropItemFromInventory(itemData.ID);

        if (success)
        {
            Debug.Log($"Successfully dropped {itemData.ItemData?.itemName} into scene");

            // Clear stats display since item is no longer in inventory
            OnItemDeselected?.Invoke();

            // Item has been removed from inventory and spawned in scene
            // The visual will be destroyed by the inventory system
        }
        else
        {
            Debug.LogWarning($"Failed to drop {itemData.ItemData?.itemName} - reverting to original position");
            RevertToOriginalState();
        }
    }

    /// <summary>
    /// NEW: Trigger stats display for this item
    /// </summary>
    private void TriggerStatsDisplay()
    {
        OnItemSelected?.Invoke(itemData);
    }

    private void RevertToOriginalState()
    {
        // Revert rotation if changed
        if (itemData.currentRotation != originalRotation)
        {
            itemData.SetRotation(originalRotation);
            visualRenderer?.RefreshVisual();
        }

        // Restore original position
        itemData.SetGridPosition(originalGridPosition);

        // Place item back in grid at original position
        if (itemRemovedFromGrid)
        {
            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
            }
            else
            {
                Debug.LogError($"[DragHandler] Failed to restore item {itemData.ID} to original position!");
            }
        }

        // Animate back to original position
        AnimateToOriginalPosition();
        visualRenderer.RefreshHotkeyIndicatorVisuals();
    }

    private void ShowDropdownMenu(Vector2 screenPosition)
    {
        if (dropdownMenu == null)
        {
            GetDropdownMenuFromManager();
        }

        if (dropdownMenu == null)
        {
            Debug.LogWarning("No dropdown menu available - falling back to direct drop");
            DropItem();
            return;
        }

        if (itemData?.ItemData == null)
        {
            Debug.LogWarning("Cannot show dropdown - no item data");
            return;
        }

        dropdownMenu.ShowMenu(itemData, screenPosition);
    }

    private void OnDropdownActionSelected(InventoryItemData selectedItem, string actionId)
    {
        if (selectedItem != itemData) return;

        switch (actionId)
        {
            case "consume":
                ConsumeItem();
                break;
            case "equip":
                EquipItem();
                break;
            case "assign_hotkey":
                AssignHotkey();
                break;
            case "unload":
                UnloadWeapon();
                break;
            case "drop":
                DropItem();
                break;
            default:
                Debug.LogWarning($"Unknown dropdown action: {actionId}");
                break;
        }
    }

    #region Dropdown Action Handlers

    private void ConsumeItem()
    {
        if (itemData?.ItemData?.itemType != ItemType.Consumable)
        {
            Debug.LogWarning("Cannot consume non-consumable item");
            return;
        }

        Debug.Log($"Consuming {itemData.ItemData.itemName}");

        var consumableData = itemData.ItemData.ConsumableData;
        if (consumableData != null)
        {
            Debug.Log($"Would restore: Health +{consumableData.healthRestore}, Hunger +{consumableData.hungerRestore}, Thirst +{consumableData.thirstRestore}");
        }

        // Remove item from inventory after consumption
        if (persistentInventory != null)
        {
            persistentInventory.RemoveItem(itemData.ID);
            // Clear stats display since item no longer exists
            OnItemDeselected?.Invoke();
        }
    }

    private void EquipItem()
    {
        if (itemData?.ItemData == null)
        {
            Debug.LogWarning("Cannot equip - no item data");
            return;
        }

        Debug.Log($"Equipping {itemData.ItemData.itemName}");

        if (EquippedItemManager.Instance != null)
        {
            bool success = EquippedItemManager.Instance.EquipItemFromInventory(itemData.ID);
            if (success)
            {
                Debug.Log($"Successfully equipped {itemData.ItemData.itemName}");
            }
            else
            {
                Debug.LogWarning($"Failed to equip {itemData.ItemData.itemName}");
            }
        }
        else
        {
            Debug.LogWarning("EquippedItemManager not found - cannot equip item");
        }
    }

    private void AssignHotkey()
    {
        if (itemData?.ItemData == null)
        {
            Debug.LogWarning("Cannot assign hotkey - no item data");
            return;
        }

        Debug.Log($"Assigning hotkey for {itemData.ItemData.itemName}");
        ShowHotkeySelectionUI();
    }

    private void ShowHotkeySelectionUI()
    {
        if (HotkeySelectionUI.Instance != null)
        {
            HotkeySelectionUI.Instance.ShowSelection(itemData);
        }
        else
        {
            AutoAssignToAvailableSlot();
        }
    }

    private void AutoAssignToAvailableSlot()
    {
        if (EquippedItemManager.Instance == null) return;

        var bindings = EquippedItemManager.Instance.GetAllHotkeyBindings();

        // Check if this item type is already assigned somewhere
        foreach (var binding in bindings)
        {
            if (binding.isAssigned)
            {
                var assignedItemData = binding.GetCurrentItemData();
                if (assignedItemData != null && assignedItemData.name == itemData.ItemData.name)
                {
                    bool success = EquippedItemManager.Instance.AssignItemToHotkey(itemData.ID, binding.slotNumber);
                    if (success)
                    {
                        Debug.Log($"Added {itemData.ItemData.itemName} to existing hotkey {binding.slotNumber} stack");
                    }
                    return;
                }
            }
        }

        // Find first empty slot
        foreach (var binding in bindings)
        {
            if (!binding.isAssigned)
            {
                bool success = EquippedItemManager.Instance.AssignItemToHotkey(itemData.ID, binding.slotNumber);
                if (success)
                {
                    Debug.Log($"Assigned {itemData.ItemData.itemName} to hotkey {binding.slotNumber}");
                }
                return;
            }
        }

        Debug.LogWarning("All hotkey slots are occupied - cannot auto-assign");
    }

    private void UnloadWeapon()
    {
        if (itemData?.ItemData?.itemType != ItemType.Weapon)
        {
            Debug.LogWarning("Cannot unload non-weapon item");
            return;
        }

        var weaponData = itemData.ItemData.WeaponData;
        if (weaponData == null || weaponData.currentAmmo <= 0)
        {
            Debug.LogWarning("No ammo to unload");
            return;
        }

        Debug.Log($"Unloading {weaponData.currentAmmo} rounds from {itemData.ItemData.itemName}");

        if (weaponData.requiredAmmoType != null)
        {
            Debug.Log($"Would add {weaponData.currentAmmo} {weaponData.requiredAmmoType.itemName} to inventory");
        }
    }

    private void DropItem()
    {
        // Check if item can be dropped
        if (itemData?.ItemData?.CanDrop != true)
        {
            Debug.LogWarning($"Cannot drop {itemData.ItemData.itemName} - it's a key item");
            return;
        }

        if (itemData?.ID == null)
        {
            Debug.LogWarning("Cannot drop item - no item data or ID");
            return;
        }

        // Use the ItemDropSystem
        bool success = ItemDropSystem.DropItemFromInventory(itemData.ID);

        if (success)
        {
            // Clear stats display since item no longer exists in inventory
            OnItemDeselected?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Failed to drop {itemData.ItemData?.itemName}");
        }
    }

    #endregion

    private void RotateItemDuringDrag()
    {
        if (!canRotate || !isDragging || itemData?.CanRotate != true) return;

        var currentRotation = itemData.currentRotation;
        var currentCenter = GetVisualCenter();

        int maxRotations = TetrominoDefinitions.GetRotationCount(itemData.shapeType);
        int newRotation = (currentRotation + 1) % maxRotations;

        itemData.SetRotation(newRotation);
        visualRenderer?.RefreshVisual();

        Vector2 newCenter = GetVisualCenter();
        Vector2 offset = currentCenter - newCenter;
        rectTransform.localPosition += (Vector3)offset;

        // Update preview with new rotation
        if (!isDraggedOutsideInventory)
        {
            Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
            bool isValidPlacement = gridVisual.GridData.IsValidPosition(gridPos, itemData);
            gridVisual.ShowPlacementPreview(gridPos, itemData, isValidPlacement);
            wasValidPlacement = isValidPlacement;
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

    public void SetDraggable(bool draggable)
    {
        canDrag = draggable;
    }

    public void SetRotatable(bool rotatable)
    {
        canRotate = rotatable;
    }
}