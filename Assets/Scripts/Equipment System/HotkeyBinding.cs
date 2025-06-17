using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data for a hotkey slot assignment
/// FIXED: Now properly handles replacement and smart stacking for consumables only
/// </summary>
[System.Serializable]
public class HotkeyBinding
{
    [Header("Slot Info")]
    public int slotNumber;      // 1-0 keys (1=slot 1, 0=slot 10)
    public bool isAssigned;     // Whether this slot has an item

    [Header("Item Reference")]
    public string itemId;       // Current item ID from inventory
    public string itemDataName; // ItemData name for persistence

    [Header("Stack Management")]
    public List<string> stackedItemIds = new List<string>(); // All items of this type
    public int currentStackIndex = 0; // Which item in stack is active

    public HotkeyBinding(int slot)
    {
        slotNumber = slot;
        isAssigned = false;
        stackedItemIds = new List<string>();
    }

    /// <summary>
    /// Assign an item to this hotkey slot (REPLACES any existing assignment)
    /// </summary>
    public void AssignItem(string newItemId, string newItemDataName)
    {
        // STEP 1: Clear any existing assignment (this is the key fix!)
        ClearSlot();

        // STEP 2: Remove this item from any other hotkey slots (unique assignment rule)
        RemoveItemFromOtherHotkeys(newItemId);

        // STEP 3: Assign the new item
        itemId = newItemId;
        itemDataName = newItemDataName;
        isAssigned = true;

        // STEP 4: Initialize stack with this item
        stackedItemIds.Add(newItemId);
        currentStackIndex = 0;

        // STEP 5: Find and stack identical consumables if this is a consumable
        FindAndStackIdenticalConsumables();
    }

    /// <summary>
    /// Remove this item from any other hotkey slots to ensure unique assignment
    /// </summary>
    private void RemoveItemFromOtherHotkeys(string itemIdToRemove)
    {
        if (EquippedItemManager.Instance == null) return;

        var allBindings = EquippedItemManager.Instance.GetAllHotkeyBindings();
        foreach (var binding in allBindings)
        {
            if (binding != this && binding.isAssigned)
            {
                // Check if this binding contains the item
                if (binding.stackedItemIds.Contains(itemIdToRemove))
                {
                    bool wasCleared = false;

                    // Remove the item
                    binding.RemoveItem(itemIdToRemove);

                    // Check if the binding was completely cleared
                    wasCleared = !binding.isAssigned;

                    // Trigger appropriate UI update event
                    if (wasCleared)
                    {
                        EquippedItemManager.Instance.OnHotkeyCleared?.Invoke(binding.slotNumber);
                    }
                    else
                    {
                        EquippedItemManager.Instance.OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Find all identical consumable items in inventory and add them to the stack
    /// </summary>
    private void FindAndStackIdenticalConsumables()
    {
        if (PersistentInventoryManager.Instance == null) return;

        // Get the ItemData to check if it's a consumable
        var itemData = GetCurrentItemData();
        if (itemData == null || itemData.itemType != ItemType.Consumable) return;

        // Find all items in inventory with the same ItemData name
        var inventoryItems = PersistentInventoryManager.Instance.InventoryData.GetAllItems();

        foreach (var inventoryItem in inventoryItems)
        {
            // Skip if it's already in our stack
            if (stackedItemIds.Contains(inventoryItem.ID)) continue;

            // Add to stack if it's the exact same consumable type
            if (inventoryItem.ItemData != null &&
                inventoryItem.ItemData.name == itemDataName)
            {
                stackedItemIds.Add(inventoryItem.ID);
            }
        }

        Debug.Log($"Hotkey {slotNumber}: Found {stackedItemIds.Count} identical {itemDataName} consumables");
    }

    /// <summary>
    /// Remove an item from this slot's stack
    /// </summary>
    public bool RemoveItem(string itemIdToRemove)
    {
        bool removed = stackedItemIds.Remove(itemIdToRemove);

        if (removed)
        {
            // If we removed the current item, find next available
            if (itemId == itemIdToRemove)
            {
                if (stackedItemIds.Count > 0)
                {
                    // Move to next item in stack
                    currentStackIndex = Mathf.Clamp(currentStackIndex, 0, stackedItemIds.Count - 1);
                    itemId = stackedItemIds[currentStackIndex];

                    Debug.Log($"Hotkey {slotNumber}: Switched to next {itemDataName} in stack ({stackedItemIds.Count} remaining)");
                }
                else
                {
                    // No more items, clear slot
                    Debug.Log($"Hotkey {slotNumber}: No more {itemDataName} items, clearing slot");
                    ClearSlot();
                }
            }
            else
            {
                // Update current index if needed
                currentStackIndex = stackedItemIds.IndexOf(itemId);
            }
        }

        return removed;
    }

    /// <summary>
    /// Clear this hotkey slot completely
    /// </summary>
    public void ClearSlot()
    {
        itemId = "";
        itemDataName = "";
        isAssigned = false;
        stackedItemIds.Clear();
        currentStackIndex = 0;
    }

    /// <summary>
    /// Refresh the stack (call this when inventory changes)
    /// </summary>
    public void RefreshStack()
    {
        if (!isAssigned) return;

        // Remove any item IDs that no longer exist in inventory
        var itemsToRemove = new List<string>();

        foreach (string stackedId in stackedItemIds)
        {
            if (PersistentInventoryManager.Instance?.InventoryData.GetItem(stackedId) == null)
            {
                itemsToRemove.Add(stackedId);
            }
        }

        foreach (string itemToRemove in itemsToRemove)
        {
            RemoveItem(itemToRemove);
        }
    }

    /// <summary>
    /// Get the ItemData for the currently assigned item
    /// </summary>
    public ItemData GetCurrentItemData()
    {
        if (!isAssigned || string.IsNullOrEmpty(itemDataName))
        {
            return null;
        }

        // First try to get from current inventory item
        if (PersistentInventoryManager.Instance != null)
        {
            var inventoryItem = PersistentInventoryManager.Instance.InventoryData.GetItem(itemId);
            if (inventoryItem?.ItemData != null)
            {
                return inventoryItem.ItemData;
            }
        }

        // Fallback to Resources load
        return Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + itemDataName);
    }

    /// <summary>
    /// Add a new item to this hotkey's stack if it's the same type (for dynamic stacking)
    /// </summary>
    public bool TryAddToStack(string newItemId, string newItemDataName)
    {
        // Only add if this hotkey is assigned and it's the same item type
        if (!isAssigned || itemDataName != newItemDataName) return false;

        // Only stack consumables
        var itemData = GetCurrentItemData();
        if (itemData == null || itemData.itemType != ItemType.Consumable) return false;

        // Don't add if already in stack
        if (stackedItemIds.Contains(newItemId)) return false;

        // Add to stack
        stackedItemIds.Add(newItemId);
        Debug.Log($"Hotkey {slotNumber}: Added new {itemDataName} to stack ({stackedItemIds.Count} total)");

        return true;
    }

    /// <summary>
    /// Check if this slot has multiple items stacked
    /// </summary>
    public bool HasMultipleItems => stackedItemIds.Count > 1;

    /// <summary>
    /// Get stack info for UI display
    /// </summary>
    public string GetStackInfo()
    {
        if (!isAssigned) return "";
        if (stackedItemIds.Count <= 1) return "";
        return $"{currentStackIndex + 1}/{stackedItemIds.Count}";
    }
}