using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour, IManager
{
    [Header("UI References")]
    public GameObject pauseMenu;
    public Slider healthBar;
    public TextMeshProUGUI healthText;

    public void Initialize()
    {
        //   Debug.Log("UIManager Initialized");
        RefreshReferences();
    }

    public void RefreshReferences()
    {
        //        Debug.Log("UIManager: Refreshing references");

        // Re-subscribe to events (unsubscribe first to prevent duplicates)
        GameEvents.OnPlayerHealthChanged -= UpdateHealthBar;
        GameEvents.OnGamePaused -= ShowPauseMenu;
        GameEvents.OnGameResumed -= HidePauseMenu;

        GameEvents.OnPlayerHealthChanged += UpdateHealthBar;
        GameEvents.OnGamePaused += ShowPauseMenu;
        GameEvents.OnGameResumed += HidePauseMenu;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }

        // Update UI with current values
        UpdateUIAfterSceneLoad();
    }

    public void Cleanup()
    {
        //Debug.Log("UIManager: Cleaning up");

        GameEvents.OnPlayerHealthChanged -= UpdateHealthBar;
        GameEvents.OnGamePaused -= ShowPauseMenu;
        GameEvents.OnGameResumed -= HidePauseMenu;
    }

    private void ShowPauseMenu()
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
        }
    }

    private void HidePauseMenu()
    {
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth / maxHealth;
        }
        if (healthText != null)
        {
            healthText.text = $"{currentHealth:F0}/{maxHealth:F0}";
        }
    }

    public void OnResumeButtonClicked()
    {
        GameManager.Instance.ResumeGame();
    }

    public void OnQuitButtonClicked()
    {
        GameManager.Instance.QuitGame();
    }

    public void OnSaveGameButtonClicked()
    {
        SaveManager.Instance.SaveGame();
    }

    // FIX: Load game with proper pause handling
    public void OnLoadGameButtonClicked()
    {
        StartCoroutine(LoadGameWithPauseHandling());
    }

    // FIX: Handle loading while paused
    private System.Collections.IEnumerator LoadGameWithPauseHandling()
    {
        // Remember if we were paused
        bool wasPaused = GameManager.Instance.isPaused;

        // Temporarily unpause for the load operation
        if (wasPaused)
        {
            //            Debug.Log("UIManager: Temporarily unpausing for load operation");
            Time.timeScale = 1f; // Allow coroutines to run
        }

        // Start the load operation
        SaveManager.Instance.LoadGame();

        // Wait a frame to let the load start
        yield return null;

        // The load operation will handle scene transitions and everything else
        // We don't need to restore pause state because:
        // 1. If loading same scene, player probably wants to continue playing
        // 2. If loading different scene, pause state gets reset anyway

        //     Debug.Log("UIManager: Load game initiated");
    }

    public void UpdateUIAfterSceneLoad()
    {
        //   Debug.Log("UIManager: UpdateUIAfterSceneLoad called");

        if (GameManager.Instance?.playerManager != null && GameManager.Instance?.playerData != null)
        {
            UpdateHealthBar(GameManager.Instance.playerManager.currentHealth, GameManager.Instance.playerData.maxHealth);
        }
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}