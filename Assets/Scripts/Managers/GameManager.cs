using System;
using BehaviorDesigner.Runtime.Tasks.Unity.UnityAnimator;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    public static GameManager Instance { get; private set; }


    [Header("Configurations")]
    public PlayerData playerData;

    [Header("Managers")]
    public PlayerManager playerManager;
    public InputManager inputManager;
    public UIManager uiManager;
    public AudioManager audioManager;


    [Header("Game State")]
    public bool isPaused = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeManagers();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeManagers()
    {
        if (playerManager == null)
            playerManager = FindFirstObjectByType<PlayerManager>();
        if (inputManager == null)
            inputManager = FindFirstObjectByType<InputManager>();
        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManager>();
        if (audioManager == null)
            audioManager = FindFirstObjectByType<AudioManager>();

        playerManager?.Initialize();
        inputManager?.Initialize();
        uiManager?.Initialize();
        audioManager?.Initialize();

    }

    public void PauseGame()
    {
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f; // Pause the game
            GameEvents.TriggerGamePaused();
            Debug.Log("Game Paused");
        }
    }

    public void ResumeGame()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f; // Resume the game
            GameEvents.TriggerGameResumed();
            Debug.Log("Game Resumed");
        }
    }


    public void QuitGame()
    {
        Debug.Log("Quitting Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop playing in the editor
#else
        Application.Quit();
#endif
    }
}
