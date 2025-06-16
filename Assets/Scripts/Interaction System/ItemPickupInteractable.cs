using UnityEngine;

/// <summary>
/// Updated ItemPickupInteractable that works with ItemStateManager
/// Much cleaner and more efficient than the previous version
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

    // Item type tracking
    private bool isDroppedInventoryItem = false;
    private bool isOriginalSceneItem = true;

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
        // Check if this original scene item was already collected
        if (isOriginalSceneItem && SceneItemStateManager.Instance != null)
        {
            if (SceneItemStateManager.Instance.IsOriginalItemCollected(interactableID))
            {
                DebugLog($"Original scene item {interactableID} was previously collected - destroying immediately");
                Destroy(gameObject);
                return;
            }
        }

        DebugLog($"Item pickup {interactableID} initialized (Original: {isOriginalSceneItem}, Dropped: {isDroppedInventoryItem})");
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

        // Handle based on item type
        if (isDroppedInventoryItem)
        {
            // Dropped inventory item: notify the  manager and destroy
            SceneItemStateManager.OnDroppedInventoryItemPickedUp(interactableID);
            DebugLog($"Dropped inventory item {interactableID} picked up - destroying GameObject");
        }
        else if (isOriginalSceneItem)
        {
            // Original scene item: mark as collected in  manager
            SceneItemStateManager.OnOriginalSceneItemPickedUp(interactableID);
            DebugLog($"Original scene item {interactableID} marked as collected - destroying GameObject");
        }

        // Destroy the GameObject
        Destroy(gameObject);
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

    #region Public Methods for Item Systems

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
    /// Mark this as a dropped inventory item (not an original scene item)
    /// </summary>
    public void MarkAsDroppedItem()
    {
        isDroppedInventoryItem = true;
        isOriginalSceneItem = false;
        DebugLog($"Item {interactableID} marked as dropped inventory item");
    }

    /// <summary>
    /// Mark this as an original scene item (default behavior)
    /// </summary>
    public void MarkAsOriginalSceneItem()
    {
        isOriginalSceneItem = true;
        isDroppedInventoryItem = false;
        DebugLog($"Item {interactableID} marked as original scene item");
    }

    /// <summary>
    /// Get the item data
    /// </summary>
    public ItemData GetItemData() => itemData;

    /// <summary>
    /// Check if this is a dropped inventory item
    /// </summary>
    public bool IsDroppedInventoryItem => isDroppedInventoryItem;

    /// <summary>
    /// Check if this is an original scene item
    /// </summary>
    public bool IsOriginalSceneItem => isOriginalSceneItem;

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ItemPickupInteractable:{interactableID}] {message}");
        }
    }
}