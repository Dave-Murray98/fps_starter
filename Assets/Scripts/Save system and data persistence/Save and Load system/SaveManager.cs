using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// FIXED: SaveManager with corrected load logic for modular save system
/// The issue was in how we prepare save data for SceneTransitionManager
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveFileName = "GameSave";
    [SerializeField] private bool showDebugLogs = true;

    [ShowInInspector] private GameSaveData currentSaveData;

    // Events
    public System.Action<bool> OnSaveComplete;
    public System.Action<bool> OnLoadComplete;

    [Header("Resources Data Paths")]
    public string itemDataPath = "Data/Items/";

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

    public void SaveGame()
    {
        StartCoroutine(SaveGameCoroutine());
    }

    public void LoadGame()
    {
        StartCoroutine(LoadGameCoroutine());
    }

    private System.Collections.IEnumerator SaveGameCoroutine()
    {
        DebugLog("Starting save operation...");

        // FORCE IMMEDIATE SCENE DATA COLLECTION FIRST (before any UI updates)
        if (SceneDataManager.Instance != null)
        {
            DebugLog("IMMEDIATE: Forcing current scene data save");
            SceneDataManager.Instance.SaveCurrentSceneData(); // This calls the private method directly
            DebugLog("IMMEDIATE: Current scene data forced - checking container...");

            // Debug: Check if data is immediately available
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var immediateCheck = SceneDataManager.Instance.GetSceneDataForSaving();
            if (immediateCheck.ContainsKey(currentScene))
            {
                DebugLog($"IMMEDIATE: Scene data confirmed in container - {immediateCheck[currentScene].objectData.Count} objects");
            }
            else
            {
                DebugLog("IMMEDIATE: WARNING - Scene data not found in container!");
            }
        }
        else
        {
            DebugLog("SceneDataManager not found - scene data will not be saved!");
        }

        // NOW start the UI and timing stuff
        LoadingScreenManager.Instance?.ShowLoadingScreen("Saving Game...", "Please wait");
        LoadingScreenManager.Instance?.SetProgress(0.1f);
        yield return new WaitForSecondsRealtime(0.1f);

        currentSaveData = new GameSaveData();
        currentSaveData.saveTime = System.DateTime.Now;
        currentSaveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        LoadingScreenManager.Instance?.SetProgress(0.3f);

        // Save player persistent data to the main save file
        if (PlayerPersistenceManager.Instance != null)
        {
            currentSaveData.playerPersistentData = PlayerPersistenceManager.Instance.GetPersistentDataForSave();
            SavePlayerPositionData();
            currentSaveData.SetPlayerSaveDataToPlayerPersistentData();

            // DEBUGGING: Log what we're about to save
            DebugLog($"Saving player data: Health={currentSaveData.playerPersistentData.currentHealth}");
            DebugLog($"Player persistent component data: {currentSaveData.playerPersistentData.ComponentDataCount} entries");
            DebugLog($"Player save custom data: {currentSaveData.playersaveData?.CustomDataCount ?? 0} entries");
        }

        LoadingScreenManager.Instance?.SetProgress(0.5f);

        // Get scene data (should already be collected above)
        if (SceneDataManager.Instance != null)
        {
            currentSaveData.sceneData = SceneDataManager.Instance.GetSceneDataForSaving();
            DebugLog($"Final scene data collection: {currentSaveData.sceneData.Count} scenes");
        }

        LoadingScreenManager.Instance?.SetProgress(0.7f);
        LoadingScreenManager.Instance?.SetProgress(0.9f);

        // Save to file
        bool saveSuccess = false;
        try
        {
            ES3.Save("gameData", currentSaveData, saveFileName + ".es3");
            saveSuccess = true;
            DebugLog("Game saved successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
            saveSuccess = false;
        }

        LoadingScreenManager.Instance?.SetProgress(1f);
        yield return new WaitForSecondsRealtime(0.3f);

        OnSaveComplete?.Invoke(saveSuccess);
        LoadingScreenManager.Instance?.HideLoadingScreen();
    }

    /// <summary>
    /// FIXED: LoadGame now properly converts save data for SceneTransitionManager
    /// The issue was in the data preparation - we need to rebuild PlayerPersistentData from the saved data
    /// </summary>
    private System.Collections.IEnumerator LoadGameCoroutine()
    {
        DebugLog("Starting load operation...");

        if (!LoadSaveDataFromFile())
        {
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        // FIXED: Prepare save data properly for SceneTransitionManager
        var saveDataForTransition = PrepareSaveDataForTransition();

        // Get target scene from save data
        string targetScene = currentSaveData.currentScene;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        DebugLog($"Load target: {targetScene}, current: {currentScene}");

        // DELEGATE TO SCENETRANSITIONMANAGER: Let it handle everything from here
        if (SceneTransitionManager.Instance != null)
        {
            // SceneTransitionManager will handle the loading screen, scene transition, and data restoration
            SceneTransitionManager.Instance.LoadSceneFromSave(targetScene, saveDataForTransition);

            // Wait for the transition to complete
            yield return new WaitUntil(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == targetScene);

            // Wait a bit more for data restoration to complete
            yield return new WaitForSecondsRealtime(0.5f);
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found - cannot load game!");
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        DebugLog("Game load operation completed");
        OnLoadComplete?.Invoke(true);
    }

    /// <summary>
    /// FIXED: Prepare save data in a format that SceneTransitionManager can use for restoration
    /// The key fix: We need to rebuild PlayerPersistentData from the custom data in PlayerSaveData
    /// </summary>
    private Dictionary<string, object> PrepareSaveDataForTransition()
    {
        var transitionData = new Dictionary<string, object>();

        // CRITICAL FIX: Rebuild PlayerPersistentData from the loaded save data
        if (currentSaveData.playersaveData != null)
        {
            DebugLog("REBUILDING PlayerPersistentData from loaded save data...");

            // Create new PlayerPersistentData
            var rebuiltPersistentData = new PlayerPersistentData();

            // Copy basic stats
            rebuiltPersistentData.currentHealth = currentSaveData.playersaveData.currentHealth;
            rebuiltPersistentData.canJump = currentSaveData.playersaveData.canJump;
            rebuiltPersistentData.canSprint = currentSaveData.playersaveData.canSprint;
            rebuiltPersistentData.canCrouch = currentSaveData.playersaveData.canCrouch;

            // CRITICAL: Copy all component data from PlayerSaveData.customStats to PlayerPersistentData.componentData
            foreach (string componentKey in currentSaveData.playersaveData.GetCustomDataKeys())
            {
                var componentData = currentSaveData.playersaveData.GetCustomData<object>(componentKey);
                if (componentData != null)
                {
                    rebuiltPersistentData.SetComponentData(componentKey, componentData);
                    DebugLog($"Rebuilt component data for {componentKey}: {componentData.GetType().Name}");
                }
            }

            DebugLog($"Rebuilt PlayerPersistentData with {rebuiltPersistentData.ComponentDataCount} component entries");
            transitionData["playerPersistentData"] = rebuiltPersistentData;
        }

        // Add direct PlayerSaveData (contains position info)
        if (currentSaveData.playersaveData != null)
        {
            transitionData["playerSaveData"] = currentSaveData.playersaveData;
            DebugLog($"Added PlayerSaveData with {currentSaveData.playersaveData.CustomDataCount} custom data entries");
        }

        // Add position data
        if (currentSaveData.playerPositionData != null)
        {
            transitionData["playerPositionData"] = currentSaveData.playerPositionData;
        }

        // Add scene data
        if (currentSaveData.sceneData != null)
        {
            transitionData["sceneData"] = currentSaveData.sceneData;
        }

        DebugLog($"Prepared transition data with {transitionData.Count} data categories");
        return transitionData;
    }

    private bool LoadSaveDataFromFile()
    {
        if (!ES3.FileExists(saveFileName + ".es3"))
        {
            DebugLog("No save file found");
            return false;
        }

        try
        {
            currentSaveData = ES3.Load<GameSaveData>("gameData", saveFileName + ".es3");

            // DEBUGGING: Log what we loaded
            DebugLog($"Save file loaded successfully - Scene: {currentSaveData.currentScene}");
            DebugLog($"Loaded player health: {currentSaveData.playersaveData?.currentHealth ?? 0}");
            DebugLog($"Loaded custom data entries: {currentSaveData.playersaveData?.CustomDataCount ?? 0}");

            if (currentSaveData.playersaveData != null)
            {
                foreach (string key in currentSaveData.playersaveData.GetCustomDataKeys())
                {
                    var data = currentSaveData.playersaveData.GetCustomData<object>(key);
                    DebugLog($"  Loaded custom data - {key}: {data?.GetType().Name ?? "null"}");
                }
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Load failed: {e.Message}");
            return false;
        }
    }

    private void SavePlayerPositionData()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            if (currentSaveData.playerPositionData == null)
                currentSaveData.playerPositionData = new PlayerPositionData();

            currentSaveData.playerPositionData.position = player.transform.position;
            currentSaveData.playerPositionData.rotation = player.transform.eulerAngles;

            DebugLog($"Saved player position: {currentSaveData.playerPositionData.position}");
        }
    }

    public bool SaveExists()
    {
        return ES3.FileExists(saveFileName + ".es3");
    }

    /// <summary>
    /// Get save file info for UI display (without loading the full save)
    /// </summary>
    public SaveFileInfo GetSaveFileInfo()
    {
        if (!SaveExists()) return null;

        try
        {
            // Load only the metadata we need for display
            var saveTime = ES3.Load<System.DateTime>("gameData.saveTime", saveFileName + ".es3");
            var sceneName = ES3.Load<string>("gameData.currentScene", saveFileName + ".es3");

            // Try to get player level if it exists
            int playerLevel = 1;
            if (ES3.KeyExists("gameData.playersaveData.level", saveFileName + ".es3"))
            {
                playerLevel = ES3.Load<int>("gameData.playersaveData.level", saveFileName + ".es3");
            }

            return new SaveFileInfo
            {
                saveTime = saveTime,
                sceneName = sceneName,
                playerLevel = playerLevel,
                playTime = 0f // Could calculate this if we track play time
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load save file info: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Delete the current save file
    /// </summary>
    public bool DeleteSaveFile()
    {
        try
        {
            if (ES3.FileExists(saveFileName + ".es3"))
            {
                ES3.DeleteFile(saveFileName + ".es3");
                DebugLog("Save file deleted successfully");
                return true;
            }
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to delete save file: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create a backup of the current save file
    /// </summary>
    public bool BackupSaveFile()
    {
        try
        {
            if (ES3.FileExists(saveFileName + ".es3"))
            {
                string backupName = saveFileName + "_backup.es3";
                ES3.CopyFile(saveFileName + ".es3", backupName);
                DebugLog("Save file backed up successfully");
                return true;
            }
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to backup save file: {e.Message}");
            return false;
        }
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] {message}");
        }
    }
}