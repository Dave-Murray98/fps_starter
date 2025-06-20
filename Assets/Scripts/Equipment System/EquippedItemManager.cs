using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// Main equipment system manager
/// Handles equipped items, hotkey assignments, and item actions
/// CLEANED: No longer implements ISaveable - EquipmentSaveComponent handles all save/load
/// </summary>
public class EquippedItemManager : MonoBehaviour
{
    public static EquippedItemManager Instance { get; private set; }

    [Header("Equipment State")]
    [SerializeField, ReadOnly] private EquipmentSaveData equipmentData;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float scrollCooldown = 0.1f; // Prevent scroll spam

    [Header("Audio")]
    [SerializeField] private AudioClip equipSound;
    [SerializeField] private AudioClip hotkeySound;

    // Components
    private InventoryManager inventoryManager;
    private InputManager inputManager;

    // Input timing
    private float lastScrollTime = 0f;

    // Events
    public System.Action<EquippedItemData> OnItemEquipped;
    public System.Action OnItemUnequipped;
    public System.Action<int, HotkeyBinding> OnHotkeyAssigned;
    public System.Action<int> OnHotkeyCleared;
    public System.Action<ItemType, bool> OnItemActionPerformed; // itemType, isLeftClick

    // Public properties
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

    private void Initialize()
    {
        equipmentData = new EquipmentSaveData();
        DebugLog("EquippedItemManager initialized");
    }

    private void Start()
    {
        // Get component references
        inventoryManager = InventoryManager.Instance;

        // Subscribe to manager events
        GameManager.OnManagersRefreshed += RefreshReferences;
        InputManager.OnInputManagerReady += OnInputManagerReady;

        // Subscribe to inventory events
        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved += OnInventoryItemRemoved;
            inventoryManager.OnItemAdded += OnInventoryItemAdded;
        }

        RefreshReferences();
        SetupInputHandlers();
    }

    private void RefreshReferences()
    {
        inputManager = GameManager.Instance?.inputManager;
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance;
        }
    }

    private void OnInputManagerReady(InputManager newInputManager)
    {
        inputManager = newInputManager;
        SetupInputHandlers();
    }

    #region Input Handling

    private void SetupInputHandlers()
    {
        if (inputManager != null)
        {
            // Subscribe to scroll wheel input from InputManager
            inputManager.OnScrollWheelInput += HandleScrollInput;
            inputManager.OnHotkeyPressed += OnHotkeyPressed;
        }
    }

    private void OnHotkeyPressed(int slotNumber)
    {
        ActivateHotkey(slotNumber);
    }

    public void HandleScrollInput(Vector2 scrollDelta)
    {
        // Only process if enough time has passed (prevents scroll spam)
        if (Time.time - lastScrollTime < scrollCooldown)
            return;

        // Only cycle if inventory is closed
        if (GameManager.Instance?.uiManager?.isInventoryOpen == true)
            return;

        // Check if there's significant scroll input
        if (Mathf.Abs(scrollDelta.y) > 0.1f)
        {
            lastScrollTime = Time.time;
            CycleEquippedItem(scrollDelta.y > 0);
        }
    }

    private void Update()
    {
        // Handle hotkey inputs (fallback if InputManager doesn't handle them)
        if (inputManager == null)
        {
            HandleHotkeyInputs();
        }

        // Handle item action inputs (could be moved to InputManager too)
        HandleItemActionInputs();
    }

    private void HandleHotkeyInputs()
    {
        // Check keys 1-0 for hotkey activation
        for (int i = 1; i <= 10; i++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
            if (i == 10) key = KeyCode.Alpha0; // 0 key is slot 10

            if (Input.GetKeyDown(key))
            {
                ActivateHotkey(i);
            }
        }
    }

    private void HandleItemActionInputs()
    {
        // Only handle if inventory is closed and we have an equipped item
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

        // Equip the item
        equipmentData.equippedItem.EquipFromInventory(itemId, inventoryItem.ItemData);

        DebugLog($"Equipped {inventoryItem.ItemData.itemName} from inventory");
        PlayEquipSound();
        OnItemEquipped?.Invoke(equipmentData.equippedItem);

        return true;
    }

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

        binding.AssignItem(itemId, inventoryItem.ItemData.name);

        DebugLog($"Assigned {inventoryItem.ItemData.itemName} to hotkey {slotNumber}");
        OnHotkeyAssigned?.Invoke(slotNumber, binding);

        return true;
    }

    public bool ActivateHotkey(int slotNumber)
    {
        var binding = equipmentData.GetHotkeyBinding(slotNumber);
        if (binding == null || !binding.isAssigned)
        {
            DebugLog($"Hotkey {slotNumber} is not assigned");
            return false;
        }

        // Check if item still exists in inventory
        if (inventoryManager == null) return false;

        var inventoryItem = inventoryManager.InventoryData.GetItem(binding.itemId);
        if (inventoryItem?.ItemData == null)
        {
            DebugLog($"Hotkey {slotNumber} item no longer in inventory - removing assignment");
            binding.RemoveItem(binding.itemId);
            OnHotkeyCleared?.Invoke(slotNumber);
            return false;
        }

        // Equip the item
        equipmentData.equippedItem.EquipFromHotkey(binding.itemId, inventoryItem.ItemData, slotNumber);

        DebugLog($"Activated hotkey {slotNumber}: {inventoryItem.ItemData.itemName}");
        PlayHotkeySound();
        OnItemEquipped?.Invoke(equipmentData.equippedItem);

        return true;
    }

    public void CycleEquippedItem(bool forward)
    {
        // Cache assigned bindings to avoid repeated calculations
        var assignedBindings = equipmentData.hotkeyBindings.FindAll(h => h.isAssigned);
        if (assignedBindings.Count == 0) return;

        int currentIndex = -1;

        // Find current equipped item in hotkey list
        if (HasEquippedItem && equipmentData.equippedItem.isEquippedFromHotkey)
        {
            currentIndex = assignedBindings.FindIndex(h => h.slotNumber == equipmentData.equippedItem.sourceHotkeySlot);
        }

        // Calculate next index
        if (forward)
        {
            currentIndex = (currentIndex + 1) % assignedBindings.Count;
        }
        else
        {
            currentIndex = currentIndex <= 0 ? assignedBindings.Count - 1 : currentIndex - 1;
        }

        // Activate the hotkey
        ActivateHotkey(assignedBindings[currentIndex].slotNumber);
    }

    #endregion

    #region Item Actions

    public void PerformItemAction(bool isLeftClick)
    {
        if (!HasEquippedItem) return;

        var itemData = equipmentData.equippedItem.GetItemData();
        if (itemData == null) return;

        DebugLog($"Performing {(isLeftClick ? "left" : "right")} click action with {itemData.itemName}");

        // Trigger action based on item type
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

    // Action implementations (placeholders for now)
    private void OnWeaponAttack(ItemData weaponData)
    {
        DebugLog($"Attacking with {weaponData.itemName}");
    }

    private void OnWeaponAim(ItemData weaponData)
    {
        DebugLog($"Aiming {weaponData.itemName}");
    }

    private void OnConsumeItem(ItemData consumableData)
    {
        DebugLog($"Consuming {consumableData.itemName}");

        // For now, just remove from inventory
        if (inventoryManager != null)
        {
            inventoryManager.RemoveItem(equipmentData.equippedItem.equippedItemId);
        }
    }

    private void OnUseEquipment(ItemData equipmentData)
    {
        DebugLog($"Using equipment {equipmentData.itemName}");
    }

    private void OnUseKeyItem(ItemData keyItemData)
    {
        DebugLog($"Attempting to use key item {keyItemData.itemName}");
    }

    private void OnPunch()
    {
        DebugLog("Punching (no weapon equipped)");
    }

    #endregion

    #region Event Handlers

    private void OnInventoryItemRemoved(string itemId)
    {
        // Check if equipped item was removed
        if (equipmentData.equippedItem.IsEquipped(itemId))
        {
            UnequipCurrentItem();
        }

        // Check and update hotkey assignments
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.RemoveItem(itemId))
            {
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

    private void OnInventoryItemAdded(InventoryItemData newItem)
    {
        if (newItem?.ItemData == null) return;

        // Only try to stack consumables
        if (newItem.ItemData.itemType != ItemType.Consumable) return;

        // Check if any hotkey can stack this item
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

    private void PlayEquipSound()
    {
        if (equipSound != null)
        {
            AudioSource.PlayClipAtPoint(equipSound, Vector3.zero);
        }
    }

    private void PlayHotkeySound()
    {
        if (hotkeySound != null)
        {
            AudioSource.PlayClipAtPoint(hotkeySound, Vector3.zero);
        }
    }

    #endregion

    #region Public API for Save System (Called by EquipmentSaveComponent)


    /// <summary>
    /// Public method to directly set equipment data (for save component)
    /// ADD THIS to EquippedItemManager
    /// </summary>
    public void SetEquipmentData(EquipmentSaveData newData)
    {
        if (newData == null || !newData.IsValid())
        {
            DebugLog("Invalid equipment data provided - clearing state");
            ClearEquipmentState();
            return;
        }

        // Clear current state first
        ClearEquipmentState();

        // Set the equipment data directly
        equipmentData = newData;

        DebugLog("Equipment data set directly via save component");
    }

    /// <summary>
    /// Public method to clear all equipment state (for save component)
    /// ADD THIS to EquippedItemManager
    /// </summary>
    public void ClearEquipmentState()
    {
        // Clear equipped item
        if (HasEquippedItem)
        {
            equipmentData.equippedItem.Clear();
            OnItemUnequipped?.Invoke();
        }

        // Clear all hotkey bindings
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
    /// Public method to get equipment data directly (for save component)
    /// ADD THIS to EquippedItemManager
    /// </summary>
    public EquipmentSaveData GetEquipmentDataDirect()
    {
        // Return a copy of the current equipment data
        return new EquipmentSaveData(equipmentData);
    }
    /// <summary>
    /// Get equipment data for saving (called by EquipmentSaveComponent)
    /// </summary>
    public EquipmentSaveData GetDataToSave()
    {
        DebugLog("=== SAVING EQUIPMENT DATA ===");
        DebugLog($"Equipped Item: {(HasEquippedItem ? equipmentData.equippedItem.GetItemData()?.itemName : "None")}");

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                DebugLog($"Saving Hotkey {binding.slotNumber}: {binding.itemDataName} (ID: {binding.itemId}) - Stack: {binding.stackedItemIds.Count} items");
            }
            else
            {
                DebugLog($"Saving Hotkey {binding.slotNumber}: Empty");
            }
        }

        return new EquipmentSaveData(equipmentData); // Return a copy of the equipmentData, so if it's a doorway transition, we don't clear it when we load;
    }

    /// <summary>
    /// Load equipment data from save (called by EquipmentSaveComponent)
    /// </summary>
    public void LoadSaveData(EquipmentSaveData savedData)
    {
        // DebugLog("=== LOADING EQUIPMENT DATA ===");

        if (savedData != null && savedData.IsValid())
        {
            // DebugLog("Clearing current equipment state before loading...");
            ClearCurrentEquipmentState();

            // Load the copied data
            equipmentData = savedData;

            // Validate loaded data against current inventory
            ValidateLoadedHotkeys();

            // DebugLog($"After validation, equipment data first itemname is {equipmentData.hotkeyBindings[0].itemDataName}");

            // DebugLog("Equipment data loaded from save");

            // Refresh UI for all hotkeys
            RefreshAllHotkeyUI();
        }
        else
        {
            DebugLog("Failed to load equipment data - invalid or null data");
        }
    }

    /// <summary>
    /// Clear all current equipment state before loading
    /// </summary>
    private void ClearCurrentEquipmentState()
    {
        // Clear equipped item
        if (HasEquippedItem)
        {
            equipmentData.equippedItem.Clear();
        }

        // Clear all hotkey bindings
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                binding.ClearSlot();
            }
        }

        DebugLog("Current equipment state cleared");
    }

    /// <summary>
    /// Refresh UI for all hotkey slots
    /// </summary>
    private void RefreshAllHotkeyUI()
    {
        // Refresh equipped item UI
        if (HasEquippedItem)
        {
            OnItemEquipped?.Invoke(equipmentData.equippedItem);
        }
        else
        {
            OnItemUnequipped?.Invoke();
        }

        // Refresh ALL hotkey slots (both assigned and empty)
        for (int i = 0; i < equipmentData.hotkeyBindings.Count; i++)
        {
            var binding = equipmentData.hotkeyBindings[i];
            if (binding.isAssigned)
            {
                OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
            }
            else
            {
                OnHotkeyCleared?.Invoke(binding.slotNumber);
            }
        }

        DebugLog($"UI refreshed for {equipmentData.hotkeyBindings.Count} hotkey slots");
    }

    /// <summary>
    /// Validate that loaded hotkey assignments still exist in inventory
    /// </summary>
    private void ValidateLoadedHotkeys()
    {

        if (inventoryManager == null)
        {
            DebugLog("Cannot validate hotkeys - inventory manager is null");
            return;
        }

        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (!binding.isAssigned)
            {
                Debug.Log("Hotkey " + binding.slotNumber + " is not assigned - skipping");
                continue;
            }

            // Check if the primary item still exists
            InventoryItemData inventoryItem = inventoryManager.InventoryData.GetItem(binding.itemId);

            Debug.Log($"Hotkey {binding.slotNumber}: Validating primary item {binding.itemId}");

            if (inventoryItem == null)
            {
                DebugLog($"Hotkey {binding.slotNumber}: Primary item {binding.itemId} no longer exists - clearing slot");
                binding.ClearSlot();
                continue;
            }

            // Validate stacked items
            var itemsToRemove = new List<string>();
            foreach (string stackedId in binding.stackedItemIds)
            {
                if (inventoryManager.InventoryData.GetItem(stackedId) == null)
                {
                    DebugLog($"Hotkey {binding.slotNumber}: Stacked item {stackedId} no longer exists - removing");
                    itemsToRemove.Add(stackedId);
                }
            }

            foreach (string itemToRemove in itemsToRemove)
            {
                DebugLog($"Hotkey {binding.slotNumber}: Removing invalid stacked item {itemToRemove}");
                binding.RemoveItem(itemToRemove);
            }
        }
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

    [Button("Debug Current State")]
    private void DebugCurrentState()
    {
        Debug.Log("=== EQUIPMENT SYSTEM DEBUG ===");
        Debug.Log($"Has Equipped Item: {HasEquippedItem}");

        if (HasEquippedItem)
        {
            var item = equipmentData.equippedItem;
            Debug.Log($"Equipped: {item.GetItemData()?.itemName} (from {(item.isEquippedFromHotkey ? $"hotkey {item.sourceHotkeySlot}" : "inventory")})");
        }

        Debug.Log("Hotkey Assignments:");
        foreach (var binding in equipmentData.hotkeyBindings)
        {
            if (binding.isAssigned)
            {
                Debug.Log($"  {binding.slotNumber}: {binding.GetCurrentItemData()?.itemName} {binding.GetStackInfo()}");
            }
        }
    }

    /// <summary>
    /// Get hotkey binding for slot (public access)
    /// </summary>
    public HotkeyBinding GetHotkeyBinding(int slotNumber)
    {
        return equipmentData.GetHotkeyBinding(slotNumber);
    }

    /// <summary>
    /// Get all hotkey bindings (for UI)
    /// </summary>
    public List<HotkeyBinding> GetAllHotkeyBindings()
    {
        return equipmentData.hotkeyBindings;
    }

    #endregion

    private void OnDestroy()
    {
        // Unsubscribe from events
        GameManager.OnManagersRefreshed -= RefreshReferences;
        InputManager.OnInputManagerReady -= OnInputManagerReady;

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved -= OnInventoryItemRemoved;
            inventoryManager.OnItemAdded -= OnInventoryItemAdded;
        }

        if (inputManager != null)
        {
            inputManager.OnScrollWheelInput -= HandleScrollInput;
            inputManager.OnHotkeyPressed -= OnHotkeyPressed;
        }
    }
}