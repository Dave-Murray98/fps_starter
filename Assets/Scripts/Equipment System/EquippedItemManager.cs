using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Manages the equipment system for the player, handling which item is currently equipped
/// and which items are assigned to hotkey slots (1-0 keys). Provides functionality for:
/// - Equipping items directly from inventory
/// - Assigning items to 10 hotkey slots with automatic stacking for consumables
/// - Cycling through equipped items with mouse wheel
/// - Performing context-sensitive actions based on equipped item type
/// - Automatic cleanup when items are removed from inventory
/// 
/// This manager coordinates with InventoryManager via shared item IDs and fires events
/// that UI components subscribe to for visual updates. Save/load is handled by
/// EquipmentSaveComponent to maintain separation of concerns.
/// </summary>
public class EquippedItemManager : MonoBehaviour
{
    public static EquippedItemManager Instance { get; private set; }

    [Header("Equipment State")]
    [SerializeField, ReadOnly] private EquipmentSaveData equipmentData;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float scrollCooldown = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip hotkeySound;

    // Component references - found automatically or via events
    private InventoryManager inventoryManager;

    // Input timing control
    private float lastScrollTime = 0f;

    // Events that UI systems subscribe to for updates
    public System.Action<EquippedItemData> OnItemEquipped;
    public System.Action OnItemUnequipped;
    public System.Action<int, HotkeyBinding> OnHotkeyAssigned;
    public System.Action<int> OnHotkeyCleared;
    public System.Action<ItemType, bool> OnItemActionPerformed;

    // Public accessors for external systems
    public EquippedItemData CurrentEquippedItem => equipmentData.equippedItem;
    public bool HasEquippedItem => equipmentData.equippedItem.isEquipped;
    public ItemData GetEquippedItemData() => equipmentData.equippedItem.GetItemData();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Initializes the equipment system with empty state (10 unassigned hotkey slots, no equipped item).
    /// </summary>
    private void Initialize()
    {
        equipmentData = new EquipmentSaveData();
        DebugLog("EquippedItemManager initialized with empty equipment state");
    }

    private void Start()
    {
        inventoryManager = InventoryManager.Instance;

        // Subscribe to system events for automatic reference updates
        GameManager.OnManagersRefreshed += RefreshReferences;
        InputManager.OnInputManagerReady += OnInputManagerReady;

        // Subscribe to inventory events for automatic equipment cleanup
        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved += OnInventoryItemRemoved;
            inventoryManager.OnItemAdded += OnInventoryItemAdded;
        }

        RefreshReferences();
        SetupInputHandlers();
    }

    /// <summary>
    /// Updates component references after scene changes or manager initialization.
    /// </summary>
    private void RefreshReferences()
    {
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
        }
    }

    /// <summary>
    /// Handles InputManager becoming available and sets up input subscriptions.
    /// </summary>
    private void OnInputManagerReady(InputManager newInputManager)
    {
        SetupInputHandlers();
    }

    #region Input Handling

    /// <summary>
    /// Subscribes to input events from InputManager for equipment controls.
    /// </summary>
    private void SetupInputHandlers()
    {
        InputManager.Instance.OnScrollWheelInput += HandleScrollInput;
        InputManager.Instance.OnHotkeyPressed += OnHotkeyPressed;

    }

    /// <summary>
    /// Handles hotkey press events (1-0 keys) by activating the corresponding slot.
    /// </summary>
    private void OnHotkeyPressed(int slotNumber)
    {
        ActivateHotkey(slotNumber);
    }

    /// <summary>
    /// Processes mouse scroll wheel input for cycling through equipped items.
    /// Only functions when inventory UI is closed to prevent conflicts.
    /// Uses cooldown to prevent overly rapid cycling from scroll spam.
    /// </summary>
    public void HandleScrollInput(Vector2 scrollDelta)
    {
        // Prevent scroll spam with cooldown
        if (Time.time - lastScrollTime < scrollCooldown)
            return;

        // Only cycle when inventory is closed
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true)
            return;

        // Process significant scroll input
        if (Mathf.Abs(scrollDelta.y) > 0.1f)
        {
            lastScrollTime = Time.time;
            CycleEquippedItem(scrollDelta.y > 0);
        }
    }

    private void Update()
    {
        // Fallback input handling when InputManager is unavailable
        if (InputManager.Instance == null)
        {
            HandleHotkeyInputs();
        }

        HandleItemActionInputs();
    }

    /// <summary>
    /// Fallback hotkey input detection using Unity's legacy input system.
    /// Checks keys 1-0 for hotkey activation when InputManager is unavailable.
    /// </summary>
    private void HandleHotkeyInputs()
    {
        for (int i = 1; i <= 10; i++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
            if (i == 10) key = KeyCode.Alpha0; // 0 key represents slot 10

            if (Input.GetKeyDown(key))
            {
                ActivateHotkey(i);
            }
        }
    }

    /// <summary>
    /// Detects mouse clicks for item actions when inventory is closed and an item is equipped.
    /// Left click and right click perform different actions based on equipped item type.
    /// </summary>
    private void HandleItemActionInputs()
    {
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true || !HasEquippedItem)
            return;

        if (Input.GetMouseButtonDown(0)) // Left click
        {
            PerformItemAction(true);
        }
        else if (Input.GetMouseButtonDown(1)) // Right click
        {
            PerformItemAction(false);
        }
    }

    #endregion

    #region Equipment Management

    /// <summary>
    /// Equips an item directly from the inventory by its ID. The item must exist in the
    /// inventory for this to succeed. Replaces any currently equipped item.
    /// Fires OnItemEquipped event for UI updates.
    /// </summary>
    /// <param name="itemId">Unique ID of the item in the inventory</param>
    /// <returns>True if item was successfully equipped, false if item not found</returns>
    public bool EquipItemFromInventory(string itemId)
    {
        if (inventoryManager == null)
        {
            DebugLog("Cannot equip item - inventory manager not found");
            return false;
        }

        var inventoryItem = inventoryManager.InventoryData.GetItem(itemId);
        if (inventoryItem?.ItemData == null)
        {
            DebugLog($"Cannot equip item - item {itemId} not found in inventory");
            return false;
        }

        // Update equipment state
        equipmentData.equippedItem.EquipFromInventory(itemId, inventoryItem.ItemData);

        DebugLog($"Equipped {inventoryItem.ItemData.itemName} from inventory");
        PlayEquipSound();
        OnItemEquipped?.Invoke(equipmentData.equippedItem);

        return true;
    }

    /// <summary>
    /// Removes the currently equipped item, returning to an unequipped state.
    /// Fires OnItemUnequipped event for UI updates.
    /// </summary>
    public void UnequipCurrentItem()
    {
        if (!HasEquippedItem) return;

        string itemName = equipmentData.equippedItem.GetItemData()?.itemName ?? "Unknown";
        equipmentData.equippedItem.Clear();

        DebugLog($"Unequipped {itemName}");
        OnItemUnequipped?.Invoke();
    }

    #endregion

    #region Hotkey Management

    /// <summary>
    /// Assigns an inventory item to a specific hotkey slot (1-10). This replaces any
    /// existing assignment for that slot. The item is automatically removed from any
    /// other hotkey slots to ensure unique assignment. For consumables, identical
    /// items in the inventory are automatically stacked in the same slot.
    /// </summary>
    /// <param name="itemId">Unique ID of the item in the inventory</param>
    /// <param name="slotNumber">Hotkey slot number (1-10, where 10 is the '0' key)</param>
    /// <returns>True if assignment succeeded, false if item not found or invalid slot</returns>
    public bool AssignItemToHotkey(string itemId, int slotNumber)
    {
        if (slotNumber < 1 || slotNumber > 10)
        {
            DebugLog($"Invalid hotkey slot: {slotNumber}");
            return false;
        }

        if (inventoryManager == null) return false;

        var inventoryItem = inventoryManager.InventoryData.GetItem(itemId);
        if (inventoryItem?.ItemData == null)
        {
            DebugLog($"Cannot assign hotkey - item {itemId} not found");
            return false;
        }

        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null) return false;

        // The binding handles clearing existing assignments and stacking logic
        binding.AssignItem(itemId, inventoryItem.ItemData.name);

        DebugLog($"Assigned {inventoryItem.ItemData.itemName} to hotkey {slotNumber}");
        OnHotkeyAssigned?.Invoke(slotNumber, binding);

        return true;
    }

    /// <summary>
    /// Activates (equips) the item assigned to a specific hotkey slot. Validates that
    /// the assigned item still exists in the inventory before equipping. If the item
    /// no longer exists, clears the hotkey assignment automatically.
    /// </summary>
    /// <param name="slotNumber">Hotkey slot number (1-10)</param>
    /// <returns>True if item was equipped, false if slot empty or item missing</returns>
    public bool ActivateHotkey(int slotNumber)
    {
        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null || !binding.isAssigned)
        {
            DebugLog($"Hotkey {slotNumber} is not assigned");
            return false;
        }

        if (inventoryManager == null) return false;

        // Verify item still exists in inventory
        var inventoryItem = inventoryManager.InventoryData.GetItem(binding.itemId);
        if (inventoryItem?.ItemData == null)
        {
            DebugLog($"Hotkey {slotNumber} item no longer in inventory - removing assignment");
            binding.RemoveItem(binding.itemId);
            OnHotkeyCleared?.Invoke(slotNumber);
            return false;
        }

        // Equip the item with hotkey source tracking
        equipmentData.equippedItem.EquipFromHotkey(binding.itemId, inventoryItem.ItemData, slotNumber);

        DebugLog($"Activated hotkey {slotNumber}: {inventoryItem.ItemData.itemName}");
        PlayHotkeySound();
        OnItemEquipped?.Invoke(equipmentData.equippedItem);

        return true;
    }

    /// <summary>
    /// Cycles through assigned hotkey items in sequence. If no item is currently equipped
    /// from a hotkey, starts with the first assigned slot. If an item is equipped from
    /// a hotkey, moves to the next/previous assigned slot in the sequence.
    /// </summary>
    /// <param name="forward">True to cycle forward, false to cycle backward</param>
    public void CycleEquippedItem(bool forward)
    {
        // Get all assigned hotkey slots
        var assignedBindings = equipmentData.hotkeyBindings.FindAll(h => h.isAssigned);
        if (assignedBindings.Count == 0) return;

        int currentIndex = -1;

        // Find current equipped item in the hotkey list
        if (HasEquippedItem && equipmentData.equippedItem.isEquippedFromHotkey)
        {
            currentIndex = assignedBindings.FindIndex(h => h.slotNumber == equipmentData.equippedItem.sourceHotkeySlot);
        }

        // Calculate next index with wraparound
        if (forward)
        {
            currentIndex = (currentIndex + 1) % assignedBindings.Count;
        }
        else
        {
            currentIndex = currentIndex <= 0 ? assignedBindings.Count - 1 : currentIndex - 1;
        }

        // Activate the selected hotkey
        ActivateHotkey(assignedBindings[currentIndex].slotNumber);
    }

    #endregion

    #region Item Actions

    /// <summary>
    /// Performs an action with the currently equipped item. The action varies based on
    /// the item type and whether it's a left or right click:
    /// - Weapons: Left = attack, Right = aim
    /// - Consumables: Left = punch, Right = consume item
    /// - Equipment: Left = punch, Right = use equipment
    /// - Key Items: Left = punch, Right = attempt to use
    /// - Ammo: Left = punch only
    /// </summary>
    /// <param name="isLeftClick">True for left click action, false for right click</param>
    public void PerformItemAction(bool isLeftClick)
    {
        if (!HasEquippedItem) return;

        var itemData = equipmentData.equippedItem.GetItemData();
        if (itemData == null) return;

        DebugLog($"Performing {(isLeftClick ? "left" : "right")} click action with {itemData.itemName}");

        // Route to appropriate action handler based on item type
        switch (itemData.itemType)
        {
            case ItemType.Weapon:
                if (isLeftClick) OnWeaponAttack(itemData);
                else OnWeaponAim(itemData);
                break;

            case ItemType.Consumable:
                if (isLeftClick) OnPunch();
                else OnConsumeItem(itemData);
                break;

            case ItemType.Equipment:
                if (isLeftClick) OnPunch();
                else OnUseEquipment(itemData);
                break;

            case ItemType.KeyItem:
                if (isLeftClick) OnPunch();
                else OnUseKeyItem(itemData);
                break;

            case ItemType.Ammo:
                if (isLeftClick) OnPunch();
                break;
        }

        OnItemActionPerformed?.Invoke(itemData.itemType, isLeftClick);
    }

    // Action implementation methods - extend these for game-specific behavior

    /// <summary>
    /// Handles weapon attack action (left click on weapon).
    /// Override or extend this method to implement actual weapon attack behavior.
    /// </summary>
    private void OnWeaponAttack(ItemData weaponData)
    {
        DebugLog($"Attacking with {weaponData.itemName}");
        // TODO: Implement weapon attack logic
    }

    /// <summary>
    /// Handles weapon aim action (right click on weapon).
    /// Override or extend this method to implement weapon aiming behavior.
    /// </summary>
    private void OnWeaponAim(ItemData weaponData)
    {
        DebugLog($"Aiming {weaponData.itemName}");
        // TODO: Implement weapon aiming logic
    }

    /// <summary>
    /// Handles consumable item usage (right click on consumable).
    /// Automatically removes the consumed item from inventory.
    /// Override or extend this method to implement consumption effects.
    /// </summary>
    private void OnConsumeItem(ItemData consumableData)
    {
        DebugLog($"Consuming {consumableData.itemName}");

        // TODO: Apply consumable effects (healing, buffs, etc.)

        // Remove consumed item from inventory
        if (inventoryManager != null)
        {
            inventoryManager.RemoveItem(equipmentData.equippedItem.equippedItemId);
        }
    }

    /// <summary>
    /// Handles equipment usage (right click on equipment).
    /// Override or extend this method to implement equipment-specific behavior.
    /// </summary>
    private void OnUseEquipment(ItemData equipmentData)
    {
        DebugLog($"Using equipment {equipmentData.itemName}");
        // TODO: Implement equipment usage logic
    }

    /// <summary>
    /// Handles key item usage (right click on key item).
    /// Override or extend this method to implement key item interactions.
    /// </summary>
    private void OnUseKeyItem(ItemData keyItemData)
    {
        DebugLog($"Attempting to use key item {keyItemData.itemName}");
        // TODO: Implement key item usage logic (unlock doors, trigger events, etc.)
    }

    /// <summary>
    /// Handles punch action (left click when no weapon equipped or with non-weapon items).
    /// Override or extend this method to implement unarmed combat.
    /// </summary>
    private void OnPunch()
    {
        DebugLog("Punching (no weapon equipped)");
        // TODO: Implement unarmed attack logic
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Responds to items being removed from inventory by cleaning up equipment assignments.
    /// Automatically unequips items and removes hotkey assignments for deleted items.
    /// For stacked items, removes only the specific item from stacks.
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        // Check if the equipped item was removed
        if (equipmentData.equippedItem.IsEquipped(itemId))
        {
            UnequipCurrentItem();
        }

        // Clean up hotkey assignments
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.RemoveItem(itemId))
            {
                // Update UI based on whether the slot is now empty or still has items
                if (!binding.isAssigned)
                {
                    OnHotkeyCleared?.Invoke(binding.slotNumber);
                }
                else
                {
                    OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                }
            }
        }
    }

    /// <summary>
    /// Responds to items being added to inventory by automatically stacking consumables
    /// in existing hotkey slots that already contain the same item type.
    /// </summary>
    private void OnInventoryItemAdded(InventoryItemData newItem)
    {
        if (newItem?.ItemData == null) return;

        // Only auto-stack consumables for convenience
        if (newItem.ItemData.itemType != ItemType.Consumable) return;

        // Try to add to existing hotkey stacks
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.TryAddToStack(newItem.ID, newItem.ItemData.name))
            {
                DebugLog($"Added new {newItem.ItemData.itemName} to hotkey {binding.slotNumber} stack");
                OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                break; // Only add to first matching hotkey
            }
        }
    }

    #endregion

    #region Audio

    /// <summary>
    /// Plays the equipment sound effect when items are equipped from inventory.
    /// </summary>
    private void PlayEquipSound()
    {
        if (equipSound != null)
        {
            AudioSource.PlayClipAtPoint(equipSound, Vector3.zero);
        }
    }

    /// <summary>
    /// Plays the hotkey sound effect when items are equipped via hotkey activation.
    /// </summary>
    private void PlayHotkeySound()
    {
        if (hotkeySound != null)
        {
            AudioSource.PlayClipAtPoint(hotkeySound, Vector3.zero);
        }
    }

    #endregion

    #region Save System Integration

    /// <summary>
    /// Directly replaces the current equipment data. Used by EquipmentSaveComponent
    /// during save/load operations. Clears existing state before applying new data.
    /// </summary>
    /// <param name="newData">Complete equipment data to restore</param>
    public void SetEquipmentData(EquipmentSaveData newData)
    {
        if (newData == null || !newData.IsValid())
        {
            DebugLog("Invalid equipment data provided - clearing state");
            ClearEquipmentState();
            return;
        }

        ClearEquipmentState();
        equipmentData = newData;

        DebugLog("Equipment data restored from save system");
    }

    /// <summary>
    /// Resets all equipment state to empty. Fires appropriate events for UI cleanup.
    /// Used by save system and for manual reset operations.
    /// </summary>
    public void ClearEquipmentState()
    {
        // Clear equipped item
        if (HasEquippedItem)
        {
            equipmentData.equippedItem.Clear();
            OnItemUnequipped?.Invoke();
        }

        // Clear all hotkey assignments
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                int slotNumber = binding.slotNumber;
                binding.ClearSlot();
                OnHotkeyCleared?.Invoke(slotNumber);
            }
        }

        DebugLog("Equipment state cleared");
    }

    /// <summary>
    /// Returns a copy of the current equipment data for saving. Used by EquipmentSaveComponent
    /// to extract data without modifying the active state.
    /// </summary>
    /// <returns>Complete copy of current equipment state</returns>
    public EquipmentSaveData GetEquipmentDataDirect()
    {
        return new EquipmentSaveData(equipmentData);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Gets the hotkey binding for a specific slot number (1-10).
    /// Used by UI systems and external code to query hotkey assignments.
    /// </summary>
    /// <param name="slotNumber">Slot number (1-10)</param>
    /// <returns>Hotkey binding data, or null if invalid slot number</returns>
    public HotkeyBinding GetHotkeyBinding(int slotNumber)
    {
        return equipmentData.GetHotkeyBinding(slotNumber);
    }

    /// <summary>
    /// Gets all hotkey bindings for bulk operations and UI display.
    /// Returns the complete list of 10 hotkey slots (some may be unassigned).
    /// </summary>
    /// <returns>List of all hotkey bindings</returns>
    public List<HotkeyBinding> GetAllHotkeyBindings()
    {
        return equipmentData.hotkeyBindings;
    }

    #endregion

    #region Debug and Utility

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EquippedItemManager] {message}");
        }
    }

    /// <summary>
    /// Outputs detailed debug information about the current equipment state.
    /// Shows equipped item, hotkey assignments, and stack information.
    /// </summary>
    [Button("Debug Current State")]
    private void DebugCurrentState()
    {
        Debug.Log("=== EQUIPMENT SYSTEM DEBUG ===");
        Debug.Log($"Has Equipped Item: {HasEquippedItem}");

        if (HasEquippedItem)
        {
            var item = equipmentData.equippedItem;
            string source = item.isEquippedFromHotkey ? $"hotkey {item.sourceHotkeySlot}" : "inventory";
            Debug.Log($"Equipped: {item.GetItemData()?.itemName} (from {source})");
        }

        Debug.Log("Hotkey Assignments:");
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                string stackInfo = binding.HasMultipleItems ? $" {binding.GetStackInfo()}" : "";
                Debug.Log($"  {binding.slotNumber}: {binding.GetCurrentItemData()?.itemName}{stackInfo}");
            }
        }
    }

    #endregion

    private void OnDestroy()
    {
        // Clean up event subscriptions
        GameManager.OnManagersRefreshed -= RefreshReferences;
        InputManager.OnInputManagerReady -= OnInputManagerReady;

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved -= OnInventoryItemRemoved;
            inventoryManager.OnItemAdded -= OnInventoryItemAdded;
        }

        InputManager.Instance.OnScrollWheelInput -= HandleScrollInput;
        InputManager.Instance.OnHotkeyPressed -= OnHotkeyPressed;
    }
}