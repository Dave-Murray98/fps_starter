using UnityEngine;
using System.Collections;
using System.Linq;


/// <summary>
/// Defines the context in which save data is being collected
/// This allows different behavior based on the situation
/// </summary>
public enum SaveContext
{
    GameSave,           // Full game save - save everything including player position
    SceneTransition,    // Scene change via portal - save scene objects but not player position
    AutoSave            // Auto-save - save everything
}

/// <summary>
/// Main save/load system manager
/// Handles all save operations and coordinates with EasySave3
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // Add this flag to track application quitting
    private static bool isQuitting = false;

    [Header("Save Settings")]
    [SerializeField] private bool enableAutoSave = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
    [SerializeField] private string saveFileName = "GameSave";
    [SerializeField] private bool encryptSaves = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private GameSaveData currentGameData;

    // Events
    public System.Action<bool> OnSaveComplete; // bool: success
    public System.Action<bool> OnLoadComplete; // bool: success
    public System.Action OnAutoSave;

    // Private variables
    private bool isInitialized = false;
    private float timeSinceLastSave = 0f;
    private Coroutine autoSaveCoroutine;

    // Properties
    public bool HasSaveFile => ES3.FileExists(saveFileName + ".es3");
    public GameSaveData CurrentGameData => currentGameData;
    public bool IsInitialized => isInitialized;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Initialize save system
        currentGameData = new GameSaveData();

        // Start auto-save if enabled
        if (enableAutoSave)
        {
            StartAutoSave();
        }

        // Subscribe to scene changes
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        isInitialized = true;
        DebugLog("SaveManager initialized");
    }

    private void Update()
    {
        if (enableAutoSave)
        {
            timeSinceLastSave += Time.unscaledDeltaTime;
        }
    }

    #region Public Save/Load Methods

    /// <summary>
    /// Save the current game state
    /// </summary>
    public void SaveGame()
    {
        StartCoroutine(SaveGameCoroutine());
    }

    /// <summary>
    /// Load the saved game
    /// </summary>
    public void LoadGame()
    {
        StartCoroutine(LoadGameCoroutine());
    }

    /// <summary>
    /// Load game and transition to saved scene if needed
    /// </summary>
    public void LoadGameWithSceneTransition()
    {
        if (!SaveExists())
        {
            Debug.LogWarning("No save file found");
            OnLoadComplete?.Invoke(false);
            return;
        }

        StartCoroutine(LoadGameWithSceneTransitionCoroutine());
    }

    /// <summary>
    /// Start a new game with fresh data
    /// </summary>
    public void NewGame()
    {
        currentGameData = new GameSaveData();
        currentGameData.saveTime = System.DateTime.Now;
        currentGameData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Initialize default player data
        InitializeDefaultPlayerData();

        DebugLog("New game started");
    }

    /// <summary>
    /// Delete the save file
    /// </summary>
    public void DeleteSave()
    {
        if (ES3.FileExists(saveFileName + ".es3"))
        {
            ES3.DeleteFile(saveFileName + ".es3");
            DebugLog("Save file deleted");
        }
    }

    /// <summary>
    /// Check if a save file exists
    /// </summary>
    public bool SaveExists()
    {
        return ES3.FileExists(saveFileName + ".es3");
    }

    /// <summary>
    /// Get save file info (for UI display)
    /// </summary>
    public SaveFileInfo GetSaveFileInfo()
    {
        if (!SaveExists()) return null;

        try
        {
            var saveTime = ES3.Load<System.DateTime>("saveTime", saveFileName + ".es3");
            var currentScene = ES3.Load<string>("currentScene", saveFileName + ".es3");
            var totalPlayTime = ES3.Load<float>("totalPlayTime", saveFileName + ".es3");
            var playerLevel = ES3.Load<int>("playerData.level", saveFileName + ".es3");

            return new SaveFileInfo
            {
                saveTime = saveTime,
                sceneName = currentScene,
                playTime = totalPlayTime,
                playerLevel = playerLevel
            };
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to get save file info: {e.Message}");
            return null;
        }
    }

    #endregion

    #region Auto-Save System

    private void StartAutoSave()
    {
        if (autoSaveCoroutine != null)
        {
            StopCoroutine(autoSaveCoroutine);
        }
        autoSaveCoroutine = StartCoroutine(AutoSaveCoroutine());
    }

    private void StopAutoSave()
    {
        if (autoSaveCoroutine != null)
        {
            StopCoroutine(autoSaveCoroutine);
            autoSaveCoroutine = null;
        }
    }

    private IEnumerator AutoSaveCoroutine()
    {
        while (enableAutoSave)
        {
            yield return new WaitForSeconds(autoSaveInterval);

            // Only auto-save if we're in a gameplay state (not in menus)
            if (CanAutoSave())
            {
                DebugLog("Auto-saving...");
                yield return SaveGameCoroutine(SaveContext.AutoSave);
                OnAutoSave?.Invoke();
            }
        }
    }

    private bool CanAutoSave()
    {
        // Add conditions for when auto-save should NOT happen
        // e.g., during cutscenes, in menus, during loading, etc.
        return GameManager.Instance != null && !GameManager.Instance.isPaused;
    }

    #endregion

    #region Save/Load Implementation

    private IEnumerator LoadGameWithSceneTransitionCoroutine()
    {
        DebugLog("Starting load game with scene transition...");

        // Load save data to check target scene
        GameSaveData saveData = null;

        try
        {
            saveData = ES3.Load<GameSaveData>("gameData", saveFileName + ".es3");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load save data: {e.Message}");
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        if (saveData == null)
        {
            Debug.LogError("Save data is null");
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string targetScene = saveData.currentScene;

        DebugLog($"Current scene: {currentScene}, Target scene: {targetScene}");

        if (!string.IsNullOrEmpty(targetScene) && targetScene != currentScene)
        {
            // Need to transition to different scene
            DebugLog($"Transitioning to saved scene: {targetScene}");

            if (SceneTransitionManager.Instance != null)
            {
                // Use SceneTransitionManager for proper scene loading
                SceneTransitionManager.Instance.LoadSceneFromSave(targetScene);
            }
            else
            {
                // Fallback to direct scene loading
                DebugLog("SceneTransitionManager not found, using direct scene load");

                // Set persistent data for scene management
                ScenePersistenceManager.Instance.SetPersistentData(saveData);
                ScenePersistenceManager.Instance.PrepareSceneChange(targetScene, true, SaveContext.GameSave);

                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
            }
        }
        else
        {
            // Same scene, just load data normally
            DebugLog("Loading data in current scene");
            yield return StartCoroutine(LoadGameCoroutine());
        }
    }

    private IEnumerator SaveGameCoroutine(SaveContext saveContext = SaveContext.GameSave)
    {
        DebugLog($"Starting save operation with context: {saveContext}...");
        bool success = false;

        // Update save metadata
        currentGameData.saveTime = System.DateTime.Now;
        currentGameData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        currentGameData.totalPlayTime += timeSinceLastSave;

        // Collect player data
        bool playerDataSuccess = CollectPlayerData();
        if (!playerDataSuccess)
        {
            DebugLog("Failed to collect player data");
            OnSaveComplete?.Invoke(false);
            yield break;
        }

        // Collect current scene data
        bool sceneDataSuccess = CollectCurrentSceneData();
        if (!sceneDataSuccess)
        {
            DebugLog("Failed to collect scene data");
            OnSaveComplete?.Invoke(false);
            yield break;
        }

        // Save to file using EasySave3
        yield return StartCoroutine(SaveToFile());

        // Check if save was successful by verifying the file exists
        if (ES3.FileExists(saveFileName + ".es3"))
        {
            timeSinceLastSave = 0f;
            success = true;
            DebugLog("Save completed successfully");
        }
        else
        {
            DebugLog("Save failed - file was not created");
            success = false;
        }

        OnSaveComplete?.Invoke(success);
    }

    private IEnumerator LoadGameCoroutine()
    {
        DebugLog("Starting load operation...");
        bool success = false;

        if (!SaveExists())
        {
            Debug.LogWarning("No save file found");
            OnLoadComplete?.Invoke(false);
            yield break;
        }

        // Load from file using EasySave3
        yield return StartCoroutine(LoadFromFile());

        // Check if load was successful
        if (currentGameData != null)
        {
            // Set persistent data for scene management
            ScenePersistenceManager.Instance.SetPersistentData(currentGameData);

            // Load the saved scene
            string targetScene = currentGameData.currentScene;
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            if (!string.IsNullOrEmpty(targetScene))
            {
                ScenePersistenceManager.Instance.PrepareSceneChange(targetScene, true, SaveContext.GameSave);

                // Only load scene if it's different from current scene
                if (targetScene != currentScene)
                {
                    DebugLog($"Loading scene: {targetScene}");
                    UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
                }
                else
                {
                    // Same scene, just restore data directly
                    DebugLog("Same scene detected, restoring data in current scene");
                    yield return new WaitForEndOfFrame(); // Wait for frame to ensure everything is ready
                    ScenePersistenceManager.Instance.OnSceneLoaded(currentScene);
                }
            }

            success = true;
            DebugLog("Load completed successfully");
        }
        else
        {
            DebugLog("Load failed - currentGameData is null");
            success = false;
        }

        OnLoadComplete?.Invoke(success);
    }

    private IEnumerator SaveToFile()
    {
        // EasySave3 can handle this synchronously, but we yield for UI responsiveness
        yield return null;

        try
        {
            ES3.Save("gameData", currentGameData, saveFileName + ".es3");

            // Also save individual key data for quick access
            ES3.Save("saveTime", currentGameData.saveTime, saveFileName + ".es3");
            ES3.Save("currentScene", currentGameData.currentScene, saveFileName + ".es3");
            ES3.Save("totalPlayTime", currentGameData.totalPlayTime, saveFileName + ".es3");
            ES3.Save("playerData.level", currentGameData.playerData.level, saveFileName + ".es3");

            DebugLog("Data saved to file successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save to file: {e.Message}");
        }
    }

    private IEnumerator LoadFromFile()
    {
        yield return null;

        try
        {
            currentGameData = ES3.Load<GameSaveData>("gameData", saveFileName + ".es3");

            // Validate loaded data
            if (currentGameData == null)
            {
                Debug.LogError("Loaded save data is null");
                currentGameData = new GameSaveData(); // Create fallback
            }
            else
            {
                DebugLog("Data loaded from file successfully");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load from file: {e.Message}");
            currentGameData = null;
        }
    }

    #endregion

    #region Data Collection

    private bool CollectPlayerData()
    {
        try
        {
            // Find PlayerSaveComponent instead of PlayerController directly
            var playerSaveComponent = FindFirstObjectByType<PlayerSaveComponent>();
            if (playerSaveComponent != null)
            {
                playerSaveComponent.OnBeforeSave();
                var playerData = playerSaveComponent.GetSaveData() as PlayerSaveData;
                if (playerData != null)
                {
                    currentGameData.playerData = playerData;
                    DebugLog("Player data collected successfully");
                    return true;
                }
                else
                {
                    Debug.LogWarning("Player save component returned null data");
                    return false;
                }
            }
            else
            {
                Debug.LogWarning("PlayerSaveComponent not found in scene");
                return false;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to collect player data: {e.Message}");
            return false;
        }
    }

    private bool CollectCurrentSceneData()
    {
        try
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Create or get scene data
            if (!currentGameData.sceneData.ContainsKey(currentScene))
            {
                currentGameData.sceneData[currentScene] = new SceneSaveData();
            }

            var sceneData = currentGameData.sceneData[currentScene];
            sceneData.sceneName = currentScene;
            sceneData.lastVisited = System.DateTime.Now;
            sceneData.hasBeenVisited = true;

            // Collect from all ISaveable objects (excluding PlayerSaveComponent as it's handled separately)
            ISaveable[] saveableObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISaveable>().ToArray();

            int savedObjectCount = 0;
            foreach (var saveable in saveableObjects)
            {
                // Skip PlayerSaveComponent as it's handled separately
                if (saveable is PlayerSaveComponent) continue;

                try
                {
                    saveable.OnBeforeSave();
                    var data = saveable.GetSaveData();
                    if (data != null)
                    {
                        sceneData.SetObjectData(saveable.SaveID, data);
                        savedObjectCount++;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to save data for {saveable.SaveID}: {e.Message}");
                }
            }

            DebugLog($"Scene data collected: {savedObjectCount} objects saved in scene '{currentScene}'");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to collect scene data: {e.Message}");
            return false;
        }
    }

    #endregion

    #region Initialization & Utilities

    private void InitializeDefaultPlayerData()
    {
        currentGameData.playerData = new PlayerSaveData
        {
            health = 100f,
            maxHealth = 100f,
            level = 1,
            experience = 0f,
            position = Vector3.zero,
            currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        ScenePersistenceManager.Instance.OnSceneLoaded(scene.name);
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[SaveManager] {message}");
        }
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    /// Manually trigger auto-save
    /// </summary>
    public void TriggerAutoSave()
    {
        if (CanAutoSave())
        {
            StartCoroutine(SaveGameCoroutine(SaveContext.AutoSave));
        }
    }

    /// <summary>
    /// Enable/disable auto-save
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        enableAutoSave = enabled;
        if (enabled)
        {
            StartAutoSave();
        }
        else
        {
            StopAutoSave();
        }
    }

    /// <summary>
    /// Set auto-save interval
    /// </summary>
    public void SetAutoSaveInterval(float minutes)
    {
        autoSaveInterval = minutes * 60f;
        if (enableAutoSave)
        {
            StartAutoSave(); // Restart with new interval
        }
    }

    /// <summary>
    /// Get current play time
    /// </summary>
    public float GetTotalPlayTime()
    {
        return currentGameData.totalPlayTime + timeSinceLastSave;
    }

    #endregion

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        StopAutoSave();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }
}