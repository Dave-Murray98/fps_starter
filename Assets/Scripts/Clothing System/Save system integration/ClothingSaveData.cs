using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Complete save data structure for the clothing system.
/// Contains all clothing slots and their equipped items with validation.
/// </summary>
[System.Serializable]
public class ClothingSaveData
{
    [Header("Clothing Slots")]
    public List<ClothingSlotSaveData> slotData = new List<ClothingSlotSaveData>();

    [Header("System State")]
    public float lastWearUpdateTime = 0f;
    public bool degradationEnabled = true;

    public ClothingSaveData()
    {
        slotData = new List<ClothingSlotSaveData>();
        lastWearUpdateTime = Time.time;
        degradationEnabled = true;
    }

    /// <summary>
    /// Add slot save data
    /// </summary>
    public void AddSlotData(ClothingSlotSaveData slotSaveData)
    {
        if (slotSaveData != null && !string.IsNullOrEmpty(slotSaveData.slotID))
        {
            // Remove existing data for this slot if it exists
            RemoveSlotData(slotSaveData.slotID);
            slotData.Add(slotSaveData);
        }
    }

    /// <summary>
    /// Remove slot data by slot ID
    /// </summary>
    public bool RemoveSlotData(string slotID)
    {
        for (int i = slotData.Count - 1; i >= 0; i--)
        {
            if (slotData[i].slotID == slotID)
            {
                slotData.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get slot data by slot ID
    /// </summary>
    public ClothingSlotSaveData GetSlotData(string slotID)
    {
        foreach (var slot in slotData)
        {
            if (slot.slotID == slotID)
                return slot;
        }
        return null;
    }

    /// <summary>
    /// Get all occupied slots
    /// </summary>
    public List<ClothingSlotSaveData> GetOccupiedSlots()
    {
        var occupiedSlots = new List<ClothingSlotSaveData>();
        foreach (var slot in slotData)
        {
            if (slot.isOccupied)
                occupiedSlots.Add(slot);
        }
        return occupiedSlots;
    }

    /// <summary>
    /// Get count of equipped items
    /// </summary>
    public int GetEquippedItemCount()
    {
        int count = 0;
        foreach (var slot in slotData)
        {
            if (slot.isOccupied)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Clear all slot data
    /// </summary>
    public void Clear()
    {
        slotData.Clear();
    }

    /// <summary>
    /// Validate that the save data is consistent and complete
    /// </summary>
    public bool IsValid()
    {
        // Check that we have all expected slot IDs
        var expectedSlotIDs = new HashSet<string>
        {
            "head_upper", "head_lower",
            "torso_inner", "torso_outer",
            "hands",
            "legs_inner", "legs_outer",
            "socks", "shoes"
        };

        var foundSlotIDs = new HashSet<string>();

        foreach (var slot in slotData)
        {
            // Check for null or invalid slot data
            if (slot == null || string.IsNullOrEmpty(slot.slotID))
                return false;

            // Check for duplicate slot IDs
            if (foundSlotIDs.Contains(slot.slotID))
                return false;

            foundSlotIDs.Add(slot.slotID);

            // Validate slot consistency
            if (slot.isOccupied && string.IsNullOrEmpty(slot.equippedItemID))
                return false;

            if (!slot.isOccupied && !string.IsNullOrEmpty(slot.equippedItemID))
                return false;
        }

        // We should have data for all expected slots
        return foundSlotIDs.SetEquals(expectedSlotIDs);
    }

    /// <summary>
    /// Ensure all required slots are present (add missing ones)
    /// </summary>
    public void EnsureAllSlotsPresent()
    {
        var expectedSlots = new Dictionary<string, (ClothingType type, ClothingLayer layer)>
        {
            { "head_upper", (ClothingType.Head, ClothingLayer.Upper) },
            { "head_lower", (ClothingType.Head, ClothingLayer.Lower) },
            { "torso_inner", (ClothingType.Torso, ClothingLayer.Lower) },
            { "torso_outer", (ClothingType.Torso, ClothingLayer.Upper) },
            { "hands", (ClothingType.Hands, ClothingLayer.Single) },
            { "legs_inner", (ClothingType.Legs, ClothingLayer.Lower) },
            { "legs_outer", (ClothingType.Legs, ClothingLayer.Upper) },
            { "socks", (ClothingType.Socks, ClothingLayer.Single) },
            { "shoes", (ClothingType.Shoes, ClothingLayer.Single) }
        };

        var existingSlotIDs = new HashSet<string>();
        foreach (var slot in slotData)
        {
            if (!string.IsNullOrEmpty(slot.slotID))
                existingSlotIDs.Add(slot.slotID);
        }

        // Add missing slots
        foreach (var expectedSlot in expectedSlots)
        {
            if (!existingSlotIDs.Contains(expectedSlot.Key))
            {
                var newSlotData = new ClothingSlotSaveData
                {
                    slotID = expectedSlot.Key,
                    equippedItemID = "",
                    isOccupied = false
                };
                slotData.Add(newSlotData);
            }
        }
    }

    /// <summary>
    /// Get debug information about this save data
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== CLOTHING SAVE DATA DEBUG ===");
        info.AppendLine($"Valid: {IsValid()}");
        info.AppendLine($"Equipped Items: {GetEquippedItemCount()}/{slotData.Count}");
        info.AppendLine($"Last Wear Update: {lastWearUpdateTime}");
        info.AppendLine($"Degradation Enabled: {degradationEnabled}");
        info.AppendLine();

        info.AppendLine("Slot Details:");
        foreach (var slot in slotData)
        {
            string status = slot.isOccupied ? $"EQUIPPED: {slot.equippedItemID}" : "EMPTY";
            info.AppendLine($"  {slot.slotID}: {status}");
        }

        var occupiedSlots = GetOccupiedSlots();
        if (occupiedSlots.Count > 0)
        {
            info.AppendLine();
            info.AppendLine("Equipped Items:");
            foreach (var slot in occupiedSlots)
            {
                info.AppendLine($"  {slot.slotID} -> {slot.equippedItemID}");
            }
        }

        return info.ToString();
    }

    /// <summary>
    /// Create a deep copy of this save data
    /// </summary>
    public ClothingSaveData CreateCopy()
    {
        var copy = new ClothingSaveData();
        copy.lastWearUpdateTime = lastWearUpdateTime;
        copy.degradationEnabled = degradationEnabled;

        foreach (var slot in slotData)
        {
            copy.slotData.Add(new ClothingSlotSaveData
            {
                slotID = slot.slotID,
                equippedItemID = slot.equippedItemID,
                isOccupied = slot.isOccupied
            });
        }

        return copy;
    }

    /// <summary>
    /// Merge data from another save data instance
    /// </summary>
    public void MergeFrom(ClothingSaveData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        // Merge system state
        if (overwriteExisting)
        {
            lastWearUpdateTime = other.lastWearUpdateTime;
            degradationEnabled = other.degradationEnabled;
        }

        // Merge slot data
        foreach (var otherSlot in other.slotData)
        {
            var existingSlot = GetSlotData(otherSlot.slotID);
            if (existingSlot != null)
            {
                if (overwriteExisting)
                {
                    existingSlot.equippedItemID = otherSlot.equippedItemID;
                    existingSlot.isOccupied = otherSlot.isOccupied;
                }
            }
            else
            {
                // Add new slot data
                AddSlotData(new ClothingSlotSaveData
                {
                    slotID = otherSlot.slotID,
                    equippedItemID = otherSlot.equippedItemID,
                    isOccupied = otherSlot.isOccupied
                });
            }
        }
    }
}
