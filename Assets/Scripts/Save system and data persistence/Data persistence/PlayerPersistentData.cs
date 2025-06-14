using UnityEngine;

/// <summary>
/// Updated PlayerPersistentData that works with the new inventory system
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

    public PlayerPersistentData()
    {
        // Default values
        inventoryData = new InventorySaveData();
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
    }
}