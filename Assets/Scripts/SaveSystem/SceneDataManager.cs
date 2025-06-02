using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// SINGLE RESPONSIBILITY: Manages scene data persistence
/// Replaces both ScenePersistenceManager and parts of SaveManager
/// </summary>
public class SceneDataManager : MonoBehaviour
{
    public static SceneDataManager Instance { get; private set; }

    [Header("Data Storage")]
    [SerializeField] private SceneDataContainer sceneDataContainer;
    [SerializeField] private bool showDebugLogs = true;

    // Transition state
    private string pendingTargetScene = "";
    private string pendingTargetDoorway = "";
    private TransitionType pendingTransitionType;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sceneDataContainer = new SceneDataContainer();
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

    public void PrepareSceneTransition(string targetScene, string targetDoorway, TransitionType transitionType)
    {
        DebugLog($"Preparing transition to {targetScene} via {transitionType}");

        // Save current scene data if this is a portal transition
        if (transitionType == TransitionType.Portal)
        {
            SaveCurrentSceneData();
        }

        pendingTargetScene = targetScene;
        pendingTargetDoorway = targetDoorway;
        pendingTransitionType = transitionType;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StartCoroutine(HandleSceneLoaded(scene.name));
    }

    private System.Collections.IEnumerator HandleSceneLoaded(string sceneName)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        if (pendingTransitionType == TransitionType.Portal)
        {
            // Portal transition - restore scene data and position player at doorway
            RestoreSceneData(sceneName);
            PositionPlayerAtDoorway(pendingTargetDoorway);
        }
        else if (pendingTransitionType == TransitionType.SaveLoad)
        {
            // Save load - let SaveManager handle scene restoration
            // SceneDataManager does nothing here
        }

        // Clear pending state
        pendingTargetScene = "";
        pendingTargetDoorway = "";
    }

    /// <summary>
    /// Save current scene's data (EXCLUDING player data)
    /// </summary>
    private void SaveCurrentSceneData()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        DebugLog($"Saving scene data for: {currentScene}");

        var sceneData = new SceneSaveData();
        sceneData.sceneName = currentScene;
        sceneData.lastVisited = System.DateTime.Now;

        // Save all saveable objects EXCEPT player-related components
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => !IsPlayerRelatedComponent(s))
            .ToArray();

        foreach (var saveable in saveableObjects)
        {
            try
            {
                saveable.OnBeforeSave();
                var data = saveable.GetSaveData();
                if (data != null)
                {
                    sceneData.SetObjectData(saveable.SaveID, data);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save {saveable.SaveID}: {e.Message}");
            }
        }

        sceneDataContainer.SetSceneData(currentScene, sceneData);
        DebugLog($"Saved {saveableObjects.Length} scene objects for: {currentScene}");
    }

    /// <summary>
    /// Check if a saveable component is player-related
    /// </summary>
    private bool IsPlayerRelatedComponent(ISaveable saveable)
    {
        // Add more player-related components here as needed
        return saveable is PlayerSaveComponent ||
               saveable.SaveID.Contains("Player") ||
               saveable.SaveID.Contains("player");
    }

    /// <summary>
    /// Restore scene data (EXCLUDING player data)
    /// </summary>
    private void RestoreSceneData(string sceneName)
    {
        var sceneData = sceneDataContainer.GetSceneData(sceneName);
        if (sceneData == null)
        {
            DebugLog($"No scene data for: {sceneName}");
            return;
        }

        DebugLog($"Restoring scene data for: {sceneName}");

        // Restore all objects EXCEPT player-related
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => !IsPlayerRelatedComponent(s))
            .ToArray();

        foreach (var saveable in saveableObjects)
        {
            try
            {
                var data = sceneData.GetObjectData<object>(saveable.SaveID);
                if (data != null)
                {
                    saveable.LoadSaveData(data);
                    saveable.OnAfterLoad();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore {saveable.SaveID}: {e.Message}");
            }
        }
    }

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
                player.transform.position = targetDoorway.transform.position + Vector3.up * 0.1f;
                DebugLog($"Positioned player at doorway: {doorwayID}");
            }
        }
        else
        {
            Debug.LogWarning($"Target doorway not found: {doorwayID}");
        }
    }

    /// <summary>
    /// Get scene data for SaveManager
    /// </summary>
    public Dictionary<string, SceneSaveData> GetSceneDataForSaving()
    {
        // Save current scene first
        SaveCurrentSceneData();
        return new Dictionary<string, SceneSaveData>(sceneDataContainer.sceneData);
    }

    /// <summary>
    /// Load scene data from SaveManager
    /// </summary>
    public void LoadSceneDataFromSave(Dictionary<string, SceneSaveData> saveData)
    {
        sceneDataContainer.sceneData = new Dictionary<string, SceneSaveData>(saveData);
        DebugLog("Scene data loaded from save file");
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneDataManager] {message}");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}