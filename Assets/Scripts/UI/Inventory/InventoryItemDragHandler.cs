using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Handles dragging of inventory items and dropdown menu interactions
/// UPDATED: Now shows dropdown menu on right-click instead of immediate drop
/// </summary>
public class InventoryItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("Drag Settings")]
    [SerializeField] private bool canDrag = true;
    [SerializeField] private bool canRotate = true;
    [SerializeField] private float snapAnimationDuration = 0.2f;

    [Header("Dropdown Menu")]
    [SerializeField] private InventoryDropdownMenu dropdownMenu;

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

        // Get dropdown menu from the InventoryDropdownManager instead of trying to find the inactive menu
        GetDropdownMenuFromManager();

        SetupRotationInput();
        SetupDropdownEvents();
    }

    /// <summary>
    /// Get the dropdown menu from the InventoryDropdownManager
    /// This works even when the dropdown menu is initially inactive
    /// </summary>
    private void GetDropdownMenuFromManager()
    {
        if (dropdownMenu == null)
        {
            var dropdownManager = FindFirstObjectByType<InventoryDropdownManager>();
            if (dropdownManager != null)
            {
                // Use the public property which ensures the dropdown menu is created
                dropdownMenu = dropdownManager.DropdownMenu;
                // Debug.Log($"[DragHandler] Got dropdown menu from manager: {dropdownMenu != null}");
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
        // Only allow dragging with left mouse button
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

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
            //            Debug.Log($"[DragHandler] Removed item {itemData.ID} from grid for dragging");
        }

        // Visual feedback
        canvasGroup.alpha = 0.8f;
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Only allow dragging with left mouse button
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

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
        // Only handle end drag for left mouse button
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

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
                // Debug.Log($"[DragHandler] Successfully placed item {itemData.ID} at {targetGridPos}");
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
        // IMPORTANT: This method reverts BOTH position AND rotation to the state before dragging started
        // Debug.Log($"[DragHandler] Reverting item {itemData.ID} to original state - Position: {originalGridPosition}, Rotation: {originalRotation}");

        // Revert rotation if changed (this happens BEFORE placing the item back)
        if (itemData.currentRotation != originalRotation)
        {
            itemData.SetRotation(originalRotation);
            visualRenderer?.RefreshVisual();
            // Debug.Log($"[DragHandler] Reverted rotation for item {itemData.ID} from {itemData.currentRotation} to {originalRotation}");
        }

        // Restore original position
        itemData.SetGridPosition(originalGridPosition);

        // Place item back in grid at original position with original rotation
        if (itemRemovedFromGrid)
        {
            if (gridVisual.GridData.PlaceItem(itemData))
            {
                itemRemovedFromGrid = false;
                //Debug.Log($"[DragHandler] Restored item {itemData.ID} to original position {originalGridPosition} with rotation {originalRotation}");
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
        // Handle right-click for dropdown menu
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // Use the actual mouse position from the event data
            ShowDropdownMenu(eventData.position);
        }
    }

    private void ShowDropdownMenu(Vector2 screenPosition)
    {
        // Try to get dropdown menu if we don't have it
        if (dropdownMenu == null)
        {
            GetDropdownMenuFromManager();
        }

        if (dropdownMenu == null)
        {
            Debug.LogWarning("No dropdown menu available - falling back to direct drop");
            Debug.LogWarning("Make sure InventoryDropdownManager is added to your inventory UI!");
            DropItem();
            return;
        }

        if (itemData?.ItemData == null)
        {
            Debug.LogWarning("Cannot show dropdown - no item data");
            return;
        }

        //Debug.Log($"Showing dropdown menu for {itemData.ItemData.itemName} at screen position {screenPosition}");
        dropdownMenu.ShowMenu(itemData, screenPosition);
    }

    private void OnDropdownActionSelected(InventoryItemData selectedItem, string actionId)
    {
        if (selectedItem != itemData)
        {
            //            Debug.LogWarning("Dropdown action for different item - ignoring");
            return;
        }

        //Debug.Log($"Processing dropdown action: {actionId} for item {itemData.ItemData.itemName}");

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

        // TODO: Implement consumption logic when you add player stats
        // For now, just remove the item from inventory

        var consumableData = itemData.ItemData.ConsumableData;
        if (consumableData != null)
        {
            Debug.Log($"Would restore: Health +{consumableData.healthRestore}, Hunger +{consumableData.hungerRestore}, Thirst +{consumableData.thirstRestore}");

            // Placeholder for actual consumption effects:
            // PlayerStatsManager.Instance.ModifyHealth(consumableData.healthRestore);
            // PlayerStatsManager.Instance.ModifyHunger(consumableData.hungerRestore);
            // PlayerStatsManager.Instance.ModifyThirst(consumableData.thirstRestore);
        }

        // Remove item from inventory after consumption
        if (persistentInventory != null)
        {
            persistentInventory.RemoveItem(itemData.ID);
        }
    }

    private void EquipItem()
    {
        Debug.Log($"Equipping {itemData.ItemData.itemName}");

        // TODO: Implement equipment logic when you add equipped item system
        // For now, just log what would happen

        switch (itemData.ItemData.itemType)
        {
            case ItemType.Weapon:
                Debug.Log($"Would equip weapon: {itemData.ItemData.itemName}");
                // EquippedItemManager.Instance.EquipWeapon(itemData);
                break;
            case ItemType.Equipment:
                Debug.Log($"Would equip equipment: {itemData.ItemData.itemName}");
                // EquippedItemManager.Instance.EquipTool(itemData);
                break;
            case ItemType.Consumable:
            case ItemType.Ammo:
            case ItemType.KeyItem:
                Debug.Log($"Would equip for quick use: {itemData.ItemData.itemName}");
                // EquippedItemManager.Instance.EquipQuickUse(itemData);
                break;
        }
    }

    private void AssignHotkey()
    {
        Debug.Log($"Assigning hotkey for {itemData.ItemData.itemName}");

        // TODO: Implement hotkey assignment when you add the hotkey system
        // For now, just log what would happen

        // This could open a hotkey selection UI or auto-assign to next available slot
        // HotkeyManager.Instance.ShowAssignmentUI(itemData);
        // or
        // HotkeyManager.Instance.AutoAssignHotkey(itemData);

        Debug.Log("Hotkey assignment system not yet implemented");
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

        // TODO: Implement ammo unloading when you add the weapon system
        // This should:
        // 1. Get the ammo type from weaponData.requiredAmmoType
        // 2. Try to add the ammo to existing stacks in inventory
        // 3. Create new ammo items if needed
        // 4. Set weaponData.currentAmmo to 0

        // Placeholder logic:
        if (weaponData.requiredAmmoType != null)
        {
            Debug.Log($"Would add {weaponData.currentAmmo} {weaponData.requiredAmmoType.itemName} to inventory");

            // AmmoManager.Instance.UnloadWeapon(itemData);
            // persistentInventory.AddAmmo(weaponData.requiredAmmoType, weaponData.currentAmmo);
            // weaponData.currentAmmo = 0;
        }
    }

    private void DropItem()
    {
        // Check if item can be dropped
        if (itemData?.ItemData?.CanDrop != true)
        {
            Debug.LogWarning($"Cannot drop {itemData.ItemData.itemName} - it's a key item");
            // Could show a message to the player here
            return;
        }

        if (itemData?.ID == null)
        {
            Debug.LogWarning("Cannot drop item - no item data or ID");
            return;
        }

        //        Debug.Log($"Dropping item: {itemData.ItemData?.itemName}");

        // Use the updated ItemDropSystem with unified state management
        bool success = ItemDropSystem.DropItemFromInventory(itemData.ID);

        if (success)
        {
            //            Debug.Log($"Successfully dropped {itemData.ItemData?.itemName} into scene");
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

        // Debug.Log($"[DragHandler] Attempting to rotate item {itemData.ID} from rotation {itemData.currentRotation}");

        // Store current state
        var currentRotation = itemData.currentRotation;
        var currentCenter = GetVisualCenter();

        // Calculate next rotation
        int maxRotations = TetrominoDefinitions.GetRotationCount(itemData.shapeType);
        int newRotation = (currentRotation + 1) % maxRotations;

        // ALWAYS apply the rotation - player should be able to rotate freely during drag
        itemData.SetRotation(newRotation);

        // Update visual immediately
        visualRenderer?.RefreshVisual();

        // Adjust position to keep center in same place
        Vector2 newCenter = GetVisualCenter();
        Vector2 offset = currentCenter - newCenter;
        rectTransform.localPosition += (Vector3)offset;

        // Update preview with new rotation and adjusted position
        Vector2Int gridPos = gridVisual.GetGridPosition(rectTransform.localPosition);
        bool isValidPlacement = gridVisual.GridData.IsValidPosition(gridPos, itemData);
        gridVisual.ShowPlacementPreview(gridPos, itemData, isValidPlacement);
        wasValidPlacement = isValidPlacement;

        // Debug.Log($"[DragHandler] Rotated item {itemData.ID} to rotation {newRotation} (placement valid: {isValidPlacement})");
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