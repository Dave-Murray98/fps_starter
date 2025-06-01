using UnityEngine;

/// <summary>
/// Handles saving/loading of player data
/// Integrates with PlayerController, PlayerManager, and PlayerData systems
/// </summary>
public class PlayerSaveComponent : SaveComponentBase
{
    [Header("Player References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerData playerData;

    [Header("Player Save Settings")]
    [SerializeField] private bool saveTransform = true;
    [SerializeField] private bool saveStats = true;
    [SerializeField] private bool saveSettings = true;
    [SerializeField] private bool saveAbilities = true;

    // Cache for performance
    private Transform playerTransform;
    private AudioManager audioManager;

    protected override void Awake()
    {
        base.Awake();

        // Auto-find references if not set
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerManager == null)
            playerManager = GameManager.Instance?.playerManager;

        if (playerData == null)
            playerData = GameManager.Instance?.playerData;

        if (audioManager == null)
            audioManager = GameManager.Instance?.audioManager;

        if (playerController != null)
        {
            playerTransform = playerController.transform;
        }

        // Override auto-generated ID for player (should always be consistent)
        if (autoGenerateID)
        {
            saveID = "Player_Main";
            autoGenerateID = false; // Don't change this
        }
    }

    public override object GetSaveData()
    {
        if (playerController == null)
        {
            Debug.LogError("PlayerController reference is null!");
            return null;
        }

        var saveData = new PlayerSaveData();

        // Save transform data
        if (saveTransform && playerTransform != null)
        {
            saveData.position = playerTransform.position;
            saveData.rotation = playerTransform.eulerAngles;
            saveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        // Save stats from PlayerManager
        if (saveStats && playerManager != null && playerData != null)
        {
            saveData.health = playerManager.currentHealth;
            saveData.maxHealth = playerData.maxHealth;
            saveData.level = 1; // You can add level system later
            saveData.experience = 0f; // You can add experience system later
        }

        // Save settings from PlayerData and AudioManager
        if (saveSettings)
        {
            if (playerData != null)
            {
                saveData.lookSensitivity = playerData.lookSensitivity;
            }

            if (audioManager != null)
            {
                saveData.masterVolume = audioManager.GetVolume("Master");
                saveData.sfxVolume = audioManager.GetVolume("SFX");
                saveData.musicVolume = audioManager.GetVolume("Music");
            }
            else
            {
                // Fallback values if AudioManager not available
                saveData.masterVolume = 1f;
                saveData.sfxVolume = 1f;
                saveData.musicVolume = 1f;
            }
        }

        // Save abilities from PlayerController
        if (saveAbilities && playerController != null)
        {
            saveData.canJump = playerController.canJump;
            saveData.canSprint = playerController.canSprint;
            saveData.canCrouch = playerController.canCrouch;
        }

        DebugLog($"Saved player data: Pos={saveData.position}, Health={saveData.health}/{saveData.maxHealth}");
        return saveData;
    }

    public object GetSaveDataWithoutPosition()
    {
        if (playerController == null)
        {
            Debug.LogError("PlayerController reference is null!");
            return null;
        }

        var saveData = new PlayerSaveData();

        // Save stats from PlayerManager
        if (saveStats && playerManager != null && playerData != null)
        {
            saveData.health = playerManager.currentHealth;
            saveData.maxHealth = playerData.maxHealth;
            saveData.level = 1; // You can add level system later
            saveData.experience = 0f; // You can add experience system later
        }

        // Save settings from PlayerData and AudioManager
        if (saveSettings)
        {
            if (playerData != null)
            {
                saveData.lookSensitivity = playerData.lookSensitivity;
            }

            if (audioManager != null)
            {
                saveData.masterVolume = audioManager.GetVolume("Master");
                saveData.sfxVolume = audioManager.GetVolume("SFX");
                saveData.musicVolume = audioManager.GetVolume("Music");
            }
            else
            {
                // Fallback values if AudioManager not available
                saveData.masterVolume = 1f;
                saveData.sfxVolume = 1f;
                saveData.musicVolume = 1f;
            }
        }

        // Save abilities from PlayerController
        if (saveAbilities && playerController != null)
        {
            saveData.canJump = playerController.canJump;
            saveData.canSprint = playerController.canSprint;
            saveData.canCrouch = playerController.canCrouch;
        }

        DebugLog($"Saved player data: Pos={saveData.position}, Health={saveData.health}/{saveData.maxHealth}");
        return saveData;
    }

    public override void LoadSaveData(object data)
    {
        if (!(data is PlayerSaveData playerSaveData))
        {
            Debug.LogError("Invalid save data type for PlayerSaveComponent");
            return;
        }

        if (playerController == null)
        {
            Debug.LogError("PlayerController reference is null!");
            return;
        }

        // Load transform data
        if (saveTransform && playerTransform != null)
        {
            playerTransform.position = playerSaveData.position;
            playerTransform.eulerAngles = playerSaveData.rotation;
            Debug.Log($"Player position loaded: {playerSaveData.position} in the PLAYERSAVECOMPONENT");
        }

        // Load stats into PlayerManager
        if (saveStats && playerManager != null)
        {
            // Set current health directly (PlayerManager will handle clamping)
            playerManager.currentHealth = Mathf.Clamp(playerSaveData.health, 0f, playerSaveData.maxHealth);

            // Update max health if it changed (you might want to modify PlayerData at runtime)
            if (playerData != null && playerData.maxHealth != playerSaveData.maxHealth)
            {
                playerData.maxHealth = playerSaveData.maxHealth;
            }

            // Trigger health changed event to update UI
            GameEvents.TriggerPlayerHealthChanged(playerManager.currentHealth, playerData?.maxHealth ?? 100f);

            // TODO: Load level and experience when you add those systems
        }

        // Load settings into PlayerData and AudioManager
        if (saveSettings)
        {
            if (playerData != null)
            {
                playerData.lookSensitivity = playerSaveData.lookSensitivity;
            }

            if (audioManager != null)
            {
                audioManager.SetVolume("Master", playerSaveData.masterVolume);
                audioManager.SetVolume("SFX", playerSaveData.sfxVolume);
                audioManager.SetVolume("Music", playerSaveData.musicVolume);
            }
        }

        // Load abilities into PlayerController
        if (saveAbilities && playerController != null)
        {
            playerController.canJump = playerSaveData.canJump;
            playerController.canSprint = playerSaveData.canSprint;
            playerController.canCrouch = playerSaveData.canCrouch;
        }

        DebugLog($"Loaded player data: Pos={playerSaveData.position}, Health={playerSaveData.health}/{playerSaveData.maxHealth}");
    }

    public void LoadSaveDataWithoutPosition(object data)
    {
        DebugLog("Loading player save data without position...");


        if (!(data is PlayerSaveData playerSaveData))
        {
            Debug.LogError("Invalid save data type for PlayerSaveComponent");
            return;
        }

        if (playerController == null)
        {
            Debug.LogError("PlayerController reference is null!");
            return;
        }

        // Load stats into PlayerManager
        if (saveStats && playerManager != null)
        {
            // Set current health directly (PlayerManager will handle clamping)
            playerManager.currentHealth = Mathf.Clamp(playerSaveData.health, 0f, playerSaveData.maxHealth);

            // Update max health if it changed (you might want to modify PlayerData at runtime)
            if (playerData != null && playerData.maxHealth != playerSaveData.maxHealth)
            {
                playerData.maxHealth = playerSaveData.maxHealth;
            }

            // Trigger health changed event to update UI
            GameEvents.TriggerPlayerHealthChanged(playerManager.currentHealth, playerData?.maxHealth ?? 100f);

            // TODO: Load level and experience when you add those systems
        }

        // Load settings into PlayerData and AudioManager
        if (saveSettings)
        {
            if (playerData != null)
            {
                playerData.lookSensitivity = playerSaveData.lookSensitivity;
            }

            if (audioManager != null)
            {
                audioManager.SetVolume("Master", playerSaveData.masterVolume);
                audioManager.SetVolume("SFX", playerSaveData.sfxVolume);
                audioManager.SetVolume("Music", playerSaveData.musicVolume);
            }
        }

        // Load abilities into PlayerController
        if (saveAbilities && playerController != null)
        {
            playerController.canJump = playerSaveData.canJump;
            playerController.canSprint = playerSaveData.canSprint;
            playerController.canCrouch = playerSaveData.canCrouch;
        }

        DebugLog($"Loaded player data: Pos={playerSaveData.position}, Health={playerSaveData.health}/{playerSaveData.maxHealth}");
    }

    public override void OnAfterLoad()
    {
        base.OnAfterLoad();

        // Any additional setup after loading
        if (playerController != null)
        {
            // Ensure player is alive if they have health
            if (playerManager != null && playerManager.currentHealth > 0 && playerManager.IsDead)
            {
                playerManager.Respawn();
            }

            // You can add more post-load setup here:
            // - Update UI elements
            // - Recalculate derived stats
            // - Notify other systems of loaded state
        }
    }

    // Validation in editor
    protected override void OnValidate()
    {
        // Auto-find references in editor
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
    }
}