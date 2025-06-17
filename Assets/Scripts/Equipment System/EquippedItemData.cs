using UnityEngine;


/// <summary>
/// Data for currently equipped item
/// </summary>
[System.Serializable]
public class EquippedItemData
{
    [Header("Equipped Item")]
    public string equippedItemId;
    public string itemDataName;
    public ItemType itemType;

    [Header("Source")]
    public int sourceHotkeySlot = -1;  // Which hotkey slot this came from (-1 = direct equip)
    public bool isEquippedFromHotkey;  // True if equipped via hotkey, false if via inventory right-click

    [Header("State")]
    public bool isEquipped;

    public EquippedItemData()
    {
        Clear();
    }

    /// <summary>
    /// Equip an item from inventory (right-click)
    /// </summary>
    public void EquipFromInventory(string itemId, ItemData itemData)
    {
        equippedItemId = itemId;
        itemDataName = itemData.name;
        itemType = itemData.itemType;
        sourceHotkeySlot = -1;
        isEquippedFromHotkey = false;
        isEquipped = true;
    }

    /// <summary>
    /// Equip an item from hotkey
    /// </summary>
    public void EquipFromHotkey(string itemId, ItemData itemData, int hotkeySlot)
    {
        equippedItemId = itemId;
        itemDataName = itemData.name;
        itemType = itemData.itemType;
        sourceHotkeySlot = hotkeySlot;
        isEquippedFromHotkey = true;
        isEquipped = true;
    }

    /// <summary>
    /// Clear equipped item
    /// </summary>
    public void Clear()
    {
        equippedItemId = "";
        itemDataName = "";
        itemType = ItemType.Consumable; // Default
        sourceHotkeySlot = -1;
        isEquippedFromHotkey = false;
        isEquipped = false;
    }

    /// <summary>
    /// Get the ItemData for equipped item
    /// </summary>
    public ItemData GetItemData()
    {
        if (!isEquipped || string.IsNullOrEmpty(itemDataName))
            return null;

        return Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + itemDataName);
    }

    /// <summary>
    /// Check if this equipped item matches the given item ID
    /// </summary>
    public bool IsEquipped(string itemId)
    {
        return isEquipped && equippedItemId == itemId;
    }
}