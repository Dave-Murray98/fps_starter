using UnityEngine;


/// <summary>
/// Simplified save context - only two cases matter
/// </summary>
public enum TransitionType
{
    Doorway,      // Player using portal/doorway - use scene persistence
    SaveLoad     // Player loading a save file - override with save data
}

/// SIMPLIFIED Scene Transition Manager
/// Only handles transitions - no data management
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    public float minLoadingTime = 1f;
    public bool showDebugLogs = true;

    // Track if we're loading from a save (to handle positioning correctly)
    private bool isLoadingFromSave = false;

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
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Transition through a doorway (portal-based movement)
    /// </summary>
    public void TransitionThroughDoorway(string targetScene, string targetDoorwayID)
    {
        isLoadingFromSave = false;
        StartCoroutine(DoTransitionWithLoading(targetScene, targetDoorwayID, TransitionType.Doorway));
    }

    /// <summary>
    /// Load scene from save file
    /// </summary>
    public void LoadSceneFromSave(string targetScene)
    {
        isLoadingFromSave = true;
        StartCoroutine(DoTransitionWithLoading(targetScene, "", TransitionType.SaveLoad));
    }

    private System.Collections.IEnumerator DoTransitionWithLoading(string targetScene, string targetDoorwayID, TransitionType transitionType)
    {
        OnTransitionStarted?.Invoke(targetScene);
        DebugLog($"Starting {transitionType} transition to {targetScene}");

        // Show appropriate loading screen
        if (transitionType == TransitionType.Doorway)
        {
            LoadingScreenManager.Instance?.ShowLoadingScreenForDoorway(targetScene);
        }
        else
        {
            LoadingScreenManager.Instance?.ShowLoadingScreenForSaveLoad(targetScene);
        }

        // Prepare the save system
        SceneDataManager.Instance?.PrepareSceneTransition(targetScene, targetDoorwayID, transitionType);

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
        DebugLog($"Completed {transitionType} transition to {targetScene}");

        // Hide loading screen after a brief pause
        yield return new WaitForSecondsRealtime(0.2f);
        LoadingScreenManager.Instance?.HideLoadingScreen();
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (isLoadingFromSave)
        {
            StartCoroutine(RestorePlayerPositionFromSave());
        }
    }

    private System.Collections.IEnumerator RestorePlayerPositionFromSave()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        if (SaveManager.Instance != null)
        {
            DebugLog("SceneTransitionManager: Letting SaveManager handle player positioning");
        }
    }

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