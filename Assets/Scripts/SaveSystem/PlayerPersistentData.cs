using UnityEngine;

/// <summary>
/// Data that persists between scenes when using doorways
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

    // Add more systems here as you create them:
    // [Header("Inventory")]
    // public InventoryData inventory;

    // [Header("Equipment")]
    // public EquipmentData equipment;

    // [Header("Quests")]
    // public QuestData quests;

    public PlayerPersistentData()
    {
        // Default values
    }

    public PlayerPersistentData(PlayerPersistentData other)
    {
        // Copy constructor
        this.currentHealth = other.currentHealth;
        this.canJump = other.canJump;
        this.canSprint = other.canSprint;
        this.canCrouch = other.canCrouch;
    }
}