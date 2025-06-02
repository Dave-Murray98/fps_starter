using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Handles smooth transitions between scenes with loading screens
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingScreen;
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private float minimumLoadTime = 1f; // Minimum time to show loading screen

    [Header("Transition Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Events
    public System.Action<string> OnSceneTransitionStarted;
    public System.Action<string> OnSceneTransitionCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Standard scene transition (used by portals)
    /// </summary>
    public void TransitionToScene(string sceneName, string spawnPointID = "DefaultSpawn")
    {
        StartCoroutine(StartTransition(sceneName, spawnPointID));
    }

    /// <summary>
    /// Scene transition for loading from save file
    /// </summary>
    public void LoadSceneFromSave(string sceneName)
    {
        StartCoroutine(StartTransitionFromSave(sceneName));
    }

    /// <summary>
    /// Standard scene transition coroutine
    /// </summary>
    private IEnumerator StartTransition(string sceneName, string spawnPointID)
    {
        DebugLog($"Starting scene transition to: {sceneName}");

        // Trigger transition started event
        OnSceneTransitionStarted?.Invoke(sceneName);

        // CRITICAL: Clean up current scene managers BEFORE scene load to prevent old event subscriptions
        CleanupCurrentSceneManagers();

        // IMPORTANT: Use SceneTransition context here to avoid saving player position at portal
        ScenePersistenceManager.Instance.PrepareSceneChange(sceneName, false, SaveContext.SceneTransition);

        // Show loading screen
        ShowLoadingScreen(true);

        yield return new WaitForSeconds(0.1f);

        // Load the new scene
        yield return StartCoroutine(LoadSceneAsync(sceneName));

        // Set up player spawn position
        SetupPlayerSpawn(spawnPointID);

        // Hide loading screen
        ShowLoadingScreen(false);

        // Trigger transition completed event
        OnSceneTransitionCompleted?.Invoke(sceneName);

        DebugLog($"Scene transition to {sceneName} completed");
    }

    /// <summary>
    /// Scene transition from save file coroutine
    /// </summary>
    private IEnumerator StartTransitionFromSave(string sceneName)
    {
        DebugLog($"Starting scene transition from save to: {sceneName}");

        // Trigger transition started event
        OnSceneTransitionStarted?.Invoke(sceneName);

        // CRITICAL: Clean up current scene managers BEFORE scene load
        CleanupCurrentSceneManagers();

        // IMPORTANT: Use GameSave context here to preserve save data
        ScenePersistenceManager.Instance.PrepareSceneChange(sceneName, true, SaveContext.GameSave);

        // Show loading screen
        ShowLoadingScreen(true);

        yield return new WaitForSeconds(0.1f);

        DebugLog("waiting 0.1 seconds before loading new scene...");

        // Load the new scene
        yield return StartCoroutine(LoadSceneAsync(sceneName));

        // Don't set spawn position - let the save system handle player positioning

        // Hide loading screen
        ShowLoadingScreen(false);

        // Trigger transition completed event
        OnSceneTransitionCompleted?.Invoke(sceneName);

        DebugLog($"Scene transition from save to {sceneName} completed");
    }

    /// <summary>
    /// Clean up all current scene managers to prevent old event subscriptions
    /// </summary>
    private void CleanupCurrentSceneManagers()
    {
        DebugLog("Cleaning up current scene managers before transition...");

        // Force cleanup of current managers through GameManager
        if (GameManager.Instance != null)
        {
            // Get current managers and force cleanup
            var currentInputManager = GameManager.Instance.inputManager;
            var currentPlayerManager = GameManager.Instance.playerManager;
            var currentUIManager = GameManager.Instance.uiManager;
            var currentAudioManager = GameManager.Instance.audioManager;

            // Clean up in reverse order of dependency
            if (currentInputManager != null)
            {
                DebugLog($"Cleaning up InputManager {currentInputManager.GetInstanceID()}");
                currentInputManager.Cleanup();
            }

            if (currentPlayerManager != null)
            {
                DebugLog($"Cleaning up PlayerManager {currentPlayerManager.GetInstanceID()}");
                currentPlayerManager.Cleanup();
            }

            if (currentUIManager != null)
            {
                DebugLog($"Cleaning up UIManager {currentUIManager.GetInstanceID()}");
                currentUIManager.Cleanup();
            }

            if (currentAudioManager != null)
            {
                DebugLog($"Cleaning up AudioManager {currentAudioManager.GetInstanceID()}");
                currentAudioManager.Cleanup();
            }

            // Also clean up any PlayerController connections
            var playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                DebugLog("Disconnecting PlayerController from old managers");
                // PlayerController will handle its own cleanup in OnDestroy
            }
        }

        DebugLog("Current scene managers cleaned up");
    }

    /// <summary>
    /// Async scene loading with progress tracking
    /// </summary>
    private IEnumerator LoadSceneAsync(string sceneName)
    {
        DebugLog($"Loading sceneAsync: {sceneName}");

        float startTime = Time.time;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // Wait for scene to be almost loaded
        while (asyncLoad.progress < 0.9f)
        {
            UpdateLoadingProgress(asyncLoad.progress);
            yield return null;
        }

        // Ensure minimum load time for smooth UX
        float elapsedTime = Time.time - startTime;
        if (elapsedTime < minimumLoadTime)
        {
            yield return new WaitForSeconds(minimumLoadTime - elapsedTime);
        }

        // Complete the loading
        UpdateLoadingProgress(1f);
        asyncLoad.allowSceneActivation = true;

        // Wait for scene to be fully loaded
        yield return new WaitUntil(() => asyncLoad.isDone);
    }

    /// <summary>
    /// Set up player spawn position
    /// </summary>
    private void SetupPlayerSpawn(string spawnPointID)
    {
        var spawnPoints = SpawnPoint.GetAllSpawnPoints();
        SpawnPoint targetSpawn = null;

        // Find the specific spawn point
        foreach (var spawn in spawnPoints)
        {
            if (spawn.spawnPointID == spawnPointID)
            {
                targetSpawn = spawn;
                break;
            }
        }

        // Fallback to default spawn if specific one not found
        if (targetSpawn == null)
        {
            foreach (var spawn in spawnPoints)
            {
                if (spawn.spawnPointID == "DefaultSpawn")
                {
                    targetSpawn = spawn;
                    break;
                }
            }
        }

        // Fallback to first spawn point if no default found
        if (targetSpawn == null && spawnPoints.Length > 0)
        {
            targetSpawn = spawnPoints[0];
        }

        if (targetSpawn != null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = targetSpawn.transform.position;
                player.transform.rotation = targetSpawn.transform.rotation;
                DebugLog($"Player spawned at: {spawnPointID} ({targetSpawn.transform.position})");
            }
            else
            {
                Debug.LogWarning("Player not found for spawn positioning");
            }
        }
        else
        {
            Debug.LogWarning($"No spawn point found for ID: {spawnPointID}");
        }
    }

    /// <summary>
    /// Show/hide loading screen
    /// </summary>
    private void ShowLoadingScreen(bool show)
    {
        DebugLog($"Setting loading screen visibility to: {show}");
        if (loadingScreen == null)
        {
            loadingScreen = GetComponentInChildren<Canvas>()?.gameObject;
            loadingSlider = loadingScreen?.GetComponentInChildren<Slider>();
        }

        if (loadingScreen != null)
        {
            loadingScreen.SetActive(show);
            if (show && loadingSlider != null)
            {
                loadingSlider.value = 0f;
            }
        }
    }

    /// <summary>
    /// Update loading progress
    /// </summary>
    private void UpdateLoadingProgress(float progress)
    {
        if (loadingSlider != null)
        {
            loadingSlider.value = progress;
        }
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionManager] {message}");
        }
    }
}