using UnityEngine;
using System.Collections.Generic;
using System.Linq;


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

        currentSaveData = new GameSaveData();
        currentSaveData.saveTime = System.DateTime.Now;
        currentSaveData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Save player persistent data
        if (PlayerPersistenceManager.Instance != null)
        {
            currentSaveData.playerPersistentData = PlayerPersistenceManager.Instance.GetPersistentDataForSave();
        }

        // Save scene data
        if (SceneDataManager.Instance != null)
        {
            currentSaveData.sceneData = SceneDataManager.Instance.GetSceneDataForSaving();
        }

        // Save player position data (for save/load, not doorway transitions)
        SavePlayerPositionData();

        // Save to file
        try
        {
            ES3.Save("gameData", currentSaveData, saveFileName + ".es3");
            DebugLog("Game saved successfully");
            OnSaveComplete?.Invoke(true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Save failed: {e.Message}");
            OnSaveComplete?.Invoke(false);
        }

        yield return null;
    }

    private System.Collections.IEnumerator LoadGameCoroutine()
    {
        DebugLog("Starting load operation...");

        if (!LoadSaveDataFromFile())
        {
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        // FIX: Tell PlayerPersistenceManager that we're handling restoration
        if (PlayerPersistenceManager.Instance != null && currentSaveData.playerPersistentData != null)
        {
            PlayerPersistenceManager.Instance.LoadPersistentDataFromSave(currentSaveData.playerPersistentData);
        }

        if (SceneDataManager.Instance != null && currentSaveData.sceneData != null)
        {
            SceneDataManager.Instance.LoadSceneDataFromSave(currentSaveData.sceneData);
        }

        // Load scene if different
        string targetScene = currentSaveData.currentScene;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (targetScene != currentScene)
        {
            // Different scene - transition and then restore
            SceneTransitionManager.Instance.LoadSceneFromSave(targetScene);

            // Wait for scene to load, then restore everything
            yield return new WaitUntil(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == targetScene);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.2f); // Give scene time to initialize

            RestoreAllDataAfterSceneLoad();
        }
        else
        {
            // Same scene - restore everything immediately
            yield return new WaitForEndOfFrame();
            RestoreAllDataInSameScene();
        }

        // FIX: Ensure game is unpaused after loading
        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            GameManager.Instance.ResumeGame();
            DebugLog("Game unpaused after load");
        }

        DebugLog("Game loaded successfully");
        OnLoadComplete?.Invoke(true);

        // FIX: Tell PlayerPersistenceManager we're done
        if (PlayerPersistenceManager.Instance != null)
        {
            PlayerPersistenceManager.Instance.SaveManagerRestorationComplete();
        }
    }

    // FIX: Combined restoration method for different scene loads
    private void RestoreAllDataAfterSceneLoad()
    {
        DebugLog("Restoring all data after scene load...");

        // Restore player position first
        RestorePlayerPositionData();

        // Restore player data
        RestorePlayerDataAfterSceneLoad();

        // Restore scene data
        RestoreSceneDataAfterSceneLoad();
    }

    // FIX: Combined restoration method for same scene loads
    private void RestoreAllDataInSameScene()
    {
        DebugLog("Restoring all data in same scene...");

        // Restore player position
        RestorePlayerPositionData();

        // Restore player data
        RestorePlayerDataInSameScene();

        // Restore scene data
        RestoreSceneDataInSameScene();
    }

    // FIX: Restore player data after scene load (different scene)
    private void RestorePlayerDataAfterSceneLoad()
    {
        if (currentSaveData?.playerPersistentData == null) return;

        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager != null)
        {
            playerManager.currentHealth = currentSaveData.playerPersistentData.currentHealth;

            // Trigger UI update
            if (GameManager.Instance?.playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(playerManager.currentHealth, GameManager.Instance.playerData.maxHealth);
            }

            DebugLog($"Restored player health after scene load: {playerManager.currentHealth}");
        }

        if (playerController != null)
        {
            playerController.canJump = currentSaveData.playerPersistentData.canJump;
            playerController.canSprint = currentSaveData.playerPersistentData.canSprint;
            playerController.canCrouch = currentSaveData.playerPersistentData.canCrouch;
        }
    }

    // Restore player data in same scene (existing method)
    private void RestorePlayerDataInSameScene()
    {
        if (currentSaveData?.playerPersistentData == null) return;

        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager != null)
        {
            playerManager.currentHealth = currentSaveData.playerPersistentData.currentHealth;

            // Trigger UI update
            if (GameManager.Instance?.playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(playerManager.currentHealth, GameManager.Instance.playerData.maxHealth);
            }

            DebugLog($"Restored player health in same scene: {playerManager.currentHealth}");
        }

        if (playerController != null)
        {
            playerController.canJump = currentSaveData.playerPersistentData.canJump;
            playerController.canSprint = currentSaveData.playerPersistentData.canSprint;
            playerController.canCrouch = currentSaveData.playerPersistentData.canCrouch;
        }
    }

    // FIX: Restore scene data after scene load (different scene)
    private void RestoreSceneDataAfterSceneLoad()
    {
        if (currentSaveData?.sceneData == null) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!currentSaveData.sceneData.ContainsKey(currentScene)) return;

        var sceneData = currentSaveData.sceneData[currentScene];

        // Find all saveable objects except player-related ones
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
                Debug.LogError($"Failed to restore {saveable.SaveID} after scene load: {e.Message}");
            }
        }

        DebugLog($"Restored scene data after scene load: {currentScene}");
    }

    // Restore scene data in same scene (existing method)
    private void RestoreSceneDataInSameScene()
    {
        if (currentSaveData?.sceneData == null) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!currentSaveData.sceneData.ContainsKey(currentScene)) return;

        var sceneData = currentSaveData.sceneData[currentScene];

        // Find all saveable objects except player-related ones
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
                Debug.LogError($"Failed to restore {saveable.SaveID} in same scene: {e.Message}");
            }
        }

        DebugLog($"Restored scene data in same scene: {currentScene}");
    }

    private bool IsPlayerRelatedComponent(ISaveable saveable)
    {
        return saveable.SaveID.Contains("Player") || saveable.SaveID.Contains("player");
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