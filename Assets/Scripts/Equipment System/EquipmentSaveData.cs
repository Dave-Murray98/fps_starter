using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Save data for the equipment system
/// </summary>
[System.Serializable]
public class EquipmentSaveData
{
    [Header("Hotkey Assignments")]
    public List<HotkeyBinding> hotkeyBindings = new List<HotkeyBinding>();

    [Header("Equipped Item")]
    public EquippedItemData equippedItem = new EquippedItemData();

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
        return hotkeyBindings.Count == 10;
    }
}