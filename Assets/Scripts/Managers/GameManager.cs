using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;


/// <summary>
/// Interface that all managers should implement for centralized management
/// </summary>
public interface IManager
{
    /// <summary>
    /// Called when the manager should initialize itself
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called when managers should refresh their references after scene load
    /// </summary>
    void RefreshReferences();

    /// <summary>
    /// Called when the manager should clean up
    /// </summary>
    void Cleanup();
}


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

    // Events for manager system
    public static event Action OnManagersInitialized;
    public static event Action OnManagersRefreshed;

    private List<IManager> allManagers = new List<IManager>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeManagers();

        // Initialize save system
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete += OnSaveComplete;
            SaveManager.Instance.OnLoadComplete += OnLoadComplete;
        }
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
        Debug.Log($"Scene loaded: {scene.name}, refreshing manager references");
        StartCoroutine(RefreshManagerReferencesCoroutine());
    }

    private void InitializeManagers()
    {
        Debug.Log("GameManager: Initializing managers...");

        FindAndRegisterManagers();
        InitializeAllManagers();

        OnManagersInitialized?.Invoke();
        Debug.Log("GameManager: All managers initialized");
    }

    private void FindAndRegisterManagers()
    {
        allManagers.Clear();

        // ALWAYS re-find scene-based managers (they change with each scene)
        playerManager = FindFirstObjectByType<PlayerManager>();
        inputManager = FindFirstObjectByType<InputManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        audioManager = FindFirstObjectByType<AudioManager>();

        Debug.Log($"GameManager: Found managers - Player: {playerManager != null}, Input: {inputManager != null}, UI: {uiManager != null}, Audio: {audioManager != null}");

        // Register managers that implement IManager
        if (playerManager != null) allManagers.Add(playerManager);
        if (inputManager != null) allManagers.Add(inputManager);
        if (uiManager != null) allManagers.Add(uiManager);
        if (audioManager != null) allManagers.Add(audioManager);

        Debug.Log($"GameManager: Registered {allManagers.Count} managers in scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
    }

    private void InitializeAllManagers()
    {
        foreach (var manager in allManagers)
        {
            try
            {
                manager.Initialize();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize manager {manager.GetType().Name}: {e.Message}");
            }
        }
    }

    private IEnumerator RefreshManagerReferencesCoroutine()
    {
        // Wait a frame for all objects to be created
        yield return null;

        // Additional wait to ensure everything is properly set up
        yield return new WaitForSeconds(0.1f);

        RefreshManagerReferences();
    }

    private void RefreshManagerReferences()
    {
        Debug.Log("GameManager: Refreshing manager references...");

        // Re-find and re-register managers
        FindAndRegisterManagers();

        // Refresh all manager references
        foreach (var manager in allManagers)
        {
            try
            {
                manager.RefreshReferences();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to refresh references for manager {manager.GetType().Name}: {e.Message}");
            }
        }

        OnManagersRefreshed?.Invoke();
        Debug.Log("GameManager: Manager references refreshed");
    }

    // Existing methods...
    private void OnSaveComplete(bool success)
    {
        if (success)
        {
            Debug.Log("Game saved successfully!");
        }
        else
        {
            Debug.LogError("Failed to save game!");
        }
    }

    private void OnLoadComplete(bool success)
    {
        if (success)
        {
            Debug.Log("Game loaded successfully!");
        }
        else
        {
            Debug.LogError("Failed to load game!");
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
            Time.timeScale = 0f;
            GameEvents.TriggerGamePaused();
        }
    }

    public void ResumeGame()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            GameEvents.TriggerGameResumed();
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void RefreshReferences()
    {
        RefreshManagerReferences();
    }

    private void OnDestroy()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete -= OnSaveComplete;
            SaveManager.Instance.OnLoadComplete -= OnLoadComplete;
        }

        // Cleanup all managers
        foreach (var manager in allManagers)
        {
            try
            {
                manager.Cleanup();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to cleanup manager {manager.GetType().Name}: {e.Message}");
            }
        }
    }
}