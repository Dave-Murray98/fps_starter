using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// REFACTORED: SceneDataManager no longer subscribes to OnSceneLoaded
/// SceneTransitionManager calls us when needed - much cleaner separation of concerns
/// Focuses purely on scene data persistence without scene transition management
/// UPDATED: Simplified with unified context-aware loading
/// </summary>
public class SceneDataManager : MonoBehaviour
{
    public static SceneDataManager Instance { get; private set; }

    [Header("Data Storage")]
    [ShowInInspector][SerializeField] private SceneDataContainer sceneDataContainer;
    [SerializeField] private bool showDebugLogs = true;

    [Header("Editor Debug Tools")]
    [SerializeField] private bool showEditorDebugTools = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            sceneDataContainer = new SceneDataContainer();
            DebugLog("SceneDataManager initialized");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Called by doorway transition system to prepare for scene change
    /// Saves current scene data before transitioning
    /// </summary>
    public void PrepareSceneTransition(string targetScene, string targetDoorway, RestoreContext restoreContext)
    {
        DebugLog($"Preparing transition to {targetScene} via {restoreContext}");

        // Save current scene data if this is a portal transition
        if (restoreContext == RestoreContext.DoorwayTransition)
        {
            SaveCurrentSceneData();
        }
    }

    /// <summary>
    /// Called by SceneTransitionManager when it needs to restore scene data for doorway transitions
    /// </summary>
    public void RestoreSceneDataForTransition(string sceneName)
    {
        DebugLog($"Restoring scene data for doorway transition: {sceneName}");
        RestoreSceneData(sceneName, RestoreContext.DoorwayTransition);
    }

    /// <summary>
    /// Called by SceneTransitionManager when it needs to restore scene data from save file
    /// </summary>
    public void RestoreSceneDataFromSave(Dictionary<string, object> saveData)
    {
        DebugLog("Loading scene data from save file");

        if (saveData != null && saveData.ContainsKey("sceneData"))
        {
            if (saveData["sceneData"] is Dictionary<string, SceneSaveData> sceneDataDict)
            {
                sceneDataContainer.sceneData = new Dictionary<string, SceneSaveData>(sceneDataDict);
                DebugLog($"Loaded {sceneDataDict.Count} scenes from save file");

                // Restore current scene data
                string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                RestoreSceneData(currentScene, RestoreContext.SaveFileLoad);
            }
            else
            {
                Debug.LogWarning("Scene data in save file is not the expected format");
            }
        }
        else
        {
            DebugLog("No scene data found in save file");
        }
    }

    /// <summary>
    /// Clear all scene data (useful for new game)
    /// </summary>
    public void ClearAllSceneData()
    {
        sceneDataContainer.sceneData.Clear();
        DebugLog("All scene data cleared for new game");
    }

    /// <summary>
    /// Save current scene's data (EXCLUDING player data)
    /// </summary>
    public void SaveCurrentSceneData()
    {
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        DebugLog($"Saving scene data for: {currentScene}");

        var sceneData = new SceneSaveData();
        sceneData.sceneName = currentScene;
        sceneData.lastVisited = System.DateTime.Now;
        sceneData.hasBeenVisited = true;

        // Save all saveable objects EXCEPT player-related components
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.SceneDependent)
            .ToArray();

        DebugLog($"Found {saveableObjects.Length} scene-dependent saveables to process");

        foreach (var saveable in saveableObjects)
        {
            try
            {
                saveable.OnBeforeSave();
                var data = saveable.GetDataToSave();
                if (data != null)
                {
                    sceneData.SetObjectData(saveable.SaveID, data);
                    DebugLog($"Saved data for: {saveable.SaveID}");
                }
                else
                {
                    DebugLog($"No data to save for: {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save {saveable.SaveID}: {e.Message}");
            }
        }

        sceneDataContainer.SetSceneData(currentScene, sceneData);
        DebugLog($"Saved {saveableObjects.Length} scene objects for: {currentScene} (Total objects with data: {sceneData.objectData.Count})");
    }

    /// <summary>
    /// Restore scene data (EXCLUDING player data)
    /// UPDATED: Now uses context-aware loading for all saveables
    /// </summary>
    private void RestoreSceneData(string sceneName, RestoreContext context)
    {
        var sceneData = sceneDataContainer.GetSceneData(sceneName);
        if (sceneData == null)
        {
            DebugLog($"No scene data for: {sceneName} (this is normal for first visit)");
            return;
        }

        DebugLog($"Restoring scene data for: {sceneName} ({sceneData.objectData.Count} objects) with context: {context}");

        // Restore all objects EXCEPT player-related
        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.SceneDependent)
            .ToArray();

        DebugLog($"Found {saveableObjects.Length} scene-dependent saveables to restore");

        int restoredCount = 0;
        foreach (var saveable in saveableObjects)
        {
            try
            {
                var data = sceneData.GetObjectData<object>(saveable.SaveID);
                if (data != null)
                {
                    // Use context-aware loading - all saveables now support this
                    saveable.LoadSaveDataWithContext(data, context);
                    saveable.OnAfterLoad();
                    restoredCount++;
                    DebugLog($"Restored scene object: {saveable.SaveID}");
                }
                else
                {
                    DebugLog($"No saved data found for: {saveable.SaveID}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore {saveable.SaveID}: {e.Message}");
            }
        }

        DebugLog($"Scene data restoration complete for: {sceneName} ({restoredCount}/{saveableObjects.Length} objects restored)");
    }

    /// <summary>
    /// Get scene data for SaveManager
    /// </summary>
    public Dictionary<string, SceneSaveData> GetSceneDataForSaving()
    {
        DebugLog("GetSceneDataForSaving called - forcing current scene save...");

        // Save current scene first
        SaveCurrentSceneData();

        // Immediate check
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneDataContainer.sceneData.ContainsKey(currentScene))
        {
            var sceneData = sceneDataContainer.sceneData[currentScene];
            DebugLog($"IMMEDIATE: Current scene '{currentScene}' found with {sceneData.objectData.Count} objects");
        }
        else
        {
            DebugLog($"WARNING - Current scene '{currentScene}' not found in container!");
        }

        DebugLog($"Returning scene data for {sceneDataContainer.sceneData.Count} total scenes");
        return new Dictionary<string, SceneSaveData>(sceneDataContainer.sceneData);
    }

    /// <summary>
    /// Get data for a specific scene (useful for debugging)
    /// </summary>
    public SceneSaveData GetSceneData(string sceneName)
    {
        return sceneDataContainer.GetSceneData(sceneName);
    }

    /// <summary>
    /// Check if we have data for a specific scene
    /// </summary>
    public bool HasSceneData(string sceneName)
    {
        return sceneDataContainer.sceneData.ContainsKey(sceneName);
    }

    /// <summary>
    /// Get list of all scenes we have data for
    /// </summary>
    public List<string> GetScenesWithData()
    {
        return new List<string>(sceneDataContainer.sceneData.Keys);
    }

    /// <summary>
    /// Remove data for a specific scene (useful for testing)
    /// </summary>
    public void RemoveSceneData(string sceneName)
    {
        if (sceneDataContainer.sceneData.ContainsKey(sceneName))
        {
            sceneDataContainer.sceneData.Remove(sceneName);
            DebugLog($"Removed scene data for: {sceneName}");
        }
        else
        {
            DebugLog($"No scene data to remove for: {sceneName}");
        }
    }

    /// <summary>
    /// Get total number of scenes with saved data
    /// </summary>
    public int GetSceneDataCount()
    {
        return sceneDataContainer.sceneData.Count;
    }

    /// <summary>
    /// Get memory usage information for debugging
    /// </summary>
    public string GetMemoryInfo()
    {
        int totalObjects = 0;
        foreach (var sceneData in sceneDataContainer.sceneData.Values)
        {
            totalObjects += sceneData.objectData.Count;
        }

        return $"Scenes: {sceneDataContainer.sceneData.Count}, Total Objects: {totalObjects}";
    }

    /// <summary>
    /// Export scene data summary for debugging
    /// </summary>
    public void LogSceneDataSummary()
    {
        DebugLog("=== SCENE DATA SUMMARY ===");

        if (sceneDataContainer.sceneData.Count == 0)
        {
            DebugLog("No scene data stored");
            return;
        }

        foreach (var kvp in sceneDataContainer.sceneData)
        {
            var sceneData = kvp.Value;
            DebugLog($"Scene: {kvp.Key}");
            DebugLog($"  Last Visited: {sceneData.lastVisited}");
            DebugLog($"  Has Been Visited: {sceneData.hasBeenVisited}");
            DebugLog($"  Objects: {sceneData.objectData.Count}");
            DebugLog($"  Flags: {sceneData.sceneFlags.Count}");
            DebugLog($"  Counters: {sceneData.sceneCounters.Count}");

            // Log object details
            foreach (var objKvp in sceneData.objectData)
            {
                DebugLog($"    - {objKvp.Key}: {objKvp.Value?.GetType().Name ?? "null"}");
            }
        }
    }

    /// <summary>
    /// Validate scene data integrity
    /// </summary>
    public bool ValidateSceneData()
    {
        bool isValid = true;

        foreach (var kvp in sceneDataContainer.sceneData)
        {
            var sceneData = kvp.Value;

            // Check if scene name matches key
            if (sceneData.sceneName != kvp.Key)
            {
                Debug.LogError($"Scene data mismatch: Key={kvp.Key}, Data.sceneName={sceneData.sceneName}");
                isValid = false;
            }

            // Check for null object data
            foreach (var objKvp in sceneData.objectData)
            {
                if (objKvp.Value == null)
                {
                    Debug.LogWarning($"Null object data found: Scene={kvp.Key}, Object={objKvp.Key}");
                }
            }
        }

        DebugLog($"Scene data validation: {(isValid ? "PASSED" : "FAILED")}");
        return isValid;
    }

    /// <summary>
    /// Create a backup of current scene data
    /// </summary>
    public Dictionary<string, SceneSaveData> CreateSceneDataBackup()
    {
        var backup = new Dictionary<string, SceneSaveData>();

        foreach (var kvp in sceneDataContainer.sceneData)
        {
            // Create a deep copy of the scene data
            var originalData = kvp.Value;
            var backupData = new SceneSaveData
            {
                sceneName = originalData.sceneName,
                lastVisited = originalData.lastVisited,
                hasBeenVisited = originalData.hasBeenVisited,
                objectData = new Dictionary<string, object>(originalData.objectData),
                sceneFlags = new Dictionary<string, bool>(originalData.sceneFlags),
                sceneCounters = new Dictionary<string, int>(originalData.sceneCounters)
            };

            backup[kvp.Key] = backupData;
        }

        DebugLog($"Created backup of {backup.Count} scenes");
        return backup;
    }

    /// <summary>
    /// Restore from a backup
    /// </summary>
    public void RestoreFromBackup(Dictionary<string, SceneSaveData> backup)
    {
        if (backup == null)
        {
            Debug.LogError("Cannot restore from null backup");
            return;
        }

        sceneDataContainer.sceneData = new Dictionary<string, SceneSaveData>(backup);
        DebugLog($"Restored from backup containing {backup.Count} scenes");
    }

    /// <summary>
    /// Manually force save of current scene (useful for testing)
    /// </summary>
    [Button("Force Save Current Scene"), ShowIf("showEditorDebugTools")]
    public void ForceSaveCurrentSceneDebug()
    {
        Debug.Log("=== FORCE SAVE DEBUG ===");
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Debug.Log($"Current scene: {currentScene}");

        SaveCurrentSceneData();

        // Immediately check what was saved
        var savedData = sceneDataContainer.GetSceneData(currentScene);
        if (savedData != null)
        {
            Debug.Log($"IMMEDIATE CHECK: Scene data contains {savedData.objectData.Count} objects");

            foreach (var kvp in savedData.objectData)
            {
                Debug.Log($"  - {kvp.Key}: {kvp.Value?.GetType().Name}");
            }
        }
        else
        {
            Debug.Log("IMMEDIATE CHECK: No scene data found!");
        }
    }

    [Button("Debug Scene Data Container"), ShowIf("showEditorDebugTools")]
    public void DebugSceneDataContainer()
    {
        Debug.Log("=== SCENE DATA CONTAINER DEBUG ===");
        Debug.Log($"Total scenes stored: {sceneDataContainer.sceneData.Count}");

        foreach (var kvp in sceneDataContainer.sceneData)
        {
            Debug.Log($"Scene: {kvp.Key} - Objects: {kvp.Value.objectData.Count} - Last Visited: {kvp.Value.lastVisited}");
        }
    }

    [Button("Log Scene Data Summary"), ShowIf("showEditorDebugTools")]
    private void EditorLogSceneDataSummary()
    {
        LogSceneDataSummary();
    }

    [Button("Validate Scene Data"), ShowIf("showEditorDebugTools")]
    private void EditorValidateSceneData()
    {
        ValidateSceneData();
    }

    [Button("Clear All Scene Data"), ShowIf("showEditorDebugTools")]
    private void EditorClearAllSceneData()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorUtility.DisplayDialog("Clear Scene Data",
            "Are you sure you want to clear all scene data? This cannot be undone.",
            "Yes", "Cancel"))
        {
            ClearAllSceneData();
        }
#else
        ClearAllSceneData();
#endif
    }

    [Button("Get Memory Info"), ShowIf("showEditorDebugTools")]
    private void EditorGetMemoryInfo()
    {
        string info = GetMemoryInfo();
        Debug.Log($"Scene Data Memory Info: {info}");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.DisplayDialog("Memory Info", info, "OK");
#endif
    }

    /// <summary>
    /// Called when the application is quitting to clean up resources
    /// </summary>
    private void OnApplicationQuit()
    {
        if (showDebugLogs)
        {
            DebugLog("Application quitting - scene data will be lost unless saved to file");
            LogSceneDataSummary();
        }
    }

    /// <summary>
    /// Cleanup when destroyed
    /// </summary>
    private void OnDestroy()
    {
        DebugLog("SceneDataManager destroyed");

        // Clean up any resources if needed
        if (sceneDataContainer != null)
        {
            sceneDataContainer.sceneData?.Clear();
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SceneDataManager] {message}");
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draw gizmos for scene data visualization
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showEditorDebugTools) return;

        // Draw scene data info in scene view
        var style = new UnityEngine.GUIStyle();
        style.normal.textColor = Color.yellow;
        style.fontSize = 12;

        string info = $"Scene Data Manager\nScenes: {GetSceneDataCount()}\n{GetMemoryInfo()}";
        UnityEditor.Handles.Label(transform.position, info, style);
    }
#endif
}