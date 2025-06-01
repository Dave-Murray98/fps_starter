using System;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [Header("Current Stats")]
    public float currentHealth;

    private PlayerData playerData;
    private bool isDead = false;

    //Public Getters
    public bool IsDead => isDead;
    public float HealthPercentage => playerData != null ? currentHealth / playerData.maxHealth : 0f;

    public void Initialize()
    {
        Debug.Log("PlayerManager Initialized");

        playerData = GameManager.Instance.playerData;

        if (playerData != null)
        {
            //Load health from save system first
            LoadHealthFromSaveSystem();
            GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);
        }

        GameEvents.OnPlayerDeath += HandlePlayerDeath;
    }

    //Load health from save system
    private void LoadHealthFromSaveSystem()
    {
        // Try to get health from SaveManager first
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentGameData != null)
        {
            var saveData = SaveManager.Instance.CurrentGameData;
            if (saveData.playerData != null && saveData.playerData.health > 0)
            {
                currentHealth = saveData.playerData.health;
                Debug.Log($"PlayerManager loaded health from save: {currentHealth}");
                return;
            }
        }

        // Try to get health from ScenePersistenceManager
        if (ScenePersistenceManager.Instance != null)
        {
            var persistentData = ScenePersistenceManager.Instance.GetPersistentData();
            if (persistentData?.playerData != null && persistentData.playerData.health > 0)
            {
                currentHealth = persistentData.playerData.health;
                Debug.Log($"PlayerManager loaded health from persistence: {currentHealth}");
                return;
            }
        }

        // Fallback to max health if no save data
        currentHealth = playerData.maxHealth;
        Debug.Log("PlayerManager using max health (no save data found)");
    }

    //Save health to save system whenever it changes
    private void SaveHealthToSaveSystem()
    {
        // Update SaveManager data
        if (SaveManager.Instance != null && SaveManager.Instance.CurrentGameData != null)
        {
            if (SaveManager.Instance.CurrentGameData.playerData == null)
            {
                SaveManager.Instance.CurrentGameData.playerData = new PlayerSaveData();
            }
            SaveManager.Instance.CurrentGameData.playerData.health = currentHealth;
            SaveManager.Instance.CurrentGameData.playerData.maxHealth = playerData.maxHealth;
        }

        // Update ScenePersistenceManager data
        if (ScenePersistenceManager.Instance != null)
        {
            var persistentData = ScenePersistenceManager.Instance.GetPersistentData();
            if (persistentData != null)
            {
                if (persistentData.playerData == null)
                {
                    persistentData.playerData = new PlayerSaveData();
                }
                persistentData.playerData.health = currentHealth;
                persistentData.playerData.maxHealth = playerData.maxHealth;
            }
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnPlayerDeath -= HandlePlayerDeath;
    }

    private void Update()
    {
        if (isDead) return;

        //health regen
        if (playerData != null && playerData.healthRegenRate > 0)
        {
            ModifyHealth(playerData.healthRegenRate * Time.deltaTime);
        }
    }

    public void ModifyHealth(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, playerData.maxHealth);
        GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);

        //Save health whenever it changes
        SaveHealthToSaveSystem();

        if (currentHealth <= 0 && !isDead)
        {
            isDead = true;
            Debug.Log("Player has died.");
            GameEvents.TriggerPlayerDeath();
        }
    }

    public void Respawn()
    {
        isDead = false;
        currentHealth = playerData.maxHealth;

        //Save the respawn health
        SaveHealthToSaveSystem();

        GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);
        Debug.Log("Player has respawned.");
    }

    private void HandlePlayerDeath()
    {
        if (isDead) return;
        Debug.Log("Handling Player Death...");
    }
}