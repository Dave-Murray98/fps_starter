using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the context for data restoration operations
/// This tells restoration systems WHY they're being called and what they should restore
/// </summary>
public enum RestoreContext
{
    /// <summary>
    /// Player is transitioning through a doorway/portal
    /// - Restore player stats, inventory, equipment, abilities
    /// - Do NOT restore player position (doorway will set position)
    /// - Restore scene-dependent data for the target scene
    /// </summary>
    DoorwayTransition,

    /// <summary>
    /// Player is loading from a save file
    /// - Restore ALL player data INCLUDING position
    /// - Restore scene-dependent data from save file
    /// - This is a complete state restoration
    /// </summary>
    SaveFileLoad,

    /// <summary>
    /// New game initialization
    /// - Set default player stats
    /// - Clear inventory/equipment
    /// - Set starting position
    /// </summary>
    NewGame
}


/// <summary>
/// REFACTORED: SceneTransitionManager is now the SINGLE orchestrator for all scene loading
/// No other systems should subscribe to OnSceneLoaded - this handles everything
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    public float minLoadingTime = 1f;
    public bool showDebugLogs = true;

    // Current transition state
    private RestoreContext currentRestoreContext = RestoreContext.DoorwayTransition;
    private string pendingTargetDoorwayID = "";
    private Dictionary<string, object> pendingSaveData = null;

    // Events
    public System.Action<string> OnTransitionStarted;
    public System.Action<string> OnTransitionCompleted;

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
        }
    }

    private void Start()
    {
        // CRITICAL: Only SceneTransitionManager subscribes to scene loaded
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Transition through a doorway (portal-based movement)
    /// </summary>
    public void TransitionThroughDoorway(string targetScene, string targetDoorwayID)
    {
        DebugLog($"Starting doorway transition to {targetScene}, doorway: {targetDoorwayID}");

        // CRITICAL FIX: Save player data BEFORE starting transition
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Saving player data before doorway transition");
            PlayerPersistenceManager.Instance.UpdatePersistentPlayerDataForTransition();
        }
        else
        {
            Debug.LogError("PlayerPersistenceManager not found - player data will not persist!");
        }

        currentRestoreContext = RestoreContext.DoorwayTransition;
        pendingTargetDoorwayID = targetDoorwayID;
        pendingSaveData = null; // Clear any save data

        StartCoroutine(DoTransitionWithLoading(targetScene, RestoreContext.DoorwayTransition));
    }

    /// <summary>
    /// Load scene from save file
    /// </summary>
    public void LoadSceneFromSave(string targetScene, Dictionary<string, object> saveData = null)
    {
        DebugLog($"Starting save file load to {targetScene}");

        currentRestoreContext = RestoreContext.SaveFileLoad;
        pendingTargetDoorwayID = "";
        pendingSaveData = saveData; // Store save data for restoration

        StartCoroutine(DoTransitionWithLoading(targetScene, RestoreContext.SaveFileLoad));
    }

    /// <summary>
    /// Start a new game
    /// </summary>
    public void StartNewGame(string startingScene)
    {
        DebugLog($"Starting new game in {startingScene}");

        currentRestoreContext = RestoreContext.NewGame;
        pendingTargetDoorwayID = "";
        pendingSaveData = null;

        StartCoroutine(DoTransitionWithLoading(startingScene, RestoreContext.NewGame));
    }

    private System.Collections.IEnumerator DoTransitionWithLoading(string targetScene, RestoreContext context)
    {
        OnTransitionStarted?.Invoke(targetScene);
        DebugLog($"Starting {context} transition to {targetScene}");

        // Show appropriate loading screen
        switch (context)
        {
            case RestoreContext.DoorwayTransition:
                LoadingScreenManager.Instance?.ShowLoadingScreenForDoorway(targetScene);
                break;
            case RestoreContext.SaveFileLoad:
                LoadingScreenManager.Instance?.ShowLoadingScreenForSaveLoad(targetScene);
                break;
            case RestoreContext.NewGame:
                LoadingScreenManager.Instance?.ShowLoadingScreen("Starting New Game...", "Loading world");
                break;
        }

        // Prepare systems for transition
        if (context == RestoreContext.DoorwayTransition)
        {
            // Tell SceneDataManager to save current scene before transition
            SceneDataManager.Instance?.PrepareSceneTransition(targetScene, pendingTargetDoorwayID, RestoreContext.DoorwayTransition);
        }

        // Phase 1: Preparation (0-20%)
        LoadingScreenManager.Instance?.SetProgress(0.1f);
        yield return new WaitForSecondsRealtime(0.2f);

        LoadingScreenManager.Instance?.SetProgress(0.2f);
        yield return new WaitForSecondsRealtime(0.1f);

        // Phase 2: Scene Loading (20-80%)
        LoadingScreenManager.Instance?.SetProgress(0.3f);

        // Start the actual scene load
        var sceneLoadOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene);
        sceneLoadOperation.allowSceneActivation = false;

        // Monitor scene loading progress
        while (sceneLoadOperation.progress < 0.9f)
        {
            float progress = Mathf.Lerp(0.3f, 0.8f, sceneLoadOperation.progress / 0.9f);
            LoadingScreenManager.Instance?.SetProgress(progress);
            yield return null;
        }

        // Phase 3: Finalization (80-100%)
        LoadingScreenManager.Instance?.SetProgress(0.8f);

        // Ensure minimum loading time
        float loadStartTime = Time.unscaledTime;
        while (Time.unscaledTime - loadStartTime < minLoadingTime)
        {
            float timeProgress = (Time.unscaledTime - loadStartTime) / minLoadingTime;
            float progress = Mathf.Lerp(0.8f, 0.95f, timeProgress);
            LoadingScreenManager.Instance?.SetProgress(progress);
            yield return null;
        }

        // Activate the scene
        LoadingScreenManager.Instance?.SetProgress(1f);
        sceneLoadOperation.allowSceneActivation = true;

        // Wait for scene to actually load
        yield return sceneLoadOperation;

        OnTransitionCompleted?.Invoke(targetScene);
        DebugLog($"Completed {context} transition to {targetScene}");

        // Hide loading screen after a brief pause
        yield return new WaitForSecondsRealtime(0.2f);
        LoadingScreenManager.Instance?.HideLoadingScreen();
    }

    /// <summary>
    /// CENTRAL ORCHESTRATOR: This is the ONLY method that should handle OnSceneLoaded
    /// All data restoration flows through here based on the current RestoreContext
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        DebugLog($"=== SCENE LOADED: {scene.name} (Context: {currentRestoreContext}) ===");

        StartCoroutine(OrchestateDataRestoration(scene.name));
    }

    /// <summary>
    /// Orchestrates all data restoration based on the current context
    /// This is the single point of control for what gets restored and when
    /// </summary>
    private System.Collections.IEnumerator OrchestateDataRestoration(string sceneName)
    {
        // Wait for scene to fully initialize
        yield return new WaitForSecondsRealtime(0.1f);

        DebugLog($"Starting data restoration for context: {currentRestoreContext}");

        switch (currentRestoreContext)
        {
            case RestoreContext.DoorwayTransition:
                yield return HandleDoorwayTransitionRestore(sceneName);
                break;

            case RestoreContext.SaveFileLoad:
                yield return HandleSaveFileLoadRestore(sceneName);
                break;

            case RestoreContext.NewGame:
                yield return HandleNewGameRestore(sceneName);
                break;
        }

        DebugLog($"Data restoration complete for context: {currentRestoreContext}");
    }

    /// <summary>
    /// Handle restoration for doorway transitions
    /// </summary>
    private System.Collections.IEnumerator HandleDoorwayTransitionRestore(string sceneName)
    {
        DebugLog("=== DOORWAY TRANSITION RESTORATION ===");

        // 1. Restore player persistent data (NO position restore)
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Restoring player data for doorway transition (no position)");
            PlayerPersistenceManager.Instance.RestoreForDoorwayTransition();
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 2. Restore scene-dependent data
        if (SceneDataManager.Instance != null)
        {
            DebugLog("Restoring scene data for doorway transition");
            SceneDataManager.Instance.RestoreSceneDataForTransition(sceneName);
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Position player at target doorway
        if (!string.IsNullOrEmpty(pendingTargetDoorwayID))
        {
            DebugLog($"Positioning player at doorway: {pendingTargetDoorwayID}");
            PositionPlayerAtDoorway(pendingTargetDoorwayID);
        }
    }

    /// <summary>
    /// Handle restoration for save file loads
    /// </summary>
    private System.Collections.IEnumerator HandleSaveFileLoadRestore(string sceneName)
    {
        DebugLog("=== SAVE FILE LOAD RESTORATION ===");

        // 1. Restore player data including position
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Restoring player data from save file (including position)");
            PlayerPersistenceManager.Instance.RestoreFromSaveFile(pendingSaveData);
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 2. Restore scene data from save file
        if (SceneDataManager.Instance != null && pendingSaveData != null)
        {
            DebugLog("Restoring scene data from save file");
            SceneDataManager.Instance.RestoreSceneDataFromSave(pendingSaveData);
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Update UI systems
        if (GameManager.Instance?.uiManager != null)
        {
            DebugLog("Refreshing UI after save load");
            GameManager.Instance.uiManager.RefreshReferences();
        }

        // 4. Ensure game is unpaused
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            DebugLog("Unpausing game after save load");
            GameManager.Instance.ResumeGame();
        }
    }

    /// <summary>
    /// Handle restoration for new game
    /// </summary>
    private System.Collections.IEnumerator HandleNewGameRestore(string sceneName)
    {
        DebugLog("=== NEW GAME INITIALIZATION ===");

        // 1. Initialize player with default values
        if (PlayerPersistenceManager.Instance != null)
        {
            DebugLog("Initializing player for new game");
            PlayerPersistenceManager.Instance.InitializeForNewGame();
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 2. Clear any existing scene data
        if (SceneDataManager.Instance != null)
        {
            DebugLog("Clearing scene data for new game");
            SceneDataManager.Instance.ClearAllSceneData();
        }

        yield return new WaitForSecondsRealtime(0.05f);

        // 3. Update UI
        if (GameManager.Instance?.uiManager != null)
        {
            DebugLog("Refreshing UI for new game");
            GameManager.Instance.uiManager.RefreshReferences();
        }
    }

    /// <summary>
    /// Position player at specified doorway
    /// </summary>
    private void PositionPlayerAtDoorway(string doorwayID)
    {
        if (string.IsNullOrEmpty(doorwayID))
        {
            DebugLog("No target doorway specified");
            return;
        }

        Doorway[] doorways = FindObjectsByType<Doorway>(FindObjectsSortMode.None);
        Doorway targetDoorway = System.Array.Find(doorways, d => d.doorwayID == doorwayID);

        if (targetDoorway != null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                Vector3 doorwayPosition = targetDoorway.transform.position + Vector3.up * 0.1f;
                player.transform.position = doorwayPosition;
                DebugLog($"Positioned player at doorway: {doorwayID} ({doorwayPosition})");
            }
            else
            {
                Debug.LogWarning("Player not found - cannot position at doorway");
            }
        }
        else
        {
            Debug.LogWarning($"Target doorway not found: {doorwayID}");
        }
    }

    /// <summary>
    /// Get current restore context (useful for debugging)
    /// </summary>
    public RestoreContext GetCurrentRestoreContext() => currentRestoreContext;

    /// <summary>
    /// Check if we're currently handling a specific restore context
    /// </summary>
    public bool IsHandlingContext(RestoreContext context) => currentRestoreContext == context;

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneTransitionManager] {message}");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}