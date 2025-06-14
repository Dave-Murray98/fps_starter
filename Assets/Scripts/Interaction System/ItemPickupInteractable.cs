using UnityEngine;
using DG.Tweening;

/// <summary>
/// Interactable for picking up items and adding them to inventory
/// Integrates with the PersistentInventoryManager system
/// </summary>
public class ItemPickupInteractable : InteractableBase
{
    [Header("Item Settings")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private string pickupMessage = "";

    // State
    private bool isPickedUp = false;

    protected override void Awake()
    {
        base.Awake();

        // Set default interaction prompt based on item
        if (string.IsNullOrEmpty(interactionPrompt) && itemData != null)
        {
            interactionPrompt = $"pick up {itemData.itemName}";
        }
    }

    protected override void Start()
    {
        base.Start();
    }


    #region IInteractable Implementation

    public override bool CanInteract
    {
        get
        {
            return base.CanInteract && !isPickedUp && itemData != null;
        }
    }

    public override string GetInteractionPrompt()
    {
        if (isPickedUp || itemData == null)
            return "";

        if (!string.IsNullOrEmpty(pickupMessage))
            return pickupMessage;

        string quantityText = quantity > 1 ? $" ({quantity})" : "";
        return $"pick up {itemData.itemName}{quantityText}";
    }

    protected override bool PerformInteraction(GameObject player)
    {
        if (isPickedUp || itemData == null)
        {
            DebugLog("Cannot pick up - item already picked up or no item data");
            return false;
        }

        // Try to add to inventory
        var inventory = PersistentInventoryManager.Instance;
        if (inventory == null)
        {
            DebugLog("No PersistentInventoryManager found");
            return false;
        }

        // Check if inventory has space
        if (!inventory.HasSpaceForItem(itemData))
        {
            DebugLog("Inventory is full - cannot pick up item");
            ShowInventoryFullMessage();
            return false;
        }

        // Add to inventory
        bool success = inventory.AddItem(itemData);
        if (success)
        {
            DebugLog($"Successfully added {itemData.itemName} to inventory");
            HandleSuccessfulPickup();
            return true;
        }
        else
        {
            DebugLog($"Failed to add {itemData.itemName} to inventory");
            return false;
        }
    }

    #endregion

    private void HandleSuccessfulPickup()
    {
        isPickedUp = true;

        // Spawn pickup effect if configured
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, transform.rotation);
        }

        // Simply destroy the pickup object
        Destroy(gameObject);
    }

    private void ShowInventoryFullMessage()
    {
        // You can integrate this with your UI system to show a message
        Debug.Log("Inventory is full!");

        // Example: Show UI message (you'd integrate this with your UIManager)
        // UIManager.Instance?.ShowMessage("Inventory is full!");
    }

    #region Save/Load Implementation

    protected override object GetCustomSaveData()
    {
        return new ItemPickupSaveData
        {
            isPickedUp = this.isPickedUp
        };
    }

    protected override void LoadCustomSaveData(object customData)
    {
        if (customData is ItemPickupSaveData pickupData)
        {
            isPickedUp = pickupData.isPickedUp;
        }
    }

    protected override void RefreshVisualState()
    {
        if (isPickedUp)
        {
            // Item was picked up - destroy it
            Destroy(gameObject);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually set the item data for this pickup
    /// </summary>
    public void SetItemData(ItemData newItemData, int newQuantity = 1)
    {
        itemData = newItemData;
        quantity = newQuantity;

        // Update interaction prompt
        if (itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
        }
    }

    /// <summary>
    /// Get the item data for this pickup
    /// </summary>
    public ItemData GetItemData() => itemData;

    /// <summary>
    /// Get the quantity of items in this pickup
    /// </summary>
    public int GetQuantity() => quantity;

    /// <summary>
    /// Check if this item has been picked up
    /// </summary>
    public bool IsPickedUp => isPickedUp;

    #endregion
}
