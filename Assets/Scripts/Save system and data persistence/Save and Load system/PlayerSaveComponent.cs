using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SIMPLIFIED Player Save Component
/// No more context switching - just save/load
/// </summary>
public class PlayerSaveComponent : SaveComponentBase
{
    [Header("Player References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerData playerData;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        // Fixed ID for player
        saveID = "Player_Main";
        autoGenerateID = false;
        base.Awake();

        // Auto-find references
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (playerManager == null)
            playerManager = GameManager.Instance?.playerManager;
        if (playerData == null)
            playerData = GameManager.Instance?.playerData;

    }

    public override object GetSaveData()
    {
        if (playerController == null) return null;

        var saveData = new PlayerSaveData();

        // Always save position and scene
        saveData.position = playerController.transform.position;
        saveData.rotation = playerController.transform.eulerAngles;
        saveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Save stats
        if (playerManager != null && playerData != null)
        {
            saveData.health = playerManager.currentHealth;
            saveData.maxHealth = playerData.maxHealth;
        }

        // Save abilities
        if (playerController != null)
        {
            saveData.canJump = playerController.canJump;
            saveData.canSprint = playerController.canSprint;
            saveData.canCrouch = playerController.canCrouch;
        }

        DebugLog($"Saved player data: Pos={saveData.position}, Health={saveData.health}");
        return saveData;
    }

    public override void LoadSaveData(object data)
    {
        if (!(data is PlayerSaveData playerSaveData) || playerController == null)
        {
            DebugLog("Invalid player save data or PlayerController not found - cannot load");
            if (data == null)
                DebugLog("Player Data is null");
            else
                DebugLog($"Data type: {data.GetType()}");
            if (playerController == null)
                DebugLog("PlayerController is null");
            return;
        }

        // Always load position (this is only called by SaveManager)
        playerController.transform.position = playerSaveData.position;
        playerController.transform.eulerAngles = playerSaveData.rotation;

        // Load stats
        if (playerManager != null)
        {
            playerManager.currentHealth = playerSaveData.health;
            DebugLog($"Loaded player health: {playerSaveData.health}");
            if (playerData != null)
                playerData.maxHealth = playerSaveData.maxHealth;
        }
        else
        {
            DebugLog("PlayerManager not found - cannot load health");
        }

        // Load abilities
        playerController.canJump = playerSaveData.canJump;
        playerController.canSprint = playerSaveData.canSprint;
        playerController.canCrouch = playerSaveData.canCrouch;

        DebugLog($"Loaded player data: Pos={playerSaveData.position}, Health={playerSaveData.health}");
    }
}