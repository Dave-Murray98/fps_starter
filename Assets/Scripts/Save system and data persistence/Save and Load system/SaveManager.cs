using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;


/// <summary>
/// SIMPLIFIED Save Manager
/// Only handles save files, delegates scene management to SceneDataManager
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Save Settings")]
    [SerializeField] private string saveFileName = "GameSave";
    [SerializeField] private bool showDebugLogs = true;

    private GameSaveData currentSaveData;

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

    private System.Collections.IEnumerator LoadGameCoroutine()
    {
        DebugLog("Starting load operation...");

        if (!LoadSaveDataFromFile())
        {
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        // Tell PlayerPersistenceManager that the savemanager is handling restoration of playerpersistent data
        // this prevents PlayerPersistenceManager from restoring data when SaveManager is already doing it
        if (PlayerPersistenceManager.Instance != null && currentSaveData.playerPersistentData != null)
        {
            PlayerPersistenceManager.Instance.LoadPersistentDataFromSave(currentSaveData.playerPersistentData);
        }

        //load the scene dependent data for the scene were loading into
        if (SceneDataManager.Instance != null && currentSaveData.sceneData != null)
        {
            SceneDataManager.Instance.LoadSceneDataFromSave(currentSaveData.sceneData);
        }

        // Load scene if different
        string targetScene = currentSaveData.currentScene;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        //if we're loading into a different scene, wait for the scene to finish loading before restoring save data
        if (targetScene != currentScene)
        {
            // Different scene - let SceneTransitionManager handle loading screen
            SceneTransitionManager.Instance.LoadSceneFromSave(targetScene);

            // Wait for scene to load
            yield return new WaitUntil(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == targetScene);

            yield return new WaitForSecondsRealtime(0.2f);

            RestoreAllDataInScene();
        }
        else // if we're loading into the same scene, we can restore data immediately
        {
            // Same scene - show our own loading screen
            LoadingScreenManager.Instance?.ShowLoadingScreenForSaveLoad(currentScene);

            yield return new WaitForSecondsRealtime(0.2f);
            LoadingScreenManager.Instance?.SetProgress(0.3f);

            yield return new WaitForSecondsRealtime(0.01f);
            RestoreAllDataInScene();


            LoadingScreenManager.Instance?.SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.5f);
            LoadingScreenManager.Instance?.HideLoadingScreen();
        }

        // Ensure game is unpaused after loading
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            GameManager.Instance.ResumeGame();
            //DebugLog("Game unpaused after load");
        }

        //close the inventory panel (we had to open it to restore inventory)
        if (GameManager.Instance?.uiManager?.inventoryPanel != null)
        {
            GameManager.Instance.uiManager.inventoryPanel.SetActive(false);
            GameManager.Instance.uiManager.isInventoryOpen = false;
        }

        DebugLog("Game loaded successfully");
        OnLoadComplete?.Invoke(true);

        // Tell PlayerPersistenceManager we're done
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.OnSaveLoadComplete();
        }
    }

    /// <summary>
    /// Restores all data in the current scene after loading a save file.
    /// </summary>
    private void RestoreAllDataInScene()
    {
        DebugLog("Restoring all data after scene load...");
        RestorePlayerData();
        RestoreSceneData();
    }

    private void RestorePlayerData()
    {
        if (currentSaveData?.playerPersistentData == null) return;

        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
           .OfType<ISaveable>()
           .Where(s => s.SaveCategory == SaveDataCategory.PlayerDependent)
           .ToArray();

        //Debug.Log($"Restoring {saveableObjects.Length} player-dependent saveables after scene load");

        foreach (var saveable in saveableObjects)
        {
            try
            {
                //from the currentsaveData's playersaveData, extract the relevant data for this saveable - for playersavecomponent, this will return the playerSaveData (which is a PlayerSaveData class), for other saveables, it will return the relevant data for that saveable
                //ie for the inventorysavecomponent, it will extract the playerSaveData.inventoryData (which is an InventorySaveData class)
                var data = saveable.ExtractRelevantData(currentSaveData.playersaveData);
                if (data != null)
                {
                    //pass the relevant extracted data to the saveable (each saveable's LoadSaveData method will know how to handle it) and will check that it's the correct type
                    saveable.LoadSaveData(data);
                    saveable.OnAfterLoad();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to restore {saveable.SaveID} after scene load: {e.Message}");
            }
        }

        //UPDATE UI
        if (GameManager.Instance?.uiManager != null)
        {
            GameManager.Instance.uiManager.RefreshReferences();
        }
    }



    private void RestoreSceneData()
    {
        if (currentSaveData?.sceneData == null) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!currentSaveData.sceneData.ContainsKey(currentScene)) return;

        var sceneData = currentSaveData.sceneData[currentScene];

        ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .OfType<ISaveable>()
            .Where(s => s.SaveCategory == SaveDataCategory.SceneDependent)
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
                Debug.LogError($"Failed to restore {saveable.SaveID} after scene load: {e.Message}");
            }
        }

        DebugLog($"Restored scene data after scene load: {currentScene}");
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

    private void RestorePlayerPositionData()
    {
        if (currentSaveData?.playerPositionData == null) return;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            player.transform.position = currentSaveData.playerPositionData.position;
            player.transform.eulerAngles = currentSaveData.playerPositionData.rotation;
            DebugLog($"Player position restored from save: {currentSaveData.playerPositionData.position}");
        }
    }

    public bool SaveExists()
    {
        return ES3.FileExists(saveFileName + ".es3");
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] {message}");
        }
    }
}