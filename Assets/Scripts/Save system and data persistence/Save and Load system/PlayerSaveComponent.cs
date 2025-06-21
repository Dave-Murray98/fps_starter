using UnityEngine;

/// <summary>
/// ENHANCED: PlayerSaveComponent now implements IPlayerDependentSaveable for true modularity
/// Handles its own data extraction, default creation, and contribution to unified saves
/// No longer requires hardcoded knowledge in PlayerPersistenceManager
/// UPDATED: Simplified to use only context-aware loading
/// </summary>
public class PlayerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
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
    /// CLEANED: Now fully modular - no legacy field references
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
            // Extract from persistent data structure - create a basic PlayerSaveData
            var extractedData = new PlayerSaveData
            {
                currentHealth = persistentData.currentHealth,
                canJump = persistentData.canJump,
                canSprint = persistentData.canSprint,
                canCrouch = persistentData.canCrouch,
                position = Vector3.zero, // Persistent data doesn't contain position
                rotation = Vector3.zero
            };

            // Try to get additional data from dynamic storage
            var fullPlayerData = persistentData.GetComponentData<PlayerSaveData>(SaveID);
            if (fullPlayerData != null)
            {
                // Merge additional data that might be stored
                extractedData.lookSensitivity = fullPlayerData.lookSensitivity;
                extractedData.masterVolume = fullPlayerData.masterVolume;
                extractedData.sfxVolume = fullPlayerData.sfxVolume;
                extractedData.musicVolume = fullPlayerData.musicVolume;
                extractedData.level = fullPlayerData.level;
                extractedData.experience = fullPlayerData.experience;
                extractedData.maxHealth = fullPlayerData.maxHealth;
                extractedData.currentScene = fullPlayerData.currentScene;

                // Merge custom data
                extractedData.MergeCustomDataFrom(fullPlayerData);
            }

            DebugLog($"Extracted from persistent data: Health={extractedData.currentHealth}, Pos={extractedData.position}");
            return extractedData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData or PlayerPersistentData, got {saveContainer?.GetType().Name ?? "null"}");
            return null;
        }
    }

    #region IPlayerDependentSaveable Implementation - NEW MODULAR INTERFACE

    /// <summary>
    /// MODULAR: Extract player data from unified save structure
    /// This component knows how to get its data from PlayerPersistentData
    /// CLEANED: No more legacy field references
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        // Create PlayerSaveData from the unified structure
        var playerSaveData = new PlayerSaveData
        {
            currentHealth = unifiedData.currentHealth,
            canJump = unifiedData.canJump,
            canSprint = unifiedData.canSprint,
            canCrouch = unifiedData.canCrouch,
            // Note: Position is intentionally not extracted from persistent data
            // Position should come from save files, not scene transitions
            position = Vector3.zero,
            rotation = Vector3.zero
        };

        // Try to get additional data from dynamic storage
        var additionalData = unifiedData.GetComponentData<PlayerSaveData>(SaveID);
        if (additionalData != null)
        {
            // Merge any additional data that might be stored
            playerSaveData.lookSensitivity = additionalData.lookSensitivity;
            playerSaveData.masterVolume = additionalData.masterVolume;
            playerSaveData.sfxVolume = additionalData.sfxVolume;
            playerSaveData.musicVolume = additionalData.musicVolume;
            playerSaveData.level = additionalData.level;
            playerSaveData.experience = additionalData.experience;
            playerSaveData.maxHealth = additionalData.maxHealth;
            playerSaveData.currentScene = additionalData.currentScene;

            // Merge any custom data
            playerSaveData.MergeCustomDataFrom(additionalData);
        }

        DebugLog($"Modular extraction complete: Health={playerSaveData.currentHealth}, Abilities set, Custom data: {playerSaveData.CustomDataCount}");
        return playerSaveData;
    }

    /// <summary>
    /// MODULAR: Create default player data for new games
    /// This component knows what its default state should be
    /// CLEANED: No more legacy field references
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default player data for new game");

        var defaultData = new PlayerSaveData();

        // Set health from PlayerData if available
        if (playerData != null)
        {
            defaultData.currentHealth = playerData.maxHealth;
            defaultData.maxHealth = playerData.maxHealth;
            defaultData.lookSensitivity = playerData.lookSensitivity;
        }
        else
        {
            defaultData.currentHealth = 100f;
            defaultData.maxHealth = 100f;
            defaultData.lookSensitivity = 2f;
        }

        // Set default abilities
        defaultData.canJump = true;
        defaultData.canSprint = true;
        defaultData.canCrouch = true;

        // Set default audio settings
        defaultData.masterVolume = 1f;
        defaultData.sfxVolume = 1f;
        defaultData.musicVolume = 1f;

        // Set default character progression
        defaultData.level = 1;
        defaultData.experience = 0f;

        // Default position will be set by spawn point or doorway
        defaultData.position = Vector3.zero;
        defaultData.rotation = Vector3.zero;
        defaultData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Note: No hardcoded component data initialization
        // Other components will handle their own default data creation

        DebugLog($"Default player data created: Health={defaultData.currentHealth}, Abilities enabled");
        return defaultData;
    }

    /// <summary>
    /// MODULAR: Contribute player data to unified save structure
    /// This component knows how to store its data in PlayerPersistentData
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is PlayerSaveData playerData && unifiedData != null)
        {
            DebugLog("Contributing player data to unified save structure");

            // Contribute basic player stats to the unified structure
            unifiedData.currentHealth = playerData.currentHealth;
            unifiedData.canJump = playerData.canJump;
            unifiedData.canSprint = playerData.canSprint;
            unifiedData.canCrouch = playerData.canCrouch;

            // Store complete player data in dynamic storage for full preservation
            unifiedData.SetComponentData(SaveID, playerData);

            DebugLog($"Player data contributed: Health={playerData.currentHealth}, Abilities set, Full data stored in dynamic storage");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected PlayerSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Context-Aware Loading

    /// <summary>
    /// CONTEXT-AWARE: Load data with awareness of restoration context
    /// This is the key improvement - we know WHY we're being restored
    /// UPDATED: This is now the primary (and only) loading method
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
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
                break;
        }

        DebugLog($"Player data restoration complete for context: {context}");
    }

    #endregion

    #region Private Helper Methods

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

    #endregion

    #region Lifecycle and Utility Methods

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

    #endregion
}