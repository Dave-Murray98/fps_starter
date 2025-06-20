using UnityEngine;

/// <summary>
/// REFACTORED: PlayerSaveComponent now handles ALL player data management
/// Extracts data from PlayerManager and PlayerController during saves
/// Restores data back to managers during loads
/// PlayerManager and PlayerController become pure data holders
/// </summary>
public class PlayerSaveComponent : SaveComponentBase
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
            // Return the player data directly
            DebugLog($"Extracted player save data: Pos={playerSaveData.position}, Health={playerSaveData.currentHealth}");
            return playerSaveData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            // Extract from persistent data structure
            var extractedData = new PlayerSaveData
            {
                currentHealth = persistentData.currentHealth,
                canJump = persistentData.canJump,
                canSprint = persistentData.canSprint,
                canCrouch = persistentData.canCrouch
            };
            DebugLog($"Extracted from persistent data: Health={extractedData.currentHealth}");
            return extractedData;
        }
        else
        {
            DebugLog("Invalid save data type - expected PlayerSaveData or PlayerPersistentData");
            return null;
        }
    }

    /// <summary>
    /// RESTORE data back to managers (managers don't handle their own loading anymore)
    /// </summary>
    public override void LoadSaveData(object data)
    {
        if (!(data is PlayerSaveData playerSaveData))
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, got {data?.GetType()}");
            return;
        }

        DebugLog("=== RESTORING PLAYER DATA TO MANAGERS ===");

        // Ensure we have current references (they might have changed after scene load)
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }

        // Restore position and abilities to PlayerController
        if (playerController != null)
        {
            // Restore position (only for save/load, not doorway transitions)
            if (IsFullSaveLoad(playerSaveData))
            {
                playerController.transform.position = playerSaveData.position;
                playerController.transform.eulerAngles = playerSaveData.rotation;
                DebugLog($"Restored position: {playerSaveData.position}");
            }

            // Restore abilities
            playerController.canJump = playerSaveData.canJump;
            playerController.canSprint = playerSaveData.canSprint;
            playerController.canCrouch = playerSaveData.canCrouch;

            DebugLog($"Restored abilities - Jump: {playerSaveData.canJump}, Sprint: {playerSaveData.canSprint}, Crouch: {playerSaveData.canCrouch}");
        }
        else
        {
            DebugLog("PlayerController not found - position and abilities not restored");
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

        DebugLog("Player data restoration complete");
    }

    /// <summary>
    /// Determine if this is a full save/load (restore position) or just data persistence (don't restore position)
    /// </summary>
    private bool IsFullSaveLoad(PlayerSaveData saveData)
    {
        // If scene name is set, this is likely from a save file
        // If scene name is empty/null, this is likely from PlayerPersistenceManager doorway transition
        return !string.IsNullOrEmpty(saveData.currentScene);
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