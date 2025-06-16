using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Centralized, efficient manager for tracking item pickup states in scenes
/// Replaces the individual save system approach with a lightweight state tracker
/// </summary>
public class SceneItemStateManager : MonoBehaviour, ISaveable
{
    public static SceneItemStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneItemStateManager";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Lightweight state tracking - just IDs and pickup status
    private HashSet<string> pickedUpItemIds = new HashSet<string>();
    private Dictionary<string, DroppedItemData> droppedItems = new Dictionary<string, DroppedItemData>();
    private int nextDroppedItemId = 1;

    // ISaveable implementation
    public string SaveID => saveID;
    public SaveDataCategory SaveCategory => SaveDataCategory.SceneDependent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Don't destroy on load - we want one per scene
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Check if we're loading from a save or entering scene fresh
        bool isLoadingFromSave = IsLoadingFromSave();

        if (isLoadingFromSave)
        {
            // Loading from save - wait for save system to call OnAfterLoad()
            DebugLog("SceneItemStateManager started - waiting for save data to load");
        }
        else
        {
            // Fresh scene entry (via doorway or new game) - apply states immediately
            DebugLog("SceneItemStateManager started - applying states immediately (fresh scene entry)");
            StartCoroutine(ApplyItemStatesAfterSceneLoad());
        }
    }

    /// <summary>
    /// Check if we're currently loading from a save file
    /// </summary>
    private bool IsLoadingFromSave()
    {
        // Method 1: Check SaveManager loading flag (if available)
        if (SaveManager.Instance != null)
        {
            // Try to get IsLoadingInProgress property via reflection if it exists
            var saveManagerType = SaveManager.Instance.GetType();
            var loadingProperty = saveManagerType.GetProperty("IsLoadingInProgress");
            if (loadingProperty != null)
            {
                return (bool)loadingProperty.GetValue(SaveManager.Instance);
            }
        }

        // Method 2: Check if SceneTransitionManager indicates a save load
        if (SceneTransitionManager.Instance != null)
        {
            // You could add a property to SceneTransitionManager to track this
            // For now, we'll use a simple heuristic
        }

        // Method 3: Fallback - assume fresh entry and let OnAfterLoad handle save loads
        return false;
    }

    /// <summary>
    /// Check if an item has been picked up in this scene
    /// </summary>
    public bool IsItemPickedUp(string itemId)
    {
        return pickedUpItemIds.Contains(itemId);
    }

    /// <summary>
    /// Mark an item as picked up (called by ItemPickupInteractable)
    /// </summary>
    public void MarkItemAsPickedUp(string itemId)
    {
        if (pickedUpItemIds.Add(itemId))
        {
            DebugLog($"Marked item {itemId} as picked up");

            // Immediately destroy the pickup GameObject for efficiency
            var pickup = FindPickupById(itemId);
            if (pickup != null)
            {
                DebugLog($"Destroying pickup GameObject for {itemId}");
                Destroy(pickup.gameObject);
            }
        }
    }

    /// <summary>
    /// Add a dropped item to the scene state
    /// </summary>
    public string AddDroppedItem(ItemData itemData, Vector3 position, float rotation = 0f)
    {
        string droppedId = $"dropped_item_{nextDroppedItemId++}";

        var droppedData = new DroppedItemData
        {
            id = droppedId,
            itemDataName = itemData.name,
            position = position,
            rotation = rotation
        };

        droppedItems[droppedId] = droppedData;
        DebugLog($"Added dropped item {droppedId} ({itemData.itemName}) at {position}");

        return droppedId;
    }

    /// <summary>
    /// Remove a dropped item (when picked up again)
    /// </summary>
    public void RemoveDroppedItem(string droppedId)
    {
        if (droppedItems.Remove(droppedId))
        {
            DebugLog($"Removed dropped item {droppedId}");
        }
    }

    /// <summary>
    /// Apply item states after scene load - hide picked up items, spawn dropped items
    /// </summary>
    private System.Collections.IEnumerator ApplyItemStatesAfterSceneLoad()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        DebugLog($"Applying item states - {pickedUpItemIds.Count} picked up, {droppedItems.Count} dropped");

        // Hide/destroy picked up items
        ApplyPickedUpStates();

        // Spawn dropped items
        SpawnDroppedItems();

        DebugLog("Item states applied successfully");
    }

    /// <summary>
    /// Hide or destroy items that have been picked up
    /// </summary>
    private void ApplyPickedUpStates()
    {
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);

        foreach (var pickup in allPickups)
        {
            if (IsItemPickedUp(pickup.InteractableID))
            {
                DebugLog($"Item {pickup.InteractableID} was previously picked up - destroying");
                Destroy(pickup.gameObject);
            }
        }
    }

    /// <summary>
    /// Spawn all dropped items in the scene
    /// </summary>
    private void SpawnDroppedItems()
    {
        foreach (var droppedData in droppedItems.Values)
        {
            SpawnDroppedItem(droppedData);
        }
    }

    /// <summary>
    /// Spawn a single dropped item
    /// </summary>
    private void SpawnDroppedItem(DroppedItemData dropData)
    {
        // Get the item pickup prefab from ItemDropSystem
        var itemDropSystem = ItemDropSystem.Instance;
        if (itemDropSystem == null)
        {
            DebugLog("ItemDropSystem not found - cannot spawn dropped items");
            return;
        }

        // Find the ItemData
        ItemData itemData = FindItemDataByName(dropData.itemDataName);
        if (itemData == null)
        {
            DebugLog($"ItemData {dropData.itemDataName} not found");
            return;
        }

        // Spawn using ItemDropSystem's prefab
        var prefab = itemDropSystem.GetPickupPrefab();
        if (prefab == null)
        {
            DebugLog("No pickup prefab available");
            return;
        }

        GameObject pickupObject = Instantiate(prefab, dropData.position, Quaternion.Euler(0, dropData.rotation, 0));
        pickupObject.name = $"DroppedItem_{dropData.id}";

        // Configure the pickup
        var pickupComponent = pickupObject.GetComponent<ItemPickupInteractable>();
        if (pickupComponent != null)
        {
            pickupComponent.SetItemData(itemData);
            pickupComponent.SetInteractableID(dropData.id);
            pickupComponent.MarkAsDroppedItem(); // Special flag for dropped items
        }

        DebugLog($"Spawned dropped item {dropData.id} ({itemData.itemName})");
    }

    /// <summary>
    /// Find an item pickup by ID
    /// </summary>
    private ItemPickupInteractable FindPickupById(string itemId)
    {
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
        return allPickups.FirstOrDefault(p => p.InteractableID == itemId);
    }

    /// <summary>
    /// Find ItemData by name
    /// </summary>
    private ItemData FindItemDataByName(string itemDataName)
    {
        ItemData itemData = Resources.Load<ItemData>(itemDataName);
        if (itemData != null) return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        return allItemData.FirstOrDefault(data => data.name == itemDataName);
    }

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        var saveData = new SceneItemStateSaveData
        {
            pickedUpItemIds = pickedUpItemIds.ToList(),
            droppedItems = droppedItems.Values.ToList(),
            nextDroppedItemId = nextDroppedItemId
        };

        DebugLog($"GetDataToSave called - saving {pickedUpItemIds.Count} picked up items, {droppedItems.Count} dropped items");

        // Debug: List the picked up items
        if (pickedUpItemIds.Count > 0)
        {
            DebugLog($"Picked up items being saved: {string.Join(", ", pickedUpItemIds)}");
        }

        return saveData;
    }

    public object ExtractRelevantData(object saveContainer)
    {
        if (saveContainer is SceneSaveData sceneData)
        {
            return sceneData.GetObjectData<SceneItemStateSaveData>(SaveID);
        }
        return saveContainer;
    }

    public void LoadSaveData(object data)
    {
        if (data is SceneItemStateSaveData saveData)
        {
            pickedUpItemIds = new HashSet<string>(saveData.pickedUpItemIds ?? new List<string>());
            droppedItems = (saveData.droppedItems ?? new List<DroppedItemData>())
                .ToDictionary(item => item.id, item => item);
            nextDroppedItemId = saveData.nextDroppedItemId;

            DebugLog($"Loaded state: {pickedUpItemIds.Count} picked up items, {droppedItems.Count} dropped items");
        }
    }

    public void OnBeforeSave()
    {
        DebugLog($"OnBeforeSave called - current state: {pickedUpItemIds.Count} picked up, {droppedItems.Count} dropped");

        // Debug: List what we're about to save
        if (pickedUpItemIds.Count > 0)
        {
            DebugLog($"About to save picked up items: {string.Join(", ", pickedUpItemIds)}");
        }
    }

    public void OnAfterLoad()
    {
        DebugLog("Item state manager loaded successfully");

        // NOW apply the states after save data has been loaded
        StartCoroutine(ApplyItemStatesAfterSceneLoad());
    }

    #endregion

    #region Public API for Item Systems

    /// <summary>
    /// Drop an item into the scene (called by inventory system)
    /// </summary>
    public static bool DropItemIntoScene(ItemData itemData, Vector3? position = null)
    {
        if (Instance == null || itemData == null) return false;

        Vector3 dropPosition = position ?? CalculateDropPosition();
        string droppedId = Instance.AddDroppedItem(itemData, dropPosition);

        // Immediately spawn the item
        var droppedData = Instance.droppedItems[droppedId];
        Instance.SpawnDroppedItem(droppedData);

        return true;
    }

    /// <summary>
    /// Mark a dropped item as picked up (called by ItemPickupInteractable)
    /// </summary>
    public static void OnDroppedItemPickedUp(string droppedId)
    {
        Instance?.RemoveDroppedItem(droppedId);
    }

    private static Vector3 CalculateDropPosition()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return Vector3.zero;

        Vector3 dropPosition = player.transform.position + player.transform.forward * 2f;
        dropPosition.y += 0.5f;

        // Simple ground detection
        if (Physics.Raycast(dropPosition, Vector3.down, out RaycastHit hit, 10f))
        {
            dropPosition.y = hit.point.y + 0.1f;
        }

        return dropPosition;
    }

    #endregion

    #region Debug Methods

    [Button("Debug Save System Integration")]
    public void DebugSaveSystemIntegration()
    {
        Debug.Log("=== SAVE SYSTEM INTEGRATION DEBUG ===");
        Debug.Log($"SaveID: {SaveID}");
        Debug.Log($"SaveCategory: {SaveCategory}");
        Debug.Log($"Current picked up items: {pickedUpItemIds.Count}");

        if (pickedUpItemIds.Count > 0)
        {
            Debug.Log($"Picked up items: {string.Join(", ", pickedUpItemIds)}");
        }

        // Test if this object would be found by the save system
        var saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.SceneDependent)
            .ToArray();

        bool foundSelf = saveableObjects.Contains(this);
        Debug.Log($"This SceneItemStateManager found by save system: {foundSelf}");
        Debug.Log($"Total scene-dependent saveables found: {saveableObjects.Length}");

        foreach (var saveable in saveableObjects)
        {
            Debug.Log($"  - {saveable.SaveID} ({saveable.GetType().Name})");
        }
    }

    [Button("Clear All States")]
    public void ClearAllStates()
    {
        pickedUpItemIds.Clear();
        droppedItems.Clear();
        nextDroppedItemId = 1;
        Debug.Log("All item states cleared");
    }

    #endregion

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneItemStateManager] {message}");
        }
    }
}

/// <summary>
/// Lightweight save data for scene item states
/// </summary>
[System.Serializable]
public class SceneItemStateSaveData
{
    public List<string> pickedUpItemIds;
    public List<DroppedItemData> droppedItems;
    public int nextDroppedItemId;
}