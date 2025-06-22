using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// PHASE 2: Helper component to improve drag and drop integration between inventory and clothing systems
/// Provides utilities for detecting drop targets and managing drag state
/// </summary>
public static class ClothingDragDropHelper
{
    /// <summary>
    /// Checks if a drag operation is over any clothing slot
    /// </summary>
    public static ClothingSlotUI GetClothingSlotUnderPointer(PointerEventData eventData)
    {
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            var clothingSlotUI = result.gameObject.GetComponent<ClothingSlotUI>();
            if (clothingSlotUI != null)
            {
                return clothingSlotUI;
            }

            // Also check parent objects in case the slot has child elements
            var parentSlotUI = result.gameObject.GetComponentInParent<ClothingSlotUI>();
            if (parentSlotUI != null)
            {
                return parentSlotUI;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates if an inventory item can be equipped to a specific clothing slot
    /// </summary>
    public static bool CanEquipToSlot(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (itemData?.ItemData?.itemType != ItemType.Clothing)
            return false;

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
            return false;

        return clothingData.CanEquipToLayer(clothingSlot.TargetLayer);
    }

    /// <summary>
    /// Gets user-friendly feedback message for invalid drops
    /// </summary>
    public static string GetDropErrorMessage(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (itemData?.ItemData == null)
            return "Invalid item";

        if (itemData.ItemData.itemType != ItemType.Clothing)
            return "Not a clothing item";

        var clothingData = itemData.ItemData.ClothingData;
        if (clothingData == null)
            return "No clothing data";

        if (!clothingData.CanEquipToLayer(clothingSlot.TargetLayer))
        {
            string layerName = ClothingInventoryUtilities.GetShortLayerName(clothingSlot.TargetLayer);
            return $"Cannot equip to {layerName}";
        }

        return "Unknown error";
    }

    /// <summary>
    /// Determines if a drag operation should show inventory preview or clothing slot feedback
    /// </summary>
    public static bool ShouldShowInventoryPreview(PointerEventData eventData, InventoryItemData itemData)
    {
        // Don't show inventory preview if we're over a valid clothing slot
        var clothingSlot = GetClothingSlotUnderPointer(eventData);
        if (clothingSlot != null && CanEquipToSlot(itemData, clothingSlot))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the appropriate visual feedback color for a clothing slot during drag operations
    /// </summary>
    public static Color GetClothingSlotFeedbackColor(InventoryItemData itemData, ClothingSlotUI clothingSlot,
        Color validColor, Color invalidColor)
    {
        return CanEquipToSlot(itemData, clothingSlot) ? validColor : invalidColor;
    }

    /// <summary>
    /// Handles the complete drop operation onto a clothing slot with proper error handling
    /// </summary>
    public static bool HandleClothingSlotDrop(InventoryItemData itemData, ClothingSlotUI clothingSlot)
    {
        if (clothingSlot == null || !CanEquipToSlot(itemData, clothingSlot))
        {
            Debug.LogWarning($"Cannot drop {itemData?.ItemData?.itemName} on clothing slot {clothingSlot?.TargetLayer}");
            return false;
        }

        // Use the clothing manager to handle the actual equipment
        var clothingManager = ClothingManager.Instance;
        if (clothingManager == null)
        {
            Debug.LogError("ClothingManager not found for drop operation");
            return false;
        }

        Debug.Log($"[ClothingDragDropHelper] Handling drop: {itemData.ItemData.itemName} -> {clothingSlot.TargetLayer}");

        bool success = clothingManager.EquipItemToLayer(itemData.ID, clothingSlot.TargetLayer);

        if (success)
        {
            Debug.Log($"[ClothingDragDropHelper] Successfully equipped {itemData.ItemData.itemName} to {clothingSlot.TargetLayer}");
        }
        else
        {
            Debug.LogWarning($"[ClothingDragDropHelper] Failed to equip {itemData.ItemData.itemName} to {clothingSlot.TargetLayer}");
        }

        return success;
    }

    /// <summary>
    /// Debug helper to log drag and drop operations
    /// </summary>
    public static void LogDragDropOperation(string operation, InventoryItemData itemData, ClothingSlotUI clothingSlot = null)
    {
        string itemName = itemData?.ItemData?.itemName ?? "Unknown";
        string slotInfo = clothingSlot != null ? $" -> {clothingSlot.TargetLayer}" : "";

        Debug.Log($"[ClothingDragDrop] {operation}: {itemName}{slotInfo}");
    }

    /// <summary>
    /// Gets all clothing slots in the scene for debugging purposes
    /// </summary>
    public static ClothingSlotUI[] GetAllClothingSlots()
    {
        return Object.FindObjectsByType<ClothingSlotUI>(FindObjectsSortMode.None);
    }

    /// <summary>
    /// Validates the overall clothing drag and drop system setup
    /// </summary>
    public static bool ValidateSystemSetup()
    {
        bool isValid = true;

        // Check for ClothingManager
        if (ClothingManager.Instance == null)
        {
            Debug.LogError("[ClothingDragDropHelper] ClothingManager not found!");
            isValid = false;
        }

        // Check for InventoryManager
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[ClothingDragDropHelper] InventoryManager not found!");
            isValid = false;
        }

        // Check for clothing slots
        var clothingSlots = GetAllClothingSlots();
        if (clothingSlots.Length == 0)
        {
            Debug.LogWarning("[ClothingDragDropHelper] No ClothingSlotUI components found in scene!");
            isValid = false;
        }
        else
        {
            Debug.Log($"[ClothingDragDropHelper] Found {clothingSlots.Length} clothing slots");
        }

        return isValid;
    }
}