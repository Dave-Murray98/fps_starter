using UnityEngine;

/// <summary>
/// Updated ItemDropSystem that works with SceneItemStateManager
/// Simplified and more reliable than the previous version
/// </summary>
public class ItemDropSystem : MonoBehaviour
{
    public static ItemDropSystem Instance { get; private set; }

    [Header("Drop Settings")]
    [SerializeField] private GameObject itemPickupPrefab;
    [SerializeField] private float dropDistance = 2f;
    [SerializeField] private float dropHeight = 0.5f;
    [SerializeField] private LayerMask groundLayerMask = 1;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    public GameObject GetPickupPrefab() => itemPickupPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (itemPickupPrefab == null)
        {
            CreateDefaultPickupPrefab();
        }
    }

    /// <summary>
    /// Drop an item from inventory into the scene
    /// </summary>
    public bool DropItem(ItemData itemData, Vector3? dropPosition = null)
    {
        if (itemData == null)
        {
            DebugLog("Cannot drop null ItemData");
            return false;
        }

        Vector3 finalDropPosition = dropPosition ?? CalculateDropPosition();

        // Use the SceneItemStateManager to handle the drop
        return SceneItemStateManager.DropItemFromInventoryIntoScene(itemData, finalDropPosition);
    }

    /// <summary>
    /// Calculate where to drop an item relative to the player
    /// </summary>
    private Vector3 CalculateDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            DebugLog("No player found - dropping at world origin");
            return Vector3.zero;
        }

        // Calculate position in front of player
        Vector3 playerForward = player.transform.forward;
        Vector3 dropPosition = player.transform.position + playerForward * dropDistance;

        // Add some height
        dropPosition.y += dropHeight;

        // Raycast down to find ground
        if (Physics.Raycast(dropPosition, Vector3.down, out RaycastHit hit, 10f, groundLayerMask))
        {
            dropPosition.y = hit.point.y + 0.1f; // Slightly above ground
        }

        return dropPosition;
    }

    /// <summary>
    /// Create a basic pickup prefab if none is assigned
    /// </summary>
    private void CreateDefaultPickupPrefab()
    {
        DebugLog("No item pickup prefab assigned - creating basic one");

        GameObject prefab = new GameObject("DefaultItemPickup");

        // Add collider
        var collider = prefab.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = Vector3.one;

        // Add visual (simple cube)
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(prefab.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.5f;

        // Remove the primitive's collider (we have our own)
        Destroy(visual.GetComponent<Collider>());

        // Add the pickup component
        prefab.AddComponent<ItemPickupInteractable>();

        itemPickupPrefab = prefab;
        DebugLog("Created default pickup prefab");
    }

    #region Public API

    /// <summary>
    /// Drop item from inventory (called by inventory UI)
    /// </summary>
    public static bool DropItemFromInventory(string itemId)
    {
        if (Instance == null)
        {
            Debug.LogError("ItemDropSystem instance not found");
            return false;
        }

        // Get the item from inventory
        var inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            Debug.LogError("PersistentInventoryManager not found");
            return false;
        }

        var inventoryItem = inventory.InventoryData.GetItem(itemId);
        if (inventoryItem?.ItemData == null)
        {
            Debug.LogError($"Item {itemId} not found in inventory");
            return false;
        }

        ItemData itemData = inventoryItem.ItemData;

        // Remove from inventory first
        if (inventory.RemoveItem(itemId))
        {
            // Then drop into scene using Scene system
            bool success = Instance.DropItem(itemData);
            if (success)
            {
                Instance.DebugLog($"Successfully dropped {itemData.itemName} from inventory");
            }
            else
            {
                Instance.DebugLog($"Failed to drop {itemData.itemName} - item removed from inventory but not spawned");
            }
            return success;
        }
        else
        {
            Debug.LogError($"Failed to remove item {itemId} from inventory");
            return false;
        }
    }

    /// <summary>
    /// Get the pickup prefab (used by SceneItemStateManager)
    /// </summary>
    public GameObject GetItemPickupPrefab()
    {
        return itemPickupPrefab;
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ItemDropSystem] {message}");
        }
    }
}