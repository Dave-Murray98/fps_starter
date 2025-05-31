using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenu;
    public Slider healthBar;
    public TextMeshProUGUI healthText;


    public void Initialize()
    {
        Debug.Log("UIManager Initialized");

        GameEvents.OnPlayerHealthChanged += UpdateHealthBar;
        GameEvents.OnGamePaused += ShowPauseMenu;
        GameEvents.OnGameResumed += HidePauseMenu;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(false);
        }
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
            healthText.text = $"{currentHealth}/{maxHealth}";
        }
    }

    public void OnResumeButtonClicked()
    {
        GameManager.Instance.ResumeGame(); // This will toggle the pause state
    }

    public void OnQuitButtonClicked()
    {
        GameManager.Instance.QuitGame();
    }

    private void OnDestroy()
    {
        GameEvents.OnPlayerHealthChanged -= UpdateHealthBar;
        GameEvents.OnGamePaused -= ShowPauseMenu;
        GameEvents.OnGameResumed -= HidePauseMenu;
    }

}
