using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Integrates clothing system with the existing inventory right-click dropdown system.
/// Adds "Equip to [Slot]" options for clothing items and handles the equipping logic.
/// Works with your existing InventoryDropdownMenu system.
/// </summary>
public class ClothingInventoryIntegration : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private ClothingManager clothingManager;
    [SerializeField] private InventoryDropdownManager dropdownManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Slot name mapping for UI display
    private Dictionary<string, string> slotDisplayNames = new Dictionary<string, string>
    {
        { "head_upper", "Head (Upper)" },
        { "head_lower", "Head (Lower)" },
        { "torso_inner", "Torso (Inner)" },
        { "torso_outer", "Torso (Outer)" },
        { "hands", "Hands" },
        { "legs_inner", "Legs (Inner)" },
        { "legs_outer", "Legs (Outer)" },
        { "socks", "Socks" },
        { "shoes", "Shoes" }
    };

    private void Awake()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
        SubscribeToEvents();
    }

    /// <summary>
    /// Find required component references
    /// </summary>
    private void FindReferences()
    {
        if (clothingManager == null)
            clothingManager = ClothingManager.Instance ?? FindFirstObjectByType<ClothingManager>();

        if (dropdownManager == null)
            dropdownManager = FindFirstObjectByType<InventoryDropdownManager>();

        DebugLog($"Auto-found references - Clothing: {clothingManager != null}, Dropdown: {dropdownManager != null}");
    }

    /// <summary>
    /// Validate references
    /// </summary>
    private void ValidateReferences()
    {
        if (clothingManager == null)
        {
            Debug.LogWarning("ClothingInventoryIntegration: ClothingManager not found - clothing equipping disabled");
        }

        if (dropdownManager == null)
        {
            Debug.LogWarning("ClothingInventoryIntegration: InventoryDropdownManager not found - dropdown integration disabled");
        }
    }

    /// <summary>
    /// Subscribe to events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (dropdownManager?.DropdownMenu != null)
        {
            // Subscribe to the dropdown menu's action selection event
            dropdownManager.DropdownMenu.OnActionSelected += OnDropdownActionSelected;
        }

        GameManager.OnManagersRefreshed += OnManagersRefreshed;
    }

    /// <summary>
    /// Handle dropdown action selection for clothing-specific actions
    /// </summary>
    private void OnDropdownActionSelected(InventoryItemData item, string actionId)
    {
        if (item?.ItemData?.itemType != ItemType.Clothing)
            return;

        // Handle clothing-specific actions
        if (actionId.StartsWith("equip_to_"))
        {
            string slotId = actionId.Substring("equip_to_".Length);
            EquipClothingToSlot(item, slotId);
        }
        else if (actionId == "equip_clothing")
        {
            EquipClothingToAnySlot(item);
        }
        else if (actionId == "unequip_clothing")
        {
            UnequipClothing(item);
        }
        else if (actionId == "repair_clothing")
        {
            RepairClothing(item);
        }
    }

    /// <summary>
    /// Repair clothing item
    /// </summary>
    private void RepairClothing(InventoryItemData item)
    {
        if (ClothingDegradationSystem.Instance == null)
        {
            DebugLog("ClothingDegradationSystem not found - cannot repair clothing");
            return;
        }

        // Find repair tool in inventory
        string repairToolId = ClothingDegradationSystem.Instance.FindRepairToolInInventory();
        if (string.IsNullOrEmpty(repairToolId))
        {
            DebugLog("No repair tool found in inventory");
            return;
        }

        // Check if item is equipped (can only repair equipped clothing)
        string slotId = FindClothingSlotId(item);
        if (string.IsNullOrEmpty(slotId))
        {
            DebugLog("Can only repair equipped clothing items");
            return;
        }

        bool success = ClothingDegradationSystem.Instance.RepairClothing(slotId, repairToolId);

        if (success)
        {
            DebugLog($"Successfully repaired {item.ItemData.itemName}");
        }
        else
        {
            DebugLog($"Failed to repair {item.ItemData.itemName}");
        }
    }

    /// <summary>
    /// Find which clothing slot this item is equipped in
    /// </summary>
    private string FindClothingSlotId(InventoryItemData item)
    {
        if (clothingManager == null || item?.ID == null)
            return null;

        foreach (var slot in clothingManager.AllClothingSlots)
        {
            if (slot.isOccupied && slot.equippedItemID == item.ID)
            {
                return slot.slotID;
            }
        }

        return null;
    }

    /// <summary>
    /// Equip clothing to a specific slot
    /// </summary>
    private void EquipClothingToSlot(InventoryItemData item, string slotId)
    {
        if (clothingManager == null || item?.ID == null)
        {
            DebugLog("Cannot equip clothing - missing manager or item ID");
            return;
        }

        bool success = clothingManager.EquipClothingToSlot(item.ID, slotId);

        if (success)
        {
            DebugLog($"Successfully equipped {item.ItemData?.itemName} to slot {slotId}");
        }
        else
        {
            DebugLog($"Failed to equip {item.ItemData?.itemName} to slot {slotId}");
        }
    }

    /// <summary>
    /// Equip clothing to any available slot (automatic selection)
    /// </summary>
    private void EquipClothingToAnySlot(InventoryItemData item)
    {
        if (clothingManager == null || item?.ID == null)
        {
            DebugLog("Cannot equip clothing - missing manager or item ID");
            return;
        }

        bool success = clothingManager.EquipClothingFromInventory(item.ID);

        if (success)
        {
            DebugLog($"Successfully equipped {item.ItemData?.itemName}");
        }
        else
        {
            DebugLog($"Failed to equip {item.ItemData?.itemName} - no available slots");
        }
    }

    /// <summary>
    /// Unequip currently equipped clothing
    /// </summary>
    private void UnequipClothing(InventoryItemData item)
    {
        if (clothingManager == null || item?.ID == null)
            return;

        // Find which slot this item is equipped in
        foreach (var slot in clothingManager.AllClothingSlots)
        {
            if (slot.isOccupied && slot.equippedItemID == item.ID)
            {
                bool success = clothingManager.UnequipClothingFromSlot(slot.slotID);

                if (success)
                {
                    DebugLog($"Successfully unequipped {item.ItemData?.itemName} from {slot.slotName}");
                }
                else
                {
                    DebugLog($"Failed to unequip {item.ItemData?.itemName}");
                }
                return;
            }
        }

        DebugLog($"Could not find equipped clothing {item.ItemData?.itemName} to unequip");
    }

    /// <summary>
    /// Check if a clothing item is currently equipped
    /// </summary>
    private bool IsClothingCurrentlyEquipped(InventoryItemData item)
    {
        if (clothingManager == null || item?.ID == null)
            return false;

        foreach (var slot in clothingManager.AllClothingSlots)
        {
            if (slot.isOccupied && slot.equippedItemID == item.ID)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get display name for a slot ID
    /// </summary>
    private string GetSlotDisplayName(string slotId)
    {
        return slotDisplayNames.TryGetValue(slotId, out string displayName) ? displayName : slotId;
    }

    /// <summary>
    /// Get icon for a clothing slot (if you have slot-specific icons)
    /// </summary>
    private Sprite GetSlotIcon(string slotId)
    {
        // You could load slot-specific icons here
        // For now, return null (no icon)
        return null;
    }

    /// <summary>
    /// Handle manager refresh events
    /// </summary>
    private void OnManagersRefreshed()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ClothingInventoryIntegration] {message}");
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        if (dropdownManager?.DropdownMenu != null)
        {
            dropdownManager.DropdownMenu.OnActionSelected -= OnDropdownActionSelected;
        }

        GameManager.OnManagersRefreshed -= OnManagersRefreshed;
    }
}

/// <summary>
/// Data structure for dropdown menu options
/// (This should match your existing DropdownOption structure)
/// </summary>
[System.Serializable]
public class DropdownOption
{
    public string actionId;
    public string displayText;
    public bool isEnabled = true;
    public Sprite icon;
}