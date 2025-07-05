using UnityEngine;

/// <summary>
/// ENHANCED: PlayerSaveComponent with comprehensive movement state persistence and validation.
/// Now handles movement mode restoration, environmental consistency checks, and clean state transitions.
/// Implements context-aware loading to handle position restoration differently for doorway transitions
/// vs save file loads. Integrates with the modular save system architecture.
/// </summary>
public class PlayerSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private PlayerWaterDetector waterDetector;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("ENHANCED: Validation Settings")]
    [SerializeField] private bool enableMovementValidation = true;
    [SerializeField] private float validationDelay = 0.3f;

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
    /// ENHANCED: Automatically locates player-related components including water detector
    /// </summary>
    private void FindPlayerReferences()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

        if (playerManager == null)
            playerManager = GetComponent<PlayerManager>() ?? FindFirstObjectByType<PlayerManager>();

        if (waterDetector == null)
            waterDetector = GetComponent<PlayerWaterDetector>() ?? FindFirstObjectByType<PlayerWaterDetector>();

        if (playerData == null && GameManager.Instance != null)
            playerData = GameManager.Instance.playerData;

        DebugLog($"Auto-found references - Controller: {playerController != null}, Manager: {playerManager != null}, Data: {playerData != null}, WaterDetector: {waterDetector != null}");
    }

    /// <summary>
    /// ENHANCED: Validates that all necessary references are available including water detector
    /// </summary>
    private void ValidateReferences()
    {
        if (playerController == null)
            Debug.LogError($"[{name}] PlayerController reference missing! Position/abilities won't be saved.");

        if (playerManager == null)
            Debug.LogError($"[{name}] PlayerManager reference missing! Health won't be saved.");

        if (waterDetector == null)
            Debug.LogError($"[{name}] PlayerWaterDetector reference missing! Movement validation won't work properly.");

        if (playerData == null)
            Debug.LogWarning($"[{name}] PlayerData reference missing! Some default values unavailable.");
    }

    /// <summary>
    /// ENHANCED: Extracts current player state including movement mode and environmental context
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

            // ENHANCED: Save movement state and environmental context
            saveData.savedMovementMode = playerController.CurrentMovementMode;
            saveData.savedMovementState = playerController.currentState;
            saveData.wasInWater = waterDetector?.IsInWater ?? false;

            DebugLog($"Extracted position: {saveData.position}, movement mode: {saveData.savedMovementMode}, in water: {saveData.wasInWater}");
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

        DebugLog($"Save data created: {saveData.GetMovementDebugInfo()}");
        return saveData;
    }

    /// <summary>
    /// ENHANCED: Extracts player data with movement state validation
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("Extracting player save data for persistence");

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // ENHANCED: Validate movement state consistency
            if (!playerSaveData.IsMovementStateConsistent())
            {
                Debug.LogWarning($"Movement state inconsistency detected: {playerSaveData.GetMovementDebugInfo()}");
            }

            DebugLog($"Extracted player save data: {playerSaveData.GetMovementDebugInfo()}");
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

                // ENHANCED: Extract movement state
                extractedData.savedMovementMode = fullPlayerData.savedMovementMode;
                extractedData.savedMovementState = fullPlayerData.savedMovementState;
                extractedData.wasInWater = fullPlayerData.wasInWater;

                extractedData.MergeCustomDataFrom(fullPlayerData);
            }

            DebugLog($"Extracted from persistent data: {extractedData.GetMovementDebugInfo()}");
            return extractedData;
        }

        DebugLog($"Invalid save data type - got {saveContainer?.GetType().Name ?? "null"}");
        return null;
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// ENHANCED: Extracts player data from unified save with movement state
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

            // ENHANCED: Extract movement state
            playerSaveData.savedMovementMode = additionalData.savedMovementMode;
            playerSaveData.savedMovementState = additionalData.savedMovementState;
            playerSaveData.wasInWater = additionalData.wasInWater;

            playerSaveData.MergeCustomDataFrom(additionalData);
        }

        DebugLog($"Modular extraction complete: {playerSaveData.GetMovementDebugInfo()}");
        return playerSaveData;
    }

    /// <summary>
    /// ENHANCED: Creates default player data with proper movement state initialization
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

        // ENHANCED: Set default movement state
        defaultData.savedMovementMode = MovementMode.Ground;
        defaultData.savedMovementState = MovementState.Idle;
        defaultData.wasInWater = false;

        DebugLog($"Default player data created: {defaultData.GetMovementDebugInfo()}");
        return defaultData;
    }

    /// <summary>
    /// ENHANCED: Contributes player data including movement state to unified save
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

            DebugLog($"Player data contributed: {playerData.GetMovementDebugInfo()}");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected PlayerSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    /// <summary>
    /// ENHANCED: Context-aware data restoration with movement validation and clean state management
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is PlayerSaveData playerSaveData))
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, got {data?.GetType().Name ?? "null"}");
            return;
        }

        DebugLog($"=== ENHANCED PLAYER DATA RESTORATION (Context: {context}) ===");
        DebugLog($"Received data: {playerSaveData.GetMovementDebugInfo()}");

        // Refresh references in case they changed after scene load
        if (autoFindReferences)
        {
            FindPlayerReferences();
        }

        // STEP 1: Always restore abilities and health first
        RestorePlayerStats(playerSaveData);

        // STEP 2: Handle position and movement based on context
        switch (context)
        {
            case RestoreContext.SaveFileLoad:
                DebugLog("Save file load - restoring position and movement state");
                RestorePlayerPosition(playerSaveData);
                RestoreMovementState(playerSaveData, context);
                break;

            case RestoreContext.DoorwayTransition:
                DebugLog("Doorway transition - NOT restoring position, but setting movement context");
                // Don't restore position (doorway will set it)
                // Set movement mode but validate after positioning
                SetInitialMovementMode(playerSaveData);
                break;

            case RestoreContext.NewGame:
                DebugLog("New game - using default movement state");
                SetDefaultMovementState();
                break;
        }

        // STEP 3: ENHANCED - Schedule validation after restoration
        if (enableMovementValidation && context != RestoreContext.NewGame)
        {
            StartCoroutine(ScheduleMovementValidation(context));
        }

        DebugLog($"Player data restoration complete for context: {context}");
    }

    /// <summary>
    /// ENHANCED: Sets initial movement mode for doorway transitions
    /// </summary>
    private void SetInitialMovementMode(PlayerSaveData playerSaveData)
    {
        if (playerController != null)
        {
            // Set the movement mode but don't validate yet (positioning hasn't happened)
            playerController.SetInitialMovementMode(playerSaveData.savedMovementMode);
            DebugLog($"Set initial movement mode: {playerSaveData.savedMovementMode}");
        }
    }

    /// <summary>
    /// ENHANCED: Sets default movement state for new games
    /// </summary>
    private void SetDefaultMovementState()
    {
        if (playerController != null)
        {
            playerController.SetInitialMovementMode(MovementMode.Ground);
            DebugLog("Set default movement state for new game");
        }
    }

    /// <summary>
    /// ENHANCED: Restores movement state with validation for save file loads
    /// </summary>
    private void RestoreMovementState(PlayerSaveData playerSaveData, RestoreContext context)
    {
        if (playerController == null) return;

        DebugLog($"Restoring movement state: {playerSaveData.savedMovementMode}");

        // Set the movement mode from save data
        playerController.SetInitialMovementMode(playerSaveData.savedMovementMode);

        // Log the restoration for debugging
        DebugLog($"Movement state restored - Mode: {playerSaveData.savedMovementMode}, WasInWater: {playerSaveData.wasInWater}");
    }

    /// <summary>
    /// ENHANCED: Schedules movement validation after position/state restoration
    /// </summary>
    private System.Collections.IEnumerator ScheduleMovementValidation(RestoreContext context)
    {
        // Wait for positioning and physics to settle
        yield return new WaitForSecondsRealtime(validationDelay);

        DebugLog($"Performing scheduled movement validation for {context}");

        if (playerController != null)
        {
            // Force validation to ensure movement mode matches environment
            playerController.ForceMovementModeValidation();
        }

        DebugLog("Scheduled movement validation complete");
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
    /// ENHANCED: Restores exact player position with clean state management
    /// </summary>
    private void RestorePlayerPosition(PlayerSaveData playerSaveData)
    {
        if (playerController != null)
        {
            // ENHANCED: Clean velocity before position change
            var rb = playerController.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                DebugLog("Cleared velocity before position restore");
            }

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