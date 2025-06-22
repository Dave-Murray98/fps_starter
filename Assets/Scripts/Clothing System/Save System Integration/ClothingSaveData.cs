using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete clothing system state for saving/loading.
/// Contains all slot assignments and equipped item references.
/// </summary>
[System.Serializable]
public class ClothingSaveData
{
    [Header("Clothing Slots")]
    public List<ClothingSlotSaveData> slots = new List<ClothingSlotSaveData>();

    public ClothingSaveData()
    {
        slots = new List<ClothingSlotSaveData>();
    }

    /// <summary>
    /// Add a clothing slot to the save data
    /// </summary>
    public void AddSlot(ClothingSlotSaveData slotData)
    {
        if (slotData != null)
        {
            slots.Add(slotData);
        }
    }

    /// <summary>
    /// Remove a slot by layer
    /// </summary>
    public bool RemoveSlot(ClothingLayer layer)
    {
        for (int i = slots.Count - 1; i >= 0; i--)
        {
            if (slots[i].layer == layer)
            {
                slots.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get slot save data by layer
    /// </summary>
    public ClothingSlotSaveData GetSlot(ClothingLayer layer)
    {
        foreach (var slot in slots)
        {
            if (slot.layer == layer)
                return slot;
        }
        return null;
    }

    /// <summary>
    /// Clear all slot data
    /// </summary>
    public void Clear()
    {
        slots.Clear();
    }

    /// <summary>
    /// Get count of equipped items (non-empty slots)
    /// </summary>
    public int GetEquippedCount()
    {
        int count = 0;
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Get total number of clothing slots
    /// </summary>
    public int SlotCount => slots.Count;

    /// <summary>
    /// Check if an item is equipped in any slot
    /// </summary>
    public bool IsItemEquipped(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.equippedItemId == itemId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Get the layer where an item is equipped, or null if not equipped
    /// </summary>
    public ClothingLayer? GetLayerForItem(string itemId)
    {
        foreach (var slot in slots)
        {
            if (slot.equippedItemId == itemId)
                return slot.layer;
        }
        return null;
    }

    /// <summary>
    /// Get all equipped item IDs
    /// </summary>
    public List<string> GetAllEquippedItemIds()
    {
        var equippedIds = new List<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
                equippedIds.Add(slot.equippedItemId);
        }
        return equippedIds;
    }

    /// <summary>
    /// Validate that the save data is consistent
    /// </summary>
    public bool IsValid()
    {
        // Check for duplicate layers
        var seenLayers = new HashSet<ClothingLayer>();
        foreach (var slot in slots)
        {
            if (seenLayers.Contains(slot.layer))
                return false;
            seenLayers.Add(slot.layer);
        }

        // Check for duplicate equipped items
        var seenItems = new HashSet<string>();
        foreach (var slot in slots)
        {
            if (!string.IsNullOrEmpty(slot.equippedItemId))
            {
                if (seenItems.Contains(slot.equippedItemId))
                    return false;
                seenItems.Add(slot.equippedItemId);
            }
        }

        return true;
    }

    /// <summary>
    /// Gets debug information about the clothing save data
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== Clothing Save Data Debug Info ===");
        info.AppendLine($"Total Slots: {slots.Count}");
        info.AppendLine($"Equipped Items: {GetEquippedCount()}");

        foreach (var slot in slots)
        {
            string itemInfo = string.IsNullOrEmpty(slot.equippedItemId) ? "Empty" : slot.equippedItemId;
            info.AppendLine($"  {slot.layer}: {itemInfo}");
        }

        return info.ToString();
    }

    /// <summary>
    /// Merge data from another ClothingSaveData instance
    /// </summary>
    public void MergeFrom(ClothingSaveData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        foreach (var otherSlot in other.slots)
        {
            var existingSlot = GetSlot(otherSlot.layer);
            if (existingSlot != null)
            {
                if (overwriteExisting)
                {
                    existingSlot.equippedItemId = otherSlot.equippedItemId;
                }
            }
            else
            {
                AddSlot(new ClothingSlotSaveData
                {
                    layer = otherSlot.layer,
                    equippedItemId = otherSlot.equippedItemId
                });
            }
        }
    }
}

/// <summary>
/// Save data for an individual clothing slot
/// </summary>
[System.Serializable]
public class ClothingSlotSaveData
{
    [Header("Slot Identity")]
    public ClothingLayer layer;

    [Header("Equipped Item")]
    public string equippedItemId = "";

    /// <summary>
    /// Checks if this slot is empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedItemId);

    /// <summary>
    /// Checks if this slot has an item equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// Create save data from a ClothingSlot
    /// </summary>
    public static ClothingSlotSaveData FromClothingSlot(ClothingSlot clothingSlot)
    {
        if (clothingSlot == null)
        {
            Debug.LogError("Cannot create ClothingSlotSaveData - ClothingSlot is null");
            return null;
        }

        return new ClothingSlotSaveData
        {
            layer = clothingSlot.layer,
            equippedItemId = clothingSlot.equippedItemId
        };
    }

    /// <summary>
    /// Apply this save data to a ClothingSlot
    /// </summary>
    public void ApplyToClothingSlot(ClothingSlot clothingSlot)
    {
        if (clothingSlot == null)
        {
            Debug.LogError("Cannot apply save data - ClothingSlot is null");
            return;
        }

        if (clothingSlot.layer != layer)
        {
            Debug.LogError($"Cannot apply save data - layer mismatch. Expected {layer}, got {clothingSlot.layer}");
            return;
        }

        if (IsEmpty)
        {
            clothingSlot.UnequipItem();
        }
        else
        {
            clothingSlot.EquipItem(equippedItemId);
        }
    }

    /// <summary>
    /// Validate that this slot save data is valid
    /// </summary>
    public bool IsValid()
    {
        // Layer must be a valid enum value
        if (!System.Enum.IsDefined(typeof(ClothingLayer), layer))
            return false;

        // If equipped item ID is not empty, it should be a valid format
        if (!string.IsNullOrEmpty(equippedItemId) && equippedItemId.Trim().Length == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Get a debug string representation
    /// </summary>
    public override string ToString()
    {
        string itemInfo = IsEmpty ? "Empty" : equippedItemId;
        return $"Slot[{layer}] = {itemInfo}";
    }

    /// <summary>
    /// Creates a copy of this slot save data
    /// </summary>
    public ClothingSlotSaveData CreateCopy()
    {
        return new ClothingSlotSaveData
        {
            layer = this.layer,
            equippedItemId = this.equippedItemId
        };
    }
}