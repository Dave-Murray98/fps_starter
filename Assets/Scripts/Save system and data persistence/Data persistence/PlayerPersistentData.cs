using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Updated PlayerPersistentData that works with the new inventory system
/// FIXED: Copy constructor no longer triggers hotkey assignment side effects
/// </summary>
[System.Serializable]
public class PlayerPersistentData
{
    [Header("Health System")]
    public float currentHealth = 100f;

    [Header("Abilities")]
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;

    [Header("Inventory")]
    public InventorySaveData inventoryData;

    [Header("Equipment")]
    public EquipmentSaveData equipmentData;

    public PlayerPersistentData()
    {
        // Default values
        inventoryData = new InventorySaveData();
        equipmentData = new EquipmentSaveData();
    }

    public PlayerPersistentData(PlayerPersistentData other)
    {
        // Copy constructor
        this.currentHealth = other.currentHealth;
        this.canJump = other.canJump;
        this.canSprint = other.canSprint;
        this.canCrouch = other.canCrouch;

        // Deep copy inventory data
        if (other.inventoryData != null)
        {
            this.inventoryData = new InventorySaveData(other.inventoryData.gridWidth, other.inventoryData.gridHeight);
            this.inventoryData.nextItemId = other.inventoryData.nextItemId;

            // Copy all items
            foreach (var item in other.inventoryData.items)
            {
                var itemCopy = new InventoryItemSaveData(item.itemID, item.itemDataName, item.gridPosition, item.currentRotation);
                itemCopy.stackCount = item.stackCount;
                this.inventoryData.AddItem(itemCopy);
            }
        }
        else
        {
            this.inventoryData = new InventorySaveData();
        }

        // Deep copy equipment data
        if (other.equipmentData != null)
        {
            this.equipmentData = new EquipmentSaveData();

            // Direct copy of equipment data (like you already do for inventory)
            for (int i = 0; i < other.equipmentData.hotkeyBindings.Count; i++)
            {
                var originalBinding = other.equipmentData.hotkeyBindings[i];
                var newBinding = new HotkeyBinding(originalBinding.slotNumber);

                if (originalBinding.isAssigned)
                {
                    // Direct property assignment - NO method calls!
                    newBinding.itemId = originalBinding.itemId;
                    newBinding.itemDataName = originalBinding.itemDataName;
                    newBinding.isAssigned = originalBinding.isAssigned;
                    newBinding.currentStackIndex = originalBinding.currentStackIndex;
                    newBinding.stackedItemIds = new List<string>(originalBinding.stackedItemIds);
                }

                this.equipmentData.hotkeyBindings[i] = newBinding;
            }

            // Copy equipped item data
            this.equipmentData.equippedItem = new EquippedItemData();
            if (other.equipmentData.equippedItem.isEquipped)
            {
                this.equipmentData.equippedItem.equippedItemId = other.equipmentData.equippedItem.equippedItemId;
                this.equipmentData.equippedItem.itemDataName = other.equipmentData.equippedItem.itemDataName;
                this.equipmentData.equippedItem.itemType = other.equipmentData.equippedItem.itemType;
                this.equipmentData.equippedItem.sourceHotkeySlot = other.equipmentData.equippedItem.sourceHotkeySlot;
                this.equipmentData.equippedItem.isEquippedFromHotkey = other.equipmentData.equippedItem.isEquippedFromHotkey;
                this.equipmentData.equippedItem.isEquipped = other.equipmentData.equippedItem.isEquipped;
            }
        }
        else
        {
            Debug.Log("Equipment data is null, creating new EquipmentSaveData");
            this.equipmentData = new EquipmentSaveData();
        }
    }
}