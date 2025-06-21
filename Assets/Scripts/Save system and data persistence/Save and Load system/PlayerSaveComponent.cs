using UnityEngine;


/// <summary>
/// Handles saving and loading of core player data including position, health, abilities, and settings.
/// Implements context-aware loading to handle position restoration differently for doorway transitions
/// vs save file loads. Integrates with the modular save system architecture.
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
        saveID = "Player_Main";
        autoGenerateID = false;
        base.Awake();

        if (autoFindReferences)
        {
            FindPlayerReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically locates player-related components in the scene.
    /// Checks current GameObject first, then searches scene, then GameManager.
    /// </summary>
    private void FindPlayerReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

        if (playerManager == null)
            playerManager = GetComponent<PlayerManager>() ?? FindFirstObjectByType<PlayerManager>();

        if (playerData == null && GameManager.Instance != null)
            playerData = GameManager.Instance.playerData;

        DebugLog($"Auto-found references - Controller: {playerController != null}, Manager: {playerManager != null}, Data: {playerData != null}");
    }

    /// <summary>
    /// Validates that all necessary references are available for saving/loading.
    /// </summary>
    private void ValidateReferences()
    {
        if (playerController == null)
            Debug.LogError($"[{name}] PlayerController reference missing! Position/abilities won't be saved.");

        if (playerManager == null)
            Debug.LogError($"[{name}] PlayerManager reference missing! Health won't be saved.");

        if (playerData == null)
            Debug.LogWarning($"[{name}] PlayerData reference missing! Some default values unavailable.");
    }

    /// <summary>
    /// Extracts current player state from controllers and managers.
    /// Collects position, health, abilities, and settings into a unified data structure.
    /// </summary>
    public override object GetDataToSave()
    {
        var saveData = new PlayerSaveData();

        // Extract position and abilities from PlayerController
        if (playerController != null)
        {
            saveData.position = playerController.transform.position;
            saveData.rotation = playerController.transform.eulerAngles;
            saveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            saveData.canJump = playerController.canJump;
            saveData.canSprint = playerController.canSprint;
            saveData.canCrouch = playerController.canCrouch;

            DebugLog($"Extracted position: {saveData.position}, abilities from PlayerController");
        }

        // Extract health data from PlayerManager
        if (playerManager != null)
        {
            saveData.currentHealth = playerManager.currentHealth;
            if (playerData != null)
            {
                saveData.maxHealth = playerData.maxHealth;
            }
            DebugLog($"Extracted health: {saveData.currentHealth}/{saveData.maxHealth}");
        }

        // Extract settings from PlayerData
        if (playerData != null)
        {
            saveData.lookSensitivity = playerData.lookSensitivity;
        }

        return saveData;
    }

    /// <summary>
    /// Extracts player data from various save container formats for persistence system.
    /// Handles PlayerSaveData (direct), PlayerPersistentData (unified), and fallbacks.
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting player save data for persistence");

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            DebugLog($"Extracted player save data: Pos={playerSaveData.position}, Health={playerSaveData.currentHealth}");
            return playerSaveData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
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
                extractedData.lookSensitivity = fullPlayerData.lookSensitivity;
                extractedData.masterVolume = fullPlayerData.masterVolume;
                extractedData.sfxVolume = fullPlayerData.sfxVolume;
                extractedData.musicVolume = fullPlayerData.musicVolume;
                extractedData.level = fullPlayerData.level;
                extractedData.experience = fullPlayerData.experience;
                extractedData.maxHealth = fullPlayerData.maxHealth;
                extractedData.currentScene = fullPlayerData.currentScene;
                extractedData.MergeCustomDataFrom(fullPlayerData);
            }

            DebugLog($"Extracted from persistent data: Health={extractedData.currentHealth}");
            return extractedData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extracts player data from the unified save structure for modular loading.
    /// Creates PlayerSaveData from the basic stats and dynamic component storage.
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var playerSaveData = new PlayerSaveData
        {
            currentHealth = unifiedData.currentHealth,
            canJump = unifiedData.canJump,
            canSprint = unifiedData.canSprint,
            canCrouch = unifiedData.canCrouch,
            position = Vector3.zero, // Position comes from save files, not persistent data
            rotation = Vector3.zero
        };

        // Merge additional data from dynamic storage
        var additionalData = unifiedData.GetComponentData<PlayerSaveData>(SaveID);
        if (additionalData != null)
        {
            playerSaveData.lookSensitivity = additionalData.lookSensitivity;
            playerSaveData.masterVolume = additionalData.masterVolume;
            playerSaveData.sfxVolume = additionalData.sfxVolume;
            playerSaveData.musicVolume = additionalData.musicVolume;
            playerSaveData.level = additionalData.level;
            playerSaveData.experience = additionalData.experience;
            playerSaveData.maxHealth = additionalData.maxHealth;
            playerSaveData.currentScene = additionalData.currentScene;
            playerSaveData.MergeCustomDataFrom(additionalData);
        }

        DebugLog($"Modular extraction complete: Health={playerSaveData.currentHealth}, Custom data: {playerSaveData.CustomDataCount}");
        return playerSaveData;
    }

    /// <summary>
    /// Creates default player data for new games with starting values.
    /// Uses PlayerData ScriptableObject for defaults when available.
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

        // Set default abilities and settings
        defaultData.canJump = true;
        defaultData.canSprint = true;
        defaultData.canCrouch = true;
        defaultData.masterVolume = 1f;
        defaultData.sfxVolume = 1f;
        defaultData.musicVolume = 1f;
        defaultData.level = 1;
        defaultData.experience = 0f;
        defaultData.position = Vector3.zero; // Spawn point will set position
        defaultData.rotation = Vector3.zero;
        defaultData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        DebugLog($"Default player data created: Health={defaultData.currentHealth}");
        return defaultData;
    }

    /// <summary>
    /// Contributes player data to the unified save structure for save file creation.
    /// Stores basic stats in the main structure and complete data in dynamic storage.
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is PlayerSaveData playerData && unifiedData != null)
        {
            DebugLog("Contributing player data to unified save structure");

            // Store basic stats in main structure
            unifiedData.currentHealth = playerData.currentHealth;
            unifiedData.canJump = playerData.canJump;
            unifiedData.canSprint = playerData.canSprint;
            unifiedData.canCrouch = playerData.canCrouch;

            // Store complete data in dynamic storage for full preservation
            unifiedData.SetComponentData(SaveID, playerData);

            DebugLog($"Player data contributed: Health={playerData.currentHealth}, abilities set");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected PlayerSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// Context-aware data restoration. Restores different data based on why loading is happening.
    /// Doorway transitions: Skip position (doorway handles it), restore stats/abilities
    /// Save loads: Restore everything including exact position
    /// New game: Use for initial setup
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

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }

        // Always restore abilities and health
        RestorePlayerStats(playerSaveData);

        // Context determines whether to restore position
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
                DebugLog("New game - position will be set by spawn point");
                break;
        }

        DebugLog($"Player data restoration complete for context: {context}");
    }

    /// <summary>
    /// Restores player stats, abilities, and health. Common to all restoration contexts.
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

        // Restore settings to PlayerData
        if (playerData != null && playerSaveData.lookSensitivity > 0)
        {
            playerData.lookSensitivity = playerSaveData.lookSensitivity;
            DebugLog($"Restored look sensitivity: {playerSaveData.lookSensitivity}");
        }
    }

    /// <summary>
    /// Restores exact player position and rotation. Only used for save file loads.
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
    /// Called before save operations to ensure references are current.
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing player data for save");

        if (autoFindReferences)
        {
            FindPlayerReferences();
        }
    }

    /// <summary>
    /// Called after load operations to refresh UI systems.
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Player data load completed");

        if (GameManager.Instance?.uiManager != null)
        {
            GameManager.Instance.uiManager.RefreshReferences();
        }
    }
}