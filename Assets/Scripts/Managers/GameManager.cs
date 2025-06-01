using System;
using BehaviorDesigner.Runtime.Tasks.Unity.UnityAnimator;
using Sirenix.OdinInspector;
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

    [Header("Save System")]
    public bool enableAutoSave = true;

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

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Re-find manager references after scene load
        StartCoroutine(RefreshManagerReferences());
    }

    private void Start()
    {
        // Initialize save system
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete += OnSaveComplete;
            SaveManager.Instance.OnLoadComplete += OnLoadComplete;
        }
    }

    private void OnSaveComplete(bool success)
    {
        if (success)
        {
            Debug.Log("Game saved successfully!");
            // Show save confirmation UI
        }
        else
        {
            Debug.LogError("Failed to save game!");
            // Show error message
        }
    }

    private void OnLoadComplete(bool success)
    {
        if (success)
        {
            Debug.Log("Game loaded successfully!");
            // Hide loading screen, update UI
        }
        else
        {
            Debug.LogError("Failed to load game!");
            // Show error message, return to main menu
        }
    }

    [Button]
    public void SaveGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }
    }

    [Button]
    public void LoadGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadGame();
        }
    }

    public void NewGame()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.NewGame();
        }
    }

    public void PauseGame()
    {
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f; // Pause the game
            GameEvents.TriggerGamePaused();
            //Debug.Log("Game Paused");
        }
    }

    public void ResumeGame()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f; // Resume the game
            GameEvents.TriggerGameResumed();
            //Debug.Log("Game Resumed");
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


    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete -= OnSaveComplete;
            SaveManager.Instance.OnLoadComplete -= OnLoadComplete;
        }
    }

    private System.Collections.IEnumerator RefreshManagerReferences()
    {
        // Wait a frame for all objects to be created
        yield return null;

        // Re-find managers in the new scene
        if (playerManager == null)
            playerManager = FindFirstObjectByType<PlayerManager>();
        if (inputManager == null)
            inputManager = FindFirstObjectByType<InputManager>();
        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManager>();
        if (audioManager == null)
            audioManager = FindFirstObjectByType<AudioManager>();

        // Re-initialize managers
        playerManager?.Initialize();
        inputManager?.Initialize();
        uiManager?.Initialize();
        audioManager?.Initialize();

        Debug.Log("GameManager references refreshed after scene load");
    }

    // Add this method for manual refresh if needed
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void RefreshReferences()
    {
        StartCoroutine(RefreshManagerReferences());
    }
}
