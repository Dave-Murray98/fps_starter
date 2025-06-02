using UnityEngine;
using System.Linq;

/// <summary>
/// Handles data persistence between scene changes
/// This is a persistent singleton that survives scene loading
/// </summary>
public class ScenePersistenceManager : MonoBehaviour
{
    private static ScenePersistenceManager _instance;
    public static ScenePersistenceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ScenePersistenceManager");
                _instance = go.AddComponent<ScenePersistenceManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Persistence Data")]
    [SerializeField] private GameSaveData persistentData;
    [SerializeField] private string targetScene;
    [SerializeField] private bool isLoadingFromSave;

    // Events
    public System.Action<string> OnSceneDataCollected;
    public System.Action<string> OnSceneDataRestored;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Enhanced PrepareSceneChange with save context
    /// </summary>
    public void PrepareSceneChange(string newSceneName, bool isLoadingFromSaveFile = false, SaveContext saveContext = SaveContext.SceneTransition)
    {
        targetScene = newSceneName;
        isLoadingFromSave = isLoadingFromSaveFile;

        if (!isLoadingFromSave)
        {
            // Use the appropriate save context when collecting scene data
            CollectCurrentSceneData(saveContext);
        }

        Debug.Log($"Prepared scene change to: {newSceneName}, FromSave: {isLoadingFromSave}, Context: {saveContext}");
    }

    /// <summary>
    /// Call this after a scene has loaded to restore data
    /// </summary>
    public void OnSceneLoaded(string sceneName)
    {
        if (persistentData != null && !string.IsNullOrEmpty(targetScene))
        {
            if (isLoadingFromSave)
            {
                // Loading from save file - restore all data including position
                RestoreSceneData(sceneName, true);
                RestorePlayerData();
            }
            else
            {
                // Normal scene transition - only restore scene data if we've been here before
                if (persistentData.sceneData.ContainsKey(sceneName))
                {
                    RestoreSceneData(sceneName, false);
                }
            }

            targetScene = "";
            isLoadingFromSave = false;
        }

        Debug.Log($"Scene loaded: {sceneName}");
    }

    /// <summary>
    /// Set the persistent data (usually from SaveManager)
    /// </summary>
    public void SetPersistentData(GameSaveData data)
    {
        persistentData = data;
        Debug.Log("Persistent data set");
    }

    /// <summary>
    /// Get the current persistent data
    /// </summary>
    public GameSaveData GetPersistentData()
    {
        return persistentData;
    }

    /// <summary>
    /// Enhanced CollectCurrentSceneData with context awareness
    /// </summary>
    private void CollectCurrentSceneData(SaveContext context = SaveContext.GameSave)
    {
        if (persistentData == null) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Create or get scene data
        if (!persistentData.sceneData.ContainsKey(currentScene))
        {
            persistentData.sceneData[currentScene] = new SceneSaveData();
        }

        var sceneData = persistentData.sceneData[currentScene];
        sceneData.sceneName = currentScene;
        sceneData.lastVisited = System.DateTime.Now;
        sceneData.hasBeenVisited = true;

        // Collect data from all ISaveable objects with context awareness
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>().ToArray();

        int savedObjectCount = 0;
        foreach (var saveable in saveableObjects)
        {
            try
            {
                saveable.OnBeforeSave();

                // Handle PlayerSaveComponent specially based on context
                if (saveable is PlayerSaveComponent playerSaveComponent)
                {
                    object data = context == SaveContext.SceneTransition
                        ? playerSaveComponent.GetSaveDataWithoutPosition()
                        : playerSaveComponent.GetSaveData();

                    if (data != null)
                    {
                        sceneData.SetObjectData(saveable.SaveID, data);
                        savedObjectCount++;
                    }
                }
                else
                {
                    // For all other ISaveable objects, always save normally
                    var data = saveable.GetSaveData();
                    if (data != null)
                    {
                        sceneData.SetObjectData(saveable.SaveID, data);
                        savedObjectCount++;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save data for {saveable.SaveID}: {e.Message}");
            }
        }

        OnSceneDataCollected?.Invoke(currentScene);
        Debug.Log($"Collected scene data for: {currentScene} ({savedObjectCount} objects) with context: {context}");
    }

    private void RestoreSceneData(string sceneName, bool restorePlayerPosition = true)
    {
        if (persistentData?.sceneData?.ContainsKey(sceneName) != true) return;

        var sceneData = persistentData.sceneData[sceneName];

        // Wait a frame for scene objects to initialize
        StartCoroutine(RestoreSceneDataCoroutine(sceneData, restorePlayerPosition));
    }

    private System.Collections.IEnumerator RestoreSceneDataCoroutine(SceneSaveData sceneData, bool restorePlayerPosition)
    {
        yield return null; // Wait one frame

        // Wait for GameManager to refresh its references after scene load
        yield return new WaitForSeconds(0.1f);

        // Find all ISaveable objects in the new scene
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>().ToArray();

        int restoredObjectCount = 0;
        foreach (var saveable in saveableObjects)
        {
            try
            {
                var data = sceneData.GetObjectData<object>(saveable.SaveID);
                if (data != null)
                {
                    if (saveable is PlayerSaveComponent playerSaveComponent)
                    {
                        // Handle PlayerSaveComponent based on whether we want to restore position
                        if (restorePlayerPosition)
                        {
                            playerSaveComponent.LoadSaveData(data);
                        }
                        else
                        {
                            playerSaveComponent.LoadSaveDataWithoutPosition(data);
                        }
                        Debug.Log($"Player data restored via PlayerSaveComponent {(restorePlayerPosition ? "with" : "without")} position");
                    }
                    else
                    {
                        // For all other saveable objects, load normally
                        saveable.LoadSaveData(data);
                    }

                    saveable.OnAfterLoad();
                    restoredObjectCount++;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load data for {saveable.SaveID}: {e.Message}");
            }
        }

        OnSceneDataRestored?.Invoke(sceneData.sceneName);
        Debug.Log($"Restored scene data for: {sceneData.sceneName} ({restoredObjectCount} objects)");
    }

    private void RestorePlayerData()
    {
        if (persistentData?.playerData == null) return;

        Debug.Log($"Restoring player data for scene: {persistentData.playerData.currentScene}");
        // Wait for GameManager to be ready
        StartCoroutine(RestorePlayerDataCoroutine());
    }

    private System.Collections.IEnumerator RestorePlayerDataCoroutine()
    {
        // Wait for GameManager and its references to be ready
        float timeout = 5f;
        while (GameManager.Instance == null || GameManager.Instance.playerManager == null)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
            if (timeout <= 0)
            {
                Debug.LogError("Timeout waiting for GameManager to initialize");
                yield break;
            }
        }

        // Find PlayerSaveComponent
        var playerSaveComponent = FindFirstObjectByType<PlayerSaveComponent>();
        if (playerSaveComponent != null)
        {
            try
            {
                playerSaveComponent.LoadSaveData(persistentData.playerData);
                playerSaveComponent.OnAfterLoad();
                Debug.Log("Player data restored via PlayerSaveComponent");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore player data: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("PlayerSaveComponent not found for data restoration");
        }
    }

    /// <summary>
    /// Clear persistent data (useful for new games)
    /// </summary>
    public void ClearPersistentData()
    {
        persistentData = null;
        targetScene = "";
        isLoadingFromSave = false;
        Debug.Log("Persistent data cleared");
    }
}