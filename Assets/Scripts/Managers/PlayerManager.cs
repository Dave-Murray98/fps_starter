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
            currentHealth = playerData.maxHealth;
            GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);
        }

        GameEvents.OnPlayerDeath += HandlePlayerDeath;
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
        GameEvents.TriggerPlayerHealthChanged(currentHealth, playerData.maxHealth);
        Debug.Log("Player has respawned.");
    }

    private void HandlePlayerDeath()
    {
        if (isDead) return;
        Debug.Log("Handling Player Death...");
    }

}
