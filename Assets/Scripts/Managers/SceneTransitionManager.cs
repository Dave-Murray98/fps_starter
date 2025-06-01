using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Manages scene transitions with spawn points and save data integration
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    public bool useSpawnPointsForNewScenes = true;
    public bool useSavedPositionForLoads = true;

    // Events
    public System.Action<string> OnSceneTransitionStarted;
    public System.Action<string> OnSceneTransitionCompleted;

    // State tracking
    private bool isTransitioning = false;
    private string targetSceneName;
    private string targetSpawnPointID;
    private bool isLoadingFromSave = false;

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

    /// <summary>
    /// Transition to a new scene using spawn points (for portals/doors)
    /// </summary>
    public void TransitionToScene(string sceneName, string spawnPointID = "DefaultSpawn")
    {
        if (isTransitioning) return;

        Debug.Log($"Transitioning to scene: {sceneName} at spawn point: {spawnPointID}");

        targetSceneName = sceneName;
        targetSpawnPointID = spawnPointID;
        isLoadingFromSave = false;

        StartTransition();
    }

    /// <summary>
    /// Load a scene from save data (exact position restoration)
    /// </summary>
    public void LoadSceneFromSave(string sceneName)
    {
        if (isTransitioning) return;

        Debug.Log($"Loading scene from save: {sceneName}");

        targetSceneName = sceneName;
        targetSpawnPointID = null;
        isLoadingFromSave = true;

        StartTransition();
    }

    private void StartTransition()
    {
        isTransitioning = true;
        OnSceneTransitionStarted?.Invoke(targetSceneName);
        Debug.Log($"🔄 Starting scene transition, isLoadingFromSave = {isLoadingFromSave}");
        // Instead of saving the entire game, just collect current scene data
        if (!isLoadingFromSave)
        {
            // Tell ScenePersistenceManager to prepare for scene change
            ScenePersistenceManager.Instance?.PrepareSceneChange(targetSceneName, false);

            Debug.Log("Collected current scene data before transition (no save file written)");
        }
        else
        {
            // Loading from save - let ScenePersistenceManager know
            ScenePersistenceManager.Instance?.PrepareSceneChange(targetSceneName, true);

            Debug.Log("Preparing to load scene from save data");
        }

        // Load the target scene
        SceneManager.LoadScene(targetSceneName);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isTransitioning) return;

        StartCoroutine(HandleSceneLoadedCoroutine(scene.name));
    }

    private IEnumerator HandleSceneLoadedCoroutine(string sceneName)
    {
        // Wait for all objects to be initialized
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        // Handle player positioning
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            if (isLoadingFromSave && useSavedPositionForLoads)
            {
                // NEW: Actually load position from save data
                player.LoadPositionFromSaveSystem();
                Debug.Log("Player position restored from save data");
            }
            else if (useSpawnPointsForNewScenes)
            {
                // Use spawn point for scene transitions
                var spawnPoint = SpawnPoint.FindSpawnPoint(targetSpawnPointID);
                if (spawnPoint != null)
                {
                    player.transform.position = spawnPoint.transform.position;
                    player.transform.rotation = spawnPoint.transform.rotation;
                    Debug.Log($"Player spawned at: {targetSpawnPointID}, Position: {spawnPoint.transform.position}");
                }
                else
                {
                    Debug.LogWarning($"Spawn point '{targetSpawnPointID}' not found in scene '{sceneName}'");
                }
            }

            // Refresh player references after scene load
            player.RefreshComponentReferences();
            GameManager.Instance.uiManager?.UpdateUIAfterSceneLoad();
        }
        else
        {
            Debug.LogError("PlayerController not found in new scene!");
        }

        // Complete transition
        isTransitioning = false;
        OnSceneTransitionCompleted?.Invoke(sceneName);

        Debug.Log($"Scene transition to '{sceneName}' completed");
    }
}