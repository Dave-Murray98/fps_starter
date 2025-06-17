using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Updated PlayerPersistentData that works with the new inventory system
/// FIXED: Simplified copy constructor using proper copy constructors for equipment data
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

    // Default constructor
    public PlayerPersistentData()
    {
        // Default values
        inventoryData = new InventorySaveData();
        equipmentData = new EquipmentSaveData();
    }

    // SIMPLIFIED: Copy constructor using proper copy constructors
    public PlayerPersistentData(PlayerPersistentData other)
    {
        // Copy basic player data
        currentHealth = other.currentHealth;
        canJump = other.canJump;
        canSprint = other.canSprint;
        canCrouch = other.canCrouch;

        // Deep copy inventory data
        if (other.inventoryData != null)
        {
            inventoryData = new InventorySaveData(other.inventoryData.gridWidth, other.inventoryData.gridHeight);
            inventoryData.nextItemId = other.inventoryData.nextItemId;

            // Copy all items
            foreach (var item in other.inventoryData.items)
            {
                var itemCopy = new InventoryItemSaveData(item.itemID, item.itemDataName, item.gridPosition, item.currentRotation);
                itemCopy.stackCount = item.stackCount;
                inventoryData.AddItem(itemCopy);
            }
        }
        else
        {
            inventoryData = new InventorySaveData();
        }

        // SIMPLIFIED: Use the EquipmentSaveData copy constructor
        if (other.equipmentData != null)
        {
            equipmentData = new EquipmentSaveData(other.equipmentData);

            // Debug log to verify copy worked
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            Debug.Log($"[PlayerPersistentData] Copy constructor: Copied equipment data with {assignedCount} hotkey assignments");

            // Debug first hotkey specifically
            if (equipmentData.hotkeyBindings?.Count > 0)
            {
                var binding1 = equipmentData.hotkeyBindings.Find(h => h.slotNumber == 1);
                if (binding1?.isAssigned == true)
                {
                    Debug.Log($"[PlayerPersistentData] Copy constructor: Hotkey 1 = {binding1.itemDataName} (ID: {binding1.itemId})");
                }
            }
        }
        else
        {
            Debug.Log("[PlayerPersistentData] Equipment data is null, creating new EquipmentSaveData");
            equipmentData = new EquipmentSaveData();
        }
    }
}