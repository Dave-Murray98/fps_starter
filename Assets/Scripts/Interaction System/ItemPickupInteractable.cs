using UnityEngine;

/// <summary>
/// Efficient item pickup that works with SceneItemStateManager
/// No longer needs to save individual state - much cleaner and more scalable
/// </summary>
public class ItemPickupInteractable : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    [SerializeField] private ItemData itemData;
    [SerializeField] private int quantity = 1;
    [SerializeField] private string interactableID;
    [SerializeField] private bool autoGenerateID = true;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private string interactionPrompt = "";

    [Header("Feedback")]
    [SerializeField] private GameObject pickupEffect;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Special flags
    private bool isDroppedItem = false;

    // IInteractable implementation
    public string InteractableID => interactableID;
    public Transform Transform => transform;
    public bool CanInteract => enabled && gameObject.activeInHierarchy && itemData != null;
    public float InteractionRange => interactionRange;

    private void Awake()
    {
        if (autoGenerateID && string.IsNullOrEmpty(interactableID))
        {
            GenerateUniqueID();
        }

        if (string.IsNullOrEmpty(interactionPrompt) && itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
        }
    }

    private void Start()
    {
        // Check if this item was already picked up
        if (SceneItemStateManager.Instance != null && !isDroppedItem)
        {
            if (SceneItemStateManager.Instance.IsItemPickedUp(interactableID))
            {
                DebugLog($"Item {interactableID} was previously picked up - destroying immediately");
                Destroy(gameObject);
                return;
            }
        }

        DebugLog($"Item pickup {interactableID} initialized");
    }

    #region IInteractable Implementation

    public string GetInteractionPrompt()
    {
        if (!CanInteract) return "";
        return interactionPrompt;
    }

    public bool Interact(GameObject player)
    {
        if (!CanInteract)
        {
            DebugLog("Cannot interact - item disabled or no data");
            return false;
        }

        // Try to add to inventory
        var inventory = PersistentInventoryManager.Instance;
        if (inventory == null)
        {
            DebugLog("No PersistentInventoryManager found");
            return false;
        }

        if (!inventory.HasSpaceForItem(itemData))
        {
            DebugLog("Inventory is full");
            ShowInventoryFullMessage();
            return false;
        }

        if (inventory.AddItem(itemData))
        {
            DebugLog($"Successfully added {itemData.itemName} to inventory");
            HandleSuccessfulPickup();
            return true;
        }

        DebugLog($"Failed to add {itemData.itemName} to inventory");
        return false;
    }

    public void OnPlayerEnterRange(GameObject player)
    {
        DebugLog($"Player entered range of {interactableID}");
    }

    public void OnPlayerExitRange(GameObject player)
    {
        DebugLog($"Player exited range of {interactableID}");
    }

    #endregion

    private void HandleSuccessfulPickup()
    {
        // Spawn pickup effect
        if (pickupEffect != null)
        {
            Instantiate(pickupEffect, transform.position, transform.rotation);
        }

        if (isDroppedItem)
        {
            // Dropped items: notify the state manager and destroy immediately
            SceneItemStateManager.OnDroppedItemPickedUp(interactableID);
            DebugLog($"Dropped item {interactableID} picked up - destroying GameObject");
            Destroy(gameObject);
        }
        else
        {
            // Scene items: mark as picked up in state manager (this will destroy the GameObject)
            SceneItemStateManager.Instance?.MarkItemAsPickedUp(interactableID);
            DebugLog($"Scene item {interactableID} marked as picked up");
            // Note: MarkItemAsPickedUp() will destroy this GameObject, so no need to do it here
        }
    }

    private void ShowInventoryFullMessage()
    {
        Debug.Log("Inventory is full!");
        // TODO: Integrate with UI system
    }

    private void GenerateUniqueID()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string position = transform.position.ToString("F2");
        interactableID = $"Item_{sceneName}_{position}";
    }

    #region Public Methods for ItemDropSystem

    /// <summary>
    /// Set the item data for this pickup
    /// </summary>
    public void SetItemData(ItemData newItemData, int newQuantity = 1)
    {
        itemData = newItemData;
        quantity = newQuantity;

        if (itemData != null)
        {
            string quantityText = quantity > 1 ? $" ({quantity})" : "";
            interactionPrompt = $"pick up {itemData.itemName}{quantityText}";
        }
    }

    /// <summary>
    /// Set a custom interactable ID
    /// </summary>
    public void SetInteractableID(string newID)
    {
        interactableID = newID;
        autoGenerateID = false;
    }

    /// <summary>
    /// Mark this as a dropped item (different behavior when picked up)
    /// </summary>
    public void MarkAsDroppedItem()
    {
        isDroppedItem = true;
        DebugLog($"Item {interactableID} marked as dropped item");
    }

    /// <summary>
    /// Get the item data
    /// </summary>
    public ItemData GetItemData() => itemData;

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[EfficientItemPickup:{interactableID}] {message}");
        }
    }
}