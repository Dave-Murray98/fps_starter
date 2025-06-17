using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Save data for the equipment system
/// FIXED: Added copy constructor for scene transitions
/// </summary>
[System.Serializable]
public class EquipmentSaveData
{
    [Header("Hotkey Assignments")]
    public List<HotkeyBinding> hotkeyBindings = new List<HotkeyBinding>();

    [Header("Equipped Item")]
    public EquippedItemData equippedItem = new EquippedItemData();

    // Default constructor
    public EquipmentSaveData()
    {
        // Initialize 10 hotkey slots (1-0)
        hotkeyBindings = new List<HotkeyBinding>();
        for (int i = 1; i <= 10; i++)
        {
            hotkeyBindings.Add(new HotkeyBinding(i));
        }

        equippedItem = new EquippedItemData();
    }

    // CRITICAL FIX: Copy constructor for scene transitions
    public EquipmentSaveData(EquipmentSaveData other)
    {
        // Deep copy equipped item
        equippedItem = new EquippedItemData(other.equippedItem);

        // Deep copy hotkey bindings using copy constructor
        hotkeyBindings = new List<HotkeyBinding>();
        foreach (var binding in other.hotkeyBindings)
        {
            hotkeyBindings.Add(new HotkeyBinding(binding)); // Uses HotkeyBinding copy constructor
        }

        // Debug log to verify copy worked
        var assignedCount = hotkeyBindings.FindAll(h => h.isAssigned).Count;
        Debug.Log($"[EquipmentSaveData] Copy constructor: Copied {hotkeyBindings.Count} hotkey slots, {assignedCount} assigned");

        // Debug first hotkey specifically
        if (hotkeyBindings.Count > 0 && hotkeyBindings[0].isAssigned)
        {
            Debug.Log($"[EquipmentSaveData] Copy constructor: Hotkey 1 = {hotkeyBindings[0].itemDataName} (ID: {hotkeyBindings[0].itemId})");
        }
    }

    /// <summary>
    /// Get hotkey binding for a specific slot
    /// </summary>
    public HotkeyBinding GetHotkeyBinding(int slotNumber)
    {
        return hotkeyBindings.Find(h => h.slotNumber == slotNumber);
    }

    /// <summary>
    /// Validate that save data is consistent
    /// </summary>
    public bool IsValid()
    {
        return hotkeyBindings != null && hotkeyBindings.Count == 10;
    }
}