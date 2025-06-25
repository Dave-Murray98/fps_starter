using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Interface for centralized manager coordination.
/// All core managers should implement this for lifecycle management.
/// </summary>
public interface IManager
{
    /// <summary>
    /// Initialize the manager's core functionality and state.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Refresh component references after scene changes.
    /// </summary>
    void RefreshReferences();

    /// <summary>
    /// Clean up resources and unsubscribe from events.
    /// </summary>
    void Cleanup();
}

/// <summary>
/// Central coordinator for all game managers and core systems.
/// Handles manager lifecycle, scene transition coordination, and provides
/// unified access to save/load functionality and game state control.
/// </summary>
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

    public DayNightCycleManager dayNightManager;

    [Header("Game State")]
    public bool isPaused = false;

    // Events for manager system coordination
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
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Handles scene loaded events by refreshing manager references after a delay.
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StartCoroutine(RefreshManagerReferencesCoroutine());
    }

    /// <summary>
    /// Discovers and initializes all managers implementing IManager interface.
    /// </summary>
    private void InitializeManagers()
    {
        FindAndRegisterManagers();
        InitializeAllManagers();
        OnManagersInitialized?.Invoke();
    }

    /// <summary>
    /// Locates all manager components in the scene and registers them.
    /// </summary>
    private void FindAndRegisterManagers()
    {
        allManagers.Clear();

        // Find scene-based managers
        playerManager = FindFirstObjectByType<PlayerManager>();
        inputManager = FindFirstObjectByType<InputManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
        dayNightManager = FindFirstObjectByType<DayNightCycleManager>();

        // Register managers that implement IManager
        if (playerManager != null) allManagers.Add(playerManager);
        if (inputManager != null) allManagers.Add(inputManager);
        if (uiManager != null) allManagers.Add(uiManager);
        if (audioManager != null) allManagers.Add(audioManager);
        if (dayNightManager != null) allManagers.Add(dayNightManager);
    }

    /// <summary>
    /// Calls Initialize() on all registered managers with error handling.
    /// </summary>
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

    /// <summary>
    /// Refreshes manager references with timing to ensure scene is fully loaded.
    /// </summary>
    private IEnumerator RefreshManagerReferencesCoroutine()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        RefreshManagerReferences();
    }

    /// <summary>
    /// Refreshes all manager references after scene changes.
    /// </summary>
    private void RefreshManagerReferences()
    {
        FindAndRegisterManagers();

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
    }

    /// <summary>
    /// Initiates a game save operation via SaveManager.
    /// </summary>
    [Button]
    public void SaveGame()
    {
        SaveManager.Instance?.SaveGame();
    }

    /// <summary>
    /// Initiates a game load operation via SaveManager.
    /// </summary>
    [Button]
    public void LoadGame()
    {
        SaveManager.Instance?.LoadGame();
    }

    /// <summary>
    /// Initializes a fresh game state with default values.
    /// </summary>
    public void NewGame()
    {
        if (playerManager != null && playerData != null)
        {
            playerManager.currentHealth = playerData.maxHealth;
        }
        Debug.Log("New game started");
    }

    /// <summary>
    /// Pauses the game by setting time scale to 0 and firing pause events.
    /// </summary>
    public void PauseGame()
    {
        if (!isPaused)
        {
            isPaused = true;
            Time.timeScale = 0f;
            GameEvents.TriggerGamePaused();
        }
    }

    /// <summary>
    /// Resumes the game by restoring time scale and firing resume events.
    /// </summary>
    public void ResumeGame()
    {
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            GameEvents.TriggerGameResumed();
        }
    }

    /// <summary>
    /// Quits the game application.
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quitting Game");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Manually triggers manager reference refresh (editor only).
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void RefreshReferences()
    {
        RefreshManagerReferences();
    }

    private void OnDestroy()
    {
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