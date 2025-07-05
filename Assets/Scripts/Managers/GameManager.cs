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
/// UPDATED: Central coordinator for all game managers and core systems.
/// Now properly handles singleton InputManager and provides enhanced manager lifecycle management.
/// Handles manager lifecycle, scene transition coordination, and provides
/// unified access to save/load functionality and game state control.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configurations")]
    public PlayerData playerData;

    [Header("Scene-Based Managers")]
    public PlayerManager playerManager;
    public UIManager uiManager;
    public AudioManager audioManager;
    public InGameTimeManager timeManager;
    public WeatherManager weatherManager;

    [Header("Game State")]
    public bool isPaused = false;

    // Events for manager system coordination
    public static event Action OnManagersInitialized;
    public static event Action OnManagersRefreshed;

    // UPDATED: Separate tracking for scene-based vs persistent managers
    private List<IManager> sceneBasedManagers = new List<IManager>();
    private List<IManager> allManagers = new List<IManager>();

    // UPDATED: InputManager is now accessed via singleton
    public InputManager InputManager => InputManager.Instance;

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
    /// UPDATED: Handles scene loaded events with improved singleton manager handling
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StartCoroutine(RefreshManagerReferencesCoroutine());
    }

    /// <summary>
    /// UPDATED: Enhanced manager initialization that handles both persistent and scene-based managers
    /// </summary>
    private void InitializeManagers()
    {
        Debug.Log("[GameManager] Starting manager initialization");

        // STEP 1: Initialize or connect to persistent singleton managers
        InitializePersistentManagers();

        // STEP 2: Find and initialize scene-based managers
        FindAndRegisterSceneManagers();
        InitializeSceneBasedManagers();

        OnManagersInitialized?.Invoke();
        Debug.Log("[GameManager] Manager initialization complete");
    }

    /// <summary>
    /// UPDATED: Handles persistent singleton managers (InputManager, etc.)
    /// </summary>
    private void InitializePersistentManagers()
    {
        Debug.Log("[GameManager] Initializing persistent managers");

        // InputManager - Check if singleton exists, create if needed
        if (InputManager.Instance == null)
        {
            Debug.Log("[GameManager] Creating InputManager singleton");
            // InputManager will be created by finding it in scene or creating a new one
            var inputManagerGO = FindFirstObjectByType<InputManager>();
            if (inputManagerGO == null)
            {
                Debug.LogWarning("[GameManager] No InputManager found in scene! You need to add one.");
            }
            else
            {
                // The InputManager's Awake() will handle singleton setup
                inputManagerGO.Initialize();
            }
        }
        else
        {
            Debug.Log("[GameManager] InputManager singleton already exists - refreshing");
            InputManager.Instance.RefreshReferences();
        }

        // Add InputManager to manager list if it exists
        if (InputManager.Instance != null)
        {
            if (!allManagers.Contains(InputManager.Instance))
            {
                allManagers.Add(InputManager.Instance);
            }
        }
    }

    /// <summary>
    /// UPDATED: Finds and registers only scene-based managers
    /// </summary>
    private void FindAndRegisterSceneManagers()
    {
        sceneBasedManagers.Clear();

        // Find scene-based managers (not persistent singletons)
        playerManager = FindFirstObjectByType<PlayerManager>();
        uiManager = FindFirstObjectByType<UIManager>();
        audioManager = FindFirstObjectByType<AudioManager>();
        timeManager = FindFirstObjectByType<InGameTimeManager>();
        weatherManager = FindFirstObjectByType<WeatherManager>();

        // Register scene-based managers that implement IManager
        if (playerManager != null) sceneBasedManagers.Add(playerManager);
        if (uiManager != null) sceneBasedManagers.Add(uiManager);
        if (audioManager != null) sceneBasedManagers.Add(audioManager);
        if (timeManager != null) sceneBasedManagers.Add(timeManager);
        if (weatherManager != null) sceneBasedManagers.Add(weatherManager);

        Debug.Log($"[GameManager] Found {sceneBasedManagers.Count} scene-based managers");

        // Update the combined manager list
        UpdateAllManagersList();
    }

    /// <summary>
    /// UPDATED: Combines persistent and scene-based managers
    /// </summary>
    private void UpdateAllManagersList()
    {
        allManagers.Clear();

        // Add persistent managers
        if (InputManager.Instance != null)
        {
            allManagers.Add(InputManager.Instance);
        }

        // Add scene-based managers
        allManagers.AddRange(sceneBasedManagers);

        Debug.Log($"[GameManager] Total managers tracked: {allManagers.Count}");
    }

    /// <summary>
    /// UPDATED: Initializes only scene-based managers (persistent ones are already initialized)
    /// </summary>
    private void InitializeSceneBasedManagers()
    {
        Debug.Log("[GameManager] Initializing scene-based managers");

        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.Initialize();
                Debug.Log($"[GameManager] Initialized {manager.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to initialize {manager.GetType().Name}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// UPDATED: Enhanced reference refresh with proper singleton handling
    /// </summary>
    private IEnumerator RefreshManagerReferencesCoroutine()
    {
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        RefreshManagerReferences();
    }

    /// <summary>
    /// UPDATED: Refreshes all manager references with singleton awareness
    /// </summary>
    private void RefreshManagerReferences()
    {
        Debug.Log("[GameManager] Refreshing manager references");

        // STEP 1: Handle persistent managers
        RefreshPersistentManagers();

        // STEP 2: Re-find scene-based managers (they may have changed)
        FindAndRegisterSceneManagers();

        // STEP 3: Refresh scene-based managers
        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.RefreshReferences();
                Debug.Log($"[GameManager] Refreshed {manager.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to refresh {manager.GetType().Name}: {e.Message}");
            }
        }

        OnManagersRefreshed?.Invoke();
        Debug.Log("[GameManager] Manager refresh complete");
    }

    /// <summary>
    /// UPDATED: Handles refresh for persistent singleton managers
    /// </summary>
    private void RefreshPersistentManagers()
    {
        // InputManager - Should persist across scenes, just refresh
        if (InputManager.Instance != null)
        {
            try
            {
                InputManager.Instance.RefreshReferences();
                Debug.Log("[GameManager] Refreshed InputManager singleton");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to refresh InputManager: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] InputManager singleton is null during refresh!");
        }
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
    /// UPDATED: Manually triggers manager reference refresh with singleton support
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void RefreshReferences()
    {
        RefreshManagerReferences();
    }

    /// <summary>
    /// UPDATED: Gets the InputManager instance (singleton)
    /// </summary>
    public InputManager GetInputManager()
    {
        return InputManager.Instance;
    }

    /// <summary>
    /// UPDATED: Checks if all critical managers are available
    /// </summary>
    public bool AreManagersReady()
    {
        bool inputManagerReady = InputManager.Instance != null && InputManager.Instance.IsProperlyInitialized;
        bool playerManagerReady = playerManager != null;

        return inputManagerReady && playerManagerReady;
    }

    /// <summary>
    /// UPDATED: Returns debug info about manager states
    /// </summary>
    [Button]
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugManagerStates()
    {
        Debug.Log("=== GAMEMANAGER DEBUG INFO ===");
        Debug.Log($"Total Managers: {allManagers.Count}");
        Debug.Log($"Scene-Based Managers: {sceneBasedManagers.Count}");

        Debug.Log($"InputManager: {(InputManager.Instance != null ? "Available" : "NULL")}");
        if (InputManager.Instance != null)
        {
            Debug.Log($"  - Initialized: {InputManager.Instance.IsProperlyInitialized}");
            Debug.Log($"  - Current Mode: {InputManager.Instance.GetCurrentMovementMode()}");
        }

        Debug.Log($"PlayerManager: {(playerManager != null ? "Available" : "NULL")}");
        Debug.Log($"UIManager: {(uiManager != null ? "Available" : "NULL")}");
        Debug.Log($"AudioManager: {(audioManager != null ? "Available" : "NULL")}");
        Debug.Log("==============================");
    }

    private void OnDestroy()
    {
        // Only cleanup scene-based managers
        // Persistent managers handle their own cleanup
        foreach (var manager in sceneBasedManagers)
        {
            try
            {
                manager.Cleanup();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] Failed to cleanup {manager.GetType().Name}: {e.Message}");
            }
        }
    }
}