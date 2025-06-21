using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Scene Item State Management System
/// Single source of truth for all item states in scenes
/// Eliminates memory waste and synchronization issues
/// UPDATED: Now uses context-aware loading
/// </summary>
public class SceneItemStateManager : MonoBehaviour, ISaveable
{
    public static SceneItemStateManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveID = "SceneItemStateManager";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // SINGLE SOURCE OF TRUTH: Two simple collections
    private HashSet<string> collectedOriginalItems = new HashSet<string>();
    private Dictionary<string, DroppedItemData> droppedInventoryItems = new Dictionary<string, DroppedItemData>();

    // Tracking for ID generation
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
            //          DebugLog("SceneItemStateManager started - waiting for save data to load");
        }
        else
        {
            // Fresh scene entry (via doorway or new game) - apply states immediately
            //            DebugLog("SceneItemStateManager started - applying states immediately (fresh scene entry)");
            StartCoroutine(ApplyItemStatesAfterSceneLoad());
        }
    }

    /// <summary>
    /// Simple heuristic to detect if we're loading from a save file
    /// </summary>
    private bool IsLoadingFromSave()
    {
        // Check if SaveManager is currently in a loading operation
        if (SaveManager.Instance != null)
        {
            // You could add a property to SaveManager to track this
            // For now, we'll use a simple heuristic
        }

        // Fallback: assume fresh entry and let OnAfterLoad handle save loads
        return false;
    }

    #region Core Item State Management

    /// <summary>
    /// Mark an original scene item as collected
    /// </summary>
    public void MarkOriginalItemAsCollected(string itemId)
    {
        if (collectedOriginalItems.Add(itemId))
        {
            //DebugLog($"Marked original item {itemId} as collected");

            // Immediately destroy the pickup GameObject for efficiency
            var pickup = FindPickupById(itemId);
            if (pickup != null)
            {
                //DebugLog($"Destroying pickup GameObject for {itemId}");
                Destroy(pickup.gameObject);
            }
        }
    }

    /// <summary>
    /// Check if an original scene item has been collected
    /// </summary>
    public bool IsOriginalItemCollected(string itemId)
    {
        return collectedOriginalItems.Contains(itemId);
    }

    /// <summary>
    /// Add a dropped inventory item to the scene state
    /// </summary>
    public string AddDroppedInventoryItem(ItemData itemData, Vector3 position, float rotation = 0f)
    {
        string droppedId = $"dropped_item_{nextDroppedItemId++}";

        var droppedData = new DroppedItemData
        {
            id = droppedId,
            itemDataName = itemData.name,
            position = position,
            rotation = rotation
        };

        droppedInventoryItems[droppedId] = droppedData;
        // DebugLog($"Added dropped inventory item {droppedId} ({itemData.itemName}) at {position}");

        return droppedId;
    }

    /// <summary>
    /// Remove a dropped inventory item (when picked up again)
    /// </summary>
    public void RemoveDroppedInventoryItem(string droppedId)
    {
        if (droppedInventoryItems.Remove(droppedId))
        {
            //DebugLog($"Removed dropped inventory item {droppedId}");
        }
    }

    /// <summary>
    /// Restore an original item to the scene (when dropped from inventory)
    /// </summary>
    public void RestoreOriginalItem(string itemId)
    {
        if (collectedOriginalItems.Remove(itemId))
        {
            //DebugLog($"Restored original item {itemId} to scene");

            // Find and respawn the original item
            var originalPickup = FindOriginalItemById(itemId);
            if (originalPickup != null)
            {
                originalPickup.gameObject.SetActive(true);
            }
            else
            {
                DebugLog($"Warning: Could not find original pickup for {itemId} to restore");
            }
        }
    }

    #endregion

    #region Scene State Application

    /// <summary>
    /// Apply item states after scene load - hide collected items, spawn dropped items
    /// </summary>
    private System.Collections.IEnumerator ApplyItemStatesAfterSceneLoad()
    {
        // Wait for scene to fully load
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        //        DebugLog($"Applying item states - {collectedOriginalItems.Count} collected, {droppedInventoryItems.Count} dropped");

        // FIRST: Clean up any existing dropped items that shouldn't be there
        CleanupExistingDroppedItems();

        // THEN: Hide/destroy collected original items
        ApplyCollectedItemStates();

        // FINALLY: Spawn dropped inventory items that should exist
        SpawnDroppedInventoryItems();

        //  DebugLog("Item states applied successfully");
    }

    /// <summary>
    /// Clean up any existing dropped items that shouldn't exist according to our state
    /// This fixes the issue where items remain in scene after save/load cycles
    /// </summary>
    private void CleanupExistingDroppedItems()
    {
        // Find all existing dropped item pickups in the scene
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
        var droppedPickups = allPickups.Where(p => p.IsDroppedInventoryItem).ToArray();

        //    DebugLog($"Found {droppedPickups.Length} existing dropped items in scene");

        foreach (var pickup in droppedPickups)
        {
            string pickupId = pickup.InteractableID;

            // If this dropped item is NOT in our saved state, it shouldn't exist
            if (!droppedInventoryItems.ContainsKey(pickupId))
            {
                //DebugLog($"Removing orphaned dropped item: {pickupId}");
                Destroy(pickup.gameObject);
            }
            else
            {
                //DebugLog($"Keeping valid dropped item: {pickupId}");
            }
        }
    }

    /// <summary>
    /// Hide or destroy original items that have been collected
    /// </summary>
    private void ApplyCollectedItemStates()
    {
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);

        foreach (var pickup in allPickups)
        {
            if (IsOriginalItemCollected(pickup.InteractableID))
            {
                //DebugLog($"Original item {pickup.InteractableID} was collected - destroying");
                Destroy(pickup.gameObject);
            }
        }
    }

    /// <summary>
    /// Spawn all dropped inventory items that should exist in the scene
    /// Only spawns items that don't already exist
    /// </summary>
    private void SpawnDroppedInventoryItems()
    {
        foreach (var droppedData in droppedInventoryItems.Values)
        {
            // Check if this dropped item already exists in the scene
            var existingPickup = FindPickupById(droppedData.id);
            if (existingPickup != null)
            {
                //DebugLog($"Dropped item {droppedData.id} already exists in scene - skipping spawn");
                continue;
            }

            // Spawn the item since it doesn't exist
            SpawnDroppedItem(droppedData);
        }
    }

    /// <summary>
    /// Spawn a single dropped inventory item
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
        pickupObject.name = $"DroppedInventoryItem_{dropData.id}";

        // Configure the pickup
        var pickupComponent = pickupObject.GetComponent<ItemPickupInteractable>();
        if (pickupComponent != null)
        {
            pickupComponent.SetItemData(itemData);
            pickupComponent.SetInteractableID(dropData.id);
            pickupComponent.MarkAsDroppedItem(); // Special flag for dropped items
        }

        // DebugLog($"Spawned dropped inventory item {dropData.id} ({itemData.itemName})");
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Find an item pickup by ID
    /// </summary>
    private ItemPickupInteractable FindPickupById(string itemId)
    {
        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
        return allPickups.FirstOrDefault(p => p.InteractableID == itemId);
    }

    /// <summary>
    /// Find original item pickup by ID (for restoration)
    /// </summary>
    private ItemPickupInteractable FindOriginalItemById(string itemId)
    {
        // This would need to be enhanced to track original item positions
        // For now, we'll assume items can't be restored to original positions
        return null;
    }

    /// <summary>
    /// Find ItemData by name
    /// </summary>
    private ItemData FindItemDataByName(string itemDataName)
    {
        ItemData itemData = Resources.Load<ItemData>(SaveManager.Instance.itemDataPath + itemDataName);
        if (itemData != null) return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        return allItemData.FirstOrDefault(data => data.name == itemDataName);
    }

    #endregion

    #region ISaveable Implementation

    public object GetDataToSave()
    {
        var saveData = new SceneItemStateSaveData
        {
            collectedOriginalItems = collectedOriginalItems.ToList(),
            droppedInventoryItems = droppedInventoryItems.Values.ToList(),
            nextDroppedItemId = nextDroppedItemId
        };

        //DebugLog($"GetDataToSave called - saving {collectedOriginalItems.Count} collected items, {droppedInventoryItems.Count} dropped items");

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

    /// <summary>
    /// UPDATED: Now uses context-aware loading
    /// Context affects how we apply the loaded state to the scene
    /// </summary>
    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (data is SceneItemStateSaveData saveData)
        {
            DebugLog($"Loading item state data (Context: {context})");

            collectedOriginalItems = new HashSet<string>(saveData.collectedOriginalItems ?? new List<string>());
            droppedInventoryItems = (saveData.droppedInventoryItems ?? new List<DroppedItemData>())
                .ToDictionary(item => item.id, item => item);
            nextDroppedItemId = saveData.nextDroppedItemId;

            DebugLog($"Loaded state: {collectedOriginalItems.Count} collected items, {droppedInventoryItems.Count} dropped items");

            // Context-aware state application
            switch (context)
            {
                case RestoreContext.SaveFileLoad:
                    DebugLog("Save file load - will apply complete state after delay");
                    // For save file loads, apply the complete saved state
                    break;

                case RestoreContext.DoorwayTransition:
                    DebugLog("Doorway transition - will apply state immediately");
                    // For doorway transitions, apply state normally
                    break;

                case RestoreContext.NewGame:
                    DebugLog("New game - clearing all item states");
                    // For new game, clear everything
                    collectedOriginalItems.Clear();
                    droppedInventoryItems.Clear();
                    nextDroppedItemId = 1;
                    break;
            }
        }
        else
        {
            DebugLog($"LoadSaveDataWithContext called with invalid data type: {data?.GetType()}");
        }
    }

    public void OnBeforeSave()
    {
        // DebugLog($"OnBeforeSave called - current state: {collectedOriginalItems.Count} collected, {droppedInventoryItems.Count} dropped");
    }

    public void OnAfterLoad()
    {
        // DebugLog("Scene item state manager loaded successfully - applying loaded state to scene");

        // Force a complete state refresh after loading save data
        // This ensures that the scene matches exactly what was saved
        StartCoroutine(ApplyItemStatesAfterSaveLoad());
    }

    /// <summary>
    /// Apply item states after loading from save - more thorough cleanup
    /// </summary>
    private System.Collections.IEnumerator ApplyItemStatesAfterSaveLoad()
    {
        // Wait for scene to fully load and other systems to initialize
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.2f); // Slightly longer wait for save loads

        //  DebugLog($"Applying SAVE-LOADED states - {collectedOriginalItems.Count} collected, {droppedInventoryItems.Count} dropped");

        // More thorough cleanup for save loads
        CleanupExistingDroppedItems();
        ApplyCollectedItemStates();
        SpawnDroppedInventoryItems();

        //DebugLog("Save-loaded item states applied successfully");

        // Debug the final state
        if (showDebugLogs)
        {
            DebugSceneItemsVsState();
        }
    }

    #endregion

    #region Public API for Item Systems

    /// <summary>
    /// Drop an item from inventory into the scene
    /// </summary>
    public static bool DropItemFromInventoryIntoScene(ItemData itemData, Vector3? position = null)
    {
        if (Instance == null || itemData == null) return false;

        Vector3 dropPosition = position ?? CalculateDropPosition();
        string droppedId = Instance.AddDroppedInventoryItem(itemData, dropPosition);

        // Immediately spawn the item
        var droppedData = Instance.droppedInventoryItems[droppedId];
        Instance.SpawnDroppedItem(droppedData);

        return true;
    }

    /// <summary>
    /// Handle when a dropped inventory item is picked up
    /// </summary>
    public static void OnDroppedInventoryItemPickedUp(string droppedId)
    {
        Instance?.RemoveDroppedInventoryItem(droppedId);
    }

    /// <summary>
    /// Handle when an original scene item is picked up
    /// </summary>
    public static void OnOriginalSceneItemPickedUp(string itemId)
    {
        Instance?.MarkOriginalItemAsCollected(itemId);
    }

    /// <summary>
    /// Drop an original scene item back into the scene (restore it)
    /// This is for when you pick up an original item then drop it back
    /// </summary>
    public static bool DropOriginalItemBackToScene(string originalItemId, ItemData itemData, Vector3? position = null)
    {
        if (Instance == null) return false;

        // Remove from collected state (making it "not collected" again)
        Instance.RestoreOriginalItem(originalItemId);

        // If we can't restore to original position, treat as dropped inventory item
        Vector3 dropPosition = position ?? CalculateDropPosition();
        string droppedId = Instance.AddDroppedInventoryItem(itemData, dropPosition);

        var droppedData = Instance.droppedInventoryItems[droppedId];
        Instance.SpawnDroppedItem(droppedData);

        return true;
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

    [Button("Debug State")]
    public void DebugCurrentState()
    {
        Debug.Log("=== Scene ITEM STATE DEBUG ===");
        Debug.Log($"Collected original items: {collectedOriginalItems.Count}");
        foreach (var item in collectedOriginalItems)
        {
            Debug.Log($"  - {item}");
        }

        Debug.Log($"Dropped inventory items: {droppedInventoryItems.Count}");
        foreach (var item in droppedInventoryItems.Values)
        {
            Debug.Log($"  - {item.id}: {item.itemDataName} at {item.position}");
        }

        Debug.Log($"Next dropped item ID: {nextDroppedItemId}");

        // Debug what's actually in the scene
        DebugSceneItemsVsState();
    }

    [Button("Debug Scene vs State")]
    public void DebugSceneItemsVsState()
    {
        Debug.Log("=== SCENE vs STATE COMPARISON ===");

        var allPickups = FindObjectsByType<ItemPickupInteractable>(FindObjectsSortMode.None);
        var originalPickups = allPickups.Where(p => p.IsOriginalSceneItem).ToArray();
        var droppedPickups = allPickups.Where(p => p.IsDroppedInventoryItem).ToArray();

        Debug.Log($"Scene has {originalPickups.Length} original items, {droppedPickups.Length} dropped items");

        // Check original items
        foreach (var pickup in originalPickups)
        {
            bool shouldBeCollected = IsOriginalItemCollected(pickup.InteractableID);
            Debug.Log($"Original item {pickup.InteractableID}: InScene={true}, ShouldBeCollected={shouldBeCollected} {(shouldBeCollected ? "❌ SHOULD BE REMOVED" : "✅")}");
        }

        // Check dropped items
        foreach (var pickup in droppedPickups)
        {
            bool shouldExist = droppedInventoryItems.ContainsKey(pickup.InteractableID);
            Debug.Log($"Dropped item {pickup.InteractableID}: InScene={true}, ShouldExist={shouldExist} {(!shouldExist ? "❌ ORPHANED" : "✅")}");
        }

        // Check for missing dropped items
        foreach (var droppedData in droppedInventoryItems.Values)
        {
            var existingPickup = FindPickupById(droppedData.id);
            Debug.Log($"Saved dropped item {droppedData.id}: ShouldExist={true}, InScene={existingPickup != null} {(existingPickup == null ? "❌ MISSING" : "✅")}");
        }
    }

    [Button("Force Refresh State")]
    public void ForceRefreshState()
    {
        Debug.Log("=== FORCING STATE REFRESH ===");
        StartCoroutine(ApplyItemStatesAfterSceneLoad());
    }

    [Button("Clear All States")]
    public void ClearAllStates()
    {
        collectedOriginalItems.Clear();
        droppedInventoryItems.Clear();
        nextDroppedItemId = 1;
        Debug.Log("All item states cleared");

        // Also refresh the scene to match
        ForceRefreshState();
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
/// Save data for the Scene item state system
/// </summary>
[System.Serializable]
public class SceneItemStateSaveData
{
    public List<string> collectedOriginalItems;
    public List<DroppedItemData> droppedInventoryItems;
    public int nextDroppedItemId;
}

