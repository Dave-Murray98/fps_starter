using UnityEngine;

/// <summary>
/// Manages player data that should persist between scenes (via doorways)
/// This is separate from save files - it's for doorway transitions
/// Now includes inventory support
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Persistent Player Data")]
    [SerializeField] private PlayerPersistentData persistentData;
    [SerializeField] private bool showDebugLogs = true;

    // Flag to track if we have persistent data to restore
    private bool hasPersistentData = false;

    // FIX: Flag to prevent restoration when SaveManager is handling it
    private bool saveManagerIsHandlingRestore = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            persistentData = new PlayerPersistentData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// called on scene load, but TRIGGERED ONLY BY DOORWAYS - Restore player data after transitioning to a new scene via doorway
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (hasPersistentData && !saveManagerIsHandlingRestore)
        {
            Debug.Log("PlayerPersistenceManager.OnSceneLoaded() called, triggering StartCoroutine(RestorePlayerDataCoroutine())");
            StartCoroutine(RestorePlayerDataCoroutine());
        }
    }

    private System.Collections.IEnumerator RestorePlayerDataCoroutine()
    {
        DebugLog("PlayerPersistenceManager.RestorePlayerDataCoroutine() called");

        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        // Double-check that SaveManager isn't handling this
        if (!saveManagerIsHandlingRestore)
        {
            RestorePlayerDataAfterTransition();
        }
        else
        {
            DebugLog("Skipping doorway data restoration - SaveManager is handling it");
        }
    }

    /// <summary>
    /// Only called by RestorePlayerDataCoroutine() after a scene transition VIA DOORWAY
    /// </summary>
    private void RestorePlayerDataAfterTransition()
    {
        if (!hasPersistentData || saveManagerIsHandlingRestore) return;

        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();
        var inventorySaveComponent = FindFirstObjectByType<InventorySaveComponent>();

        if (playerManager != null && playerController != null)
        {
            playerManager.currentHealth = persistentData.currentHealth;
            playerController.canJump = persistentData.canJump;
            playerController.canSprint = persistentData.canSprint;
            playerController.canCrouch = persistentData.canCrouch;

            // Restore inventory data
            if (inventorySaveComponent != null && persistentData.inventoryData != null)
            {
                inventorySaveComponent.LoadInventoryFromSaveData(persistentData.inventoryData);
                DebugLog($"Restored inventory data: {persistentData.inventoryData.ItemCount} items");
            }

            // Trigger UI updates
            if (GameManager.Instance?.playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(persistentData.currentHealth, GameManager.Instance.playerData.maxHealth);
            }

            DebugLog($"Player data restored after doorway transition: Health={persistentData.currentHealth}, Inventory={persistentData.inventoryData?.ItemCount ?? 0} items");
        }
    }

    /// <summary>
    /// Get current persistent data for save system
    /// </summary>
    public PlayerPersistentData GetPersistentDataForSave()
    {
        SavePlayerDataForTransition(); // Update with current values
        return new PlayerPersistentData(persistentData); // Return copy
    }

    /// <summary>
    /// Save current player data before scene transition (called by doorway)
    /// </summary>
    public void SavePlayerDataForTransition()
    {
        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();
        var inventorySaveComponent = FindFirstObjectByType<InventorySaveComponent>();

        if (playerManager != null && playerController != null)
        {
            persistentData.currentHealth = playerManager.currentHealth;
            persistentData.canJump = playerController.canJump;
            persistentData.canSprint = playerController.canSprint;
            persistentData.canCrouch = playerController.canCrouch;

            // Save inventory data
            if (inventorySaveComponent != null)
            {
                persistentData.inventoryData = inventorySaveComponent.GetInventorySaveData();
                DebugLog($"Saved inventory data: {persistentData.inventoryData.ItemCount} items");
            }
            else
            {
                DebugLog("No InventoryManager found - inventory not saved");
            }

            hasPersistentData = true;
            DebugLog($"Player data saved for doorway transition: Health={persistentData.currentHealth}, Inventory={persistentData.inventoryData.ItemCount} items");
        }
    }

    /// <summary>
    /// Load persistent data from save system - Clear doorway data when loading from save to prevent conflicts
    /// </summary>
    public void LoadPersistentDataFromSave(PlayerPersistentData saveData)
    {
        if (saveData != null)
        {
            // Clear existing data before loading new
            persistentData = new PlayerPersistentData(saveData);

            //Clear doorway transition data since we're loading from save
            hasPersistentData = false;
            saveManagerIsHandlingRestore = true; //disables doorway transition data restoration, so it won't conflict with SaveManager's loading process

            DebugLog($"Player persistent data loaded from save - doorway data cleared. Inventory: {persistentData.inventoryData?.ItemCount ?? 0} items");
        }
    }

    /// <summary>
    /// Clear persistent data (useful for new game)
    /// </summary>
    public void ClearPersistentData()
    {
        persistentData = new PlayerPersistentData();
        hasPersistentData = false;
        saveManagerIsHandlingRestore = false;
        DebugLog("Player persistent data cleared");
    }

    /// <summary>
    /// Call this when SaveManager finishes loading to re-enable doorway transitions
    /// </summary>
    public void OnSaveLoadComplete()
    {
        saveManagerIsHandlingRestore = false;
        DebugLog("Save load complete - doorway transitions re-enabled");
    }

    /// <summary>
    /// Check if we have persistent data waiting to be restored
    /// </summary>
    public bool HasPersistentData => hasPersistentData && !saveManagerIsHandlingRestore;

    /// <summary>
    /// Get current player data snapshot (useful for debugging)
    /// </summary>
    public PlayerPersistentData GetCurrentSnapshot()
    {
        SavePlayerDataForTransition(); // Update with current values
        return persistentData;
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPersistence] {message}");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}