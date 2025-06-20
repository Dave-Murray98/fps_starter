using UnityEngine;

/// <summary>
/// REFACTORED: PlayerSaveComponent now implements context-aware restoration
/// Can distinguish between doorway transitions (no position restore) and save loads (full restore)
/// Much cleaner and more predictable than the previous IsFullSaveLoad method
/// </summary>
public class PlayerSaveComponent : SaveComponentBase, IContextAwareSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerData playerData;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        // Fixed ID for player
        saveID = "Player_Main";
        autoGenerateID = false;
        base.Awake();

        // Auto-find references if enabled
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }
    }

    private void Start()
    {
        // Ensure we have all references
        ValidateReferences();
    }

    /// <summary>
    /// Automatically find player-related components
    /// </summary>
    private void FindPlayerReferences()
    {
        // Try to find on same GameObject first
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerManager == null)
            playerManager = GetComponent<PlayerManager>();

        // If not found on same GameObject, search scene
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager == null)
            playerManager = FindFirstObjectByType<PlayerManager>();

        // Get player data from GameManager
        if (playerData == null && GameManager.Instance != null)
            playerData = GameManager.Instance.playerData;

        DebugLog($"Auto-found references - Controller: {playerController != null}, Manager: {playerManager != null}, Data: {playerData != null}");
    }

    /// <summary>
    /// Validate that we have all necessary references
    /// </summary>
    private void ValidateReferences()
    {
        if (playerController == null)
            Debug.LogError($"[{name}] PlayerController reference is missing! Player position/abilities won't be saved.");

        if (playerManager == null)
            Debug.LogError($"[{name}] PlayerManager reference is missing! Player health won't be saved.");

        if (playerData == null)
            Debug.LogWarning($"[{name}] PlayerData reference is missing! Some default values may not be available.");
    }

    /// <summary>
    /// EXTRACT player data from managers (they don't handle their own saving anymore)
    /// </summary>
    public override object GetDataToSave()
    {
        var saveData = new PlayerSaveData();

        // Extract position and scene data from PlayerController
        if (playerController != null)
        {
            saveData.position = playerController.transform.position;
            saveData.rotation = playerController.transform.eulerAngles;
            saveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Extract abilities from PlayerController
            saveData.canJump = playerController.canJump;
            saveData.canSprint = playerController.canSprint;
            saveData.canCrouch = playerController.canCrouch;

            DebugLog($"Extracted position: {saveData.position}, abilities from PlayerController");
        }
        else
        {
            DebugLog("PlayerController not found - position and abilities not saved");
        }

        // Extract health data from PlayerManager
        if (playerManager != null)
        {
            saveData.currentHealth = playerManager.currentHealth;

            // Get max health from PlayerData if available
            if (playerData != null)
            {
                saveData.maxHealth = playerData.maxHealth;
            }

            DebugLog($"Extracted health: {saveData.currentHealth}/{saveData.maxHealth} from PlayerManager");
        }
        else
        {
            DebugLog("PlayerManager not found - health not saved");
        }

        // Extract other player data if available
        if (playerData != null)
        {
            saveData.lookSensitivity = playerData.lookSensitivity;
            // Add other PlayerData fields as needed
        }

        return saveData;
    }

    /// <summary>
    /// For PlayerPersistenceManager - extract only the data we need
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("PlayerSaveComponent: Extracting player save data for persistence");

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Return the player data directly - this preserves position information
            DebugLog($"Extracted player save data: Pos={playerSaveData.position}, Health={playerSaveData.currentHealth}");
            return playerSaveData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            // Extract from persistent data structure (this typically doesn't have position)
            var extractedData = new PlayerSaveData
            {
                currentHealth = persistentData.currentHealth,
                canJump = persistentData.canJump,
                canSprint = persistentData.canSprint,
                canCrouch = persistentData.canCrouch,
                position = Vector3.zero, // Persistent data doesn't contain position
                rotation = Vector3.zero,
                inventoryData = persistentData.inventoryData,
                equipmentData = persistentData.equipmentData
            };
            DebugLog($"Extracted from persistent data: Health={extractedData.currentHealth}, Pos={extractedData.position}");
            return extractedData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData or PlayerPersistentData, got {saveContainer?.GetType().Name ?? "null"}");
            return null;
        }
    }

    /// <summary>
    /// CONTEXT-AWARE: Load data with awareness of restoration context
    /// This is the key improvement - we know WHY we're being restored
    /// </summary>
    public void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is PlayerSaveData playerSaveData))
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== RESTORING PLAYER DATA (Context: {context}) ===");
        DebugLog($"Received data - Position: {playerSaveData.position}, Health: {playerSaveData.currentHealth}");

        // Ensure we have current references (they might have changed after scene load)
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }

        // Restore abilities and health regardless of context
        RestorePlayerStats(playerSaveData);

        // CONTEXT-AWARE: Only restore position for save file loads
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring position");
                RestorePlayerPosition(playerSaveData);
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - NOT restoring position (doorway will set it)");
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - setting default position");
                SetDefaultPosition();
                break;
        }

        DebugLog($"Player data restoration complete for context: {context}");
    }

    /// <summary>
    /// FALLBACK: Standard LoadSaveData method for non-context-aware systems
    /// Defaults to full restoration (including position)
    /// </summary>
    public override void LoadSaveData(object data)
    {
        DebugLog("Using fallback LoadSaveData - defaulting to full restoration");
        LoadSaveDataWithContext(data, RestoreContext.SaveFileLoad);
    }

    /// <summary>
    /// Restore player stats, abilities, and health (common to all contexts)
    /// </summary>
    private void RestorePlayerStats(PlayerSaveData playerSaveData)
    {
        // Restore abilities to PlayerController
        if (playerController != null)
        {
            playerController.canJump = playerSaveData.canJump;
            playerController.canSprint = playerSaveData.canSprint;
            playerController.canCrouch = playerSaveData.canCrouch;

            DebugLog($"Restored abilities - Jump: {playerSaveData.canJump}, Sprint: {playerSaveData.canSprint}, Crouch: {playerSaveData.canCrouch}");
        }
        else
        {
            DebugLog("PlayerController not found - abilities not restored");
        }

        // Restore health to PlayerManager
        if (playerManager != null)
        {
            playerManager.currentHealth = playerSaveData.currentHealth;
            DebugLog($"Restored health: {playerSaveData.currentHealth}");

            // Trigger health UI update
            if (playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(playerManager.currentHealth, playerData.maxHealth);
            }
        }
        else
        {
            DebugLog("PlayerManager not found - health not restored");
        }

        // Restore other player data if available
        if (playerData != null && playerSaveData.lookSensitivity > 0)
        {
            playerData.lookSensitivity = playerSaveData.lookSensitivity;
            DebugLog($"Restored look sensitivity: {playerSaveData.lookSensitivity}");
        }
    }

    /// <summary>
    /// Restore player position (only for save file loads)
    /// </summary>
    private void RestorePlayerPosition(PlayerSaveData playerSaveData)
    {
        if (playerController != null)
        {
            playerController.transform.position = playerSaveData.position;
            playerController.transform.eulerAngles = playerSaveData.rotation;
            DebugLog($"Restored position: {playerSaveData.position}, rotation: {playerSaveData.rotation}");
        }
        else
        {
            DebugLog("PlayerController not found - position not restored");
        }
    }

    /// <summary>
    /// Set default position for new game
    /// </summary>
    private void SetDefaultPosition()
    {
        if (playerController != null)
        {
            // Find a spawn point or use world origin
            var spawnPoint = FindFirstObjectByType<PlayerSpawnPoint>();
            if (spawnPoint != null)
            {
                playerController.transform.position = spawnPoint.transform.position;
                playerController.transform.rotation = spawnPoint.transform.rotation;
                DebugLog($"Set default position to spawn point: {spawnPoint.transform.position}");
            }
            else
            {
                playerController.transform.position = Vector3.zero;
                playerController.transform.rotation = Quaternion.identity;
                DebugLog("Set default position to world origin");
            }
        }
    }

    /// <summary>
    /// Called before save operations
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing player data for save");

        // Refresh references in case they changed
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Player data load completed");

        // Update UI systems after loading
        if (GameManager.Instance?.uiManager != null)
        {
            GameManager.Instance.uiManager.RefreshReferences();
        }
    }

    /// <summary>
    /// Public method to manually set component references (useful for testing)
    /// </summary>
    public void SetReferences(PlayerController controller, PlayerManager manager, PlayerData data)
    {
        playerController = controller;
        playerManager = manager;
        playerData = data;
        autoFindReferences = false; // Disable auto-find when manually set

        DebugLog("References manually set");
    }

    /// <summary>
    /// Get current player health (useful for other systems)
    /// </summary>
    public float GetCurrentHealth()
    {
        return playerManager?.currentHealth ?? 0f;
    }

    /// <summary>
    /// Get current player position (useful for other systems)
    /// </summary>
    public Vector3 GetCurrentPosition()
    {
        return playerController?.transform.position ?? Vector3.zero;
    }

    /// <summary>
    /// Check if all required references are valid
    /// </summary>
    public bool HasValidReferences()
    {
        return playerController != null && playerManager != null;
    }

    /// <summary>
    /// Force refresh of component references
    /// </summary>
    public void RefreshReferences()
    {
        if (autoFindReferences)
        {
            FindPlayerReferences();
            ValidateReferences();
        }
    }
}

/// <summary>
/// Optional component to mark player spawn points for new game initialization
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Spawn Point Settings")]
    public bool isDefaultSpawn = true;
    public string spawnPointID = "default";

    private void OnDrawGizmos()
    {
        Gizmos.color = isDefaultSpawn ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}