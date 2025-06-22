using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// FIXED: Enhanced drag handler with proper clothing slot integration
/// Now properly coordinates with ClothingSlotUI for seamless drag and drop
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
    [SerializeField] private float dropOutsideBuffer = 50f;

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

    // Events for stats display
    public System.Action<InventoryItemData> OnItemSelected;
    public System.Action OnItemDeselected;

    // FIXED: Track drag state for clothing integration
    private bool isDraggedOutsideInventory = false;
    private ClothingSlotUI lastHoveredClothingSlot = null;

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
            TriggerStatsDisplay();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            ShowDropdownMenu(eventData.position);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!canDrag || itemData == null || persistentInventory == null) return;

        isDragging = true;
        isDraggedOutsideInventory = false;
        lastHoveredClothingSlot = null;
        originalPosition = rectTransform.localPosition;
        originalGridPosition = itemData.GridPosition;
        originalRotation = itemData.currentRotation;
        itemRemovedFromGrid = false;

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

    /// <summary>
    /// FIXED: Enhanced drag detection with proper clothing slot coordination
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isDragging || itemData == null || gridVisual == null) return;

        // Move the visual with the mouse
        rectTransform.localPosition += (Vector3)(eventData.delta / canvas.scaleFactor);

        // Check what we're dragging over
        CheckDragOverTargets(eventData);
    }

    /// <summary>
    /// FIXED: Comprehensive drag target detection and feedback
    /// </summary>
    private void CheckDragOverTargets(PointerEventData eventData)
    {
        // First check if we're outside inventory bounds
        CheckIfOutsideInventoryBounds();

        // Clear previous clothing slot feedback
        if (lastHoveredClothingSlot != null)
        {
            ClothingSlotUI.ClearAllDragFeedback();
            lastHoveredClothingSlot = null;
        }

        // Check for clothing slot under pointer
        var currentClothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (currentClothingSlot != null)
        {
            // We're over a clothing slot
            lastHoveredClothingSlot = currentClothingSlot;

            // Provide visual feedback to the clothing slot
            ClothingSlotUI.HandleDragOverClothingSlot(eventData, itemData);

            // Clear inventory preview since we're over clothing
            gridVisual.ClearPlacementPreview();
            wasValidPlacement = false;
        }
        else if (!isDraggedOutsideInventory)
        {
            // We're over inventory - show inventory preview
            Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

            var tempItem = new InventoryItemData(itemData.ID + "_temp", itemData.ItemData, gridPos);
            tempItem.SetRotation(itemData.currentRotation);

            bool isValid = gridVisual.GridData.IsValidPosition(gridPos, tempItem);
            gridVisual.ShowPlacementPreview(gridPos, tempItem, isValid);
            wasValidPlacement = isValid;
        }
        else
        {
            // We're outside both inventory and clothing slots
            gridVisual.ClearPlacementPreview();
            wasValidPlacement = false;
        }
    }

    /// <summary>
    /// FIXED: Enhanced end drag handling with proper clothing slot detection
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!isDragging || itemData == null) return;

        isDragging = false;
        canvasGroup.alpha = 1f;

        // Clear any clothing slot feedback
        ClothingSlotUI.ClearAllDragFeedback();

        // FIXED: Check if we dropped on a clothing slot
        var droppedOnClothingSlot = ClothingDragDropHelper.GetClothingSlotUnderPointer(eventData);

        if (droppedOnClothingSlot != null && ClothingDragDropHelper.CanEquipToSlot(itemData, droppedOnClothingSlot))
        {
            Debug.Log($"[DragHandler] Attempting to equip {itemData.ItemData?.itemName} to clothing slot {droppedOnClothingSlot.TargetLayer}");

            // FIXED: Restore item to inventory first if it was removed during drag
            if (itemRemovedFromGrid)
            {
                itemData.SetGridPosition(originalGridPosition);
                itemData.SetRotation(originalRotation);

                if (gridVisual.GridData.PlaceItem(itemData))
                {
                    itemRemovedFromGrid = false;
                    Debug.Log($"[DragHandler] Restored item {itemData.ID} to inventory before equipment");
                }
                else
                {
                    Debug.LogError($"[DragHandler] Failed to restore item {itemData.ID} to inventory!");
                    RevertToOriginalState();
                    return;
                }
            }

            // Now attempt to equip using the helper
            bool success = ClothingDragDropHelper.HandleClothingSlotDrop(itemData, droppedOnClothingSlot);

            if (success)
            {
                Debug.Log($"[DragHandler] Successfully equipped {itemData.ItemData?.itemName} to {droppedOnClothingSlot.TargetLayer}");

                // Clear stats display since item is no longer in inventory
                OnItemDeselected?.Invoke();

                // The clothing system will handle removing the item from inventory
                // and the visual will be destroyed automatically
            }
            else
            {
                Debug.LogWarning($"[DragHandler] Failed to equip {itemData.ItemData?.itemName} to {droppedOnClothingSlot.TargetLayer}");
                RevertToOriginalState();
            }

            gridVisual.ClearPlacementPreview();
            return;
        }

        // Handle drop outside inventory
        if (isDraggedOutsideInventory)
        {
            HandleDropOutsideInventory();
            return;
        }

        // Original inventory placement logic
        Vector2Int targetGridPos = gridVisual.GetGridPosition(rectTransform.localPosition);

        if (wasValidPlacement)
        {
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
    /// Check if the item is being dragged outside inventory bounds
    /// </summary>
    private void CheckIfOutsideInventoryBounds()
    {
        if (gridVisual == null) return;

        RectTransform gridRect = gridVisual.GetComponent<RectTransform>();
        if (gridRect == null) return;

        Vector2 localPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridRect,
            RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rectTransform.position),
            canvas.worldCamera,
            out localPosition);

        Rect gridBounds = gridRect.rect;
        gridBounds.xMin -= dropOutsideBuffer;
        gridBounds.xMax += dropOutsideBuffer;
        gridBounds.yMin -= dropOutsideBuffer;
        gridBounds.yMax += dropOutsideBuffer;

        isDraggedOutsideInventory = !gridBounds.Contains(localPosition);

        // Visual feedback for dragging outside
        if (isDraggedOutsideInventory)
        {
            canvasGroup.alpha = 0.6f;
        }
        else
        {
            canvasGroup.alpha = 0.8f;
        }
    }

    /// <summary>
    /// FIXED: Handle dropping item outside inventory with proper restoration
    /// </summary>
    private void HandleDropOutsideInventory()
    {
        Debug.Log($"[DragHandler] Item {itemData.ItemData?.itemName} dropped outside inventory - attempting to drop into scene");

        if (itemData?.ItemData?.CanDrop != true)
        {
            Debug.LogWarning($"Cannot drop {itemData.ItemData?.itemName} - it's a key item");
            RevertToOriginalState();
            return;
        }

        // Restore item to inventory first if it was removed during drag
        if (itemRemovedFromGrid)
        {
            Debug.Log($"[DragHandler] Restoring item {itemData.ID} to inventory before dropping");

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
                gridVisual.GridData.RemoveItem(itemData.ID);
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
            OnItemDeselected?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Failed to drop {itemData.ItemData?.itemName} - reverting to original position");
            RevertToOriginalState();
        }
    }

    /// <summary>
    /// Trigger stats display for this item
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

        // Handle clothing wear actions
        if (actionId.StartsWith("wear_"))
        {
            string layerName = actionId.Substring(5);
            if (System.Enum.TryParse<ClothingLayer>(layerName, out ClothingLayer targetLayer))
            {
                WearInSlot(targetLayer);
            }
            return;
        }

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

        if (persistentInventory != null)
        {
            persistentInventory.RemoveItem(itemData.ID);
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

        bool success = ItemDropSystem.DropItemFromInventory(itemData.ID);

        if (success)
        {
            OnItemDeselected?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Failed to drop {itemData.ItemData?.itemName}");
        }
    }

    /// <summary>
    /// ENHANCED: Equips the item to the specified clothing layer with improved error handling
    /// </summary>
    private void WearInSlot(ClothingLayer targetLayer)
    {
        if (itemData?.ItemData?.itemType != ItemType.Clothing)
        {
            Debug.LogWarning("Cannot wear - not a clothing item");
            return;
        }

        if (ClothingManager.Instance == null)
        {
            Debug.LogWarning("ClothingManager not found - cannot equip clothing");
            return;
        }

        Debug.Log($"Equipping {itemData.ItemData.itemName} to {targetLayer}");

        var validation = ClothingInventoryUtilities.ValidateClothingEquip(itemData, targetLayer);
        if (!validation.IsValid)
        {
            Debug.LogWarning($"Cannot equip {itemData.ItemData.itemName} to {targetLayer}: {validation.Message}");
            return;
        }

        var slot = ClothingManager.Instance.GetSlot(targetLayer);
        if (slot != null && !slot.IsEmpty)
        {
            var swapValidation = ClothingInventoryUtilities.ValidateSwapOperation(itemData, targetLayer);
            if (!swapValidation.IsValid)
            {
                Debug.LogWarning($"Cannot swap {itemData.ItemData.itemName}: {swapValidation.Message}");
                return;
            }
        }

        bool success = ClothingManager.Instance.EquipItemToLayer(itemData.ID, targetLayer);
        if (success)
        {
            Debug.Log($"Successfully equipped {itemData.ItemData.itemName} to {targetLayer}");
            OnItemDeselected?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Failed to equip {itemData.ItemData.itemName} to {targetLayer}");
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

        if (!isDraggedOutsideInventory && lastHoveredClothingSlot == null)
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