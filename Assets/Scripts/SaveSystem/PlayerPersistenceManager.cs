using UnityEngine;

/// <summary>
/// Manages player data that should persist between scenes (via doorways)
/// This is separate from save files - it's for doorway transitions
/// </summary>
public class PlayerPersistenceManager : MonoBehaviour
{
    public static PlayerPersistenceManager Instance { get; private set; }

    [Header("Persistent Player Data")]
    [SerializeField] private PlayerPersistentData persistentData;
    [SerializeField] private bool showDebugLogs = true;

    // Flag to track if we have persistent data to restore
    private bool hasPersistentData = false;

    // FIX: Flag to prevent restoration when SaveManager is handling it
    private bool saveManagerIsHandlingRestore = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            persistentData = new PlayerPersistentData();
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
    /// Save current player data before scene transition (called by doorway)
    /// </summary>
    public void SavePlayerDataForTransition()
    {
        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager != null && playerController != null)
        {
            persistentData.currentHealth = playerManager.currentHealth;
            persistentData.canJump = playerController.canJump;
            persistentData.canSprint = playerController.canSprint;
            persistentData.canCrouch = playerController.canCrouch;

            hasPersistentData = true;
            DebugLog($"Player data saved for doorway transition: Health={persistentData.currentHealth}");
        }
    }

    /// <summary>
    /// Restore player data after scene transition (called automatically for doorways only)
    /// </summary>
    private void RestorePlayerDataAfterTransition()
    {
        if (!hasPersistentData || saveManagerIsHandlingRestore) return;

        var playerManager = FindFirstObjectByType<PlayerManager>();
        var playerController = FindFirstObjectByType<PlayerController>();

        if (playerManager != null && playerController != null)
        {
            playerManager.currentHealth = persistentData.currentHealth;
            playerController.canJump = persistentData.canJump;
            playerController.canSprint = persistentData.canSprint;
            playerController.canCrouch = persistentData.canCrouch;

            // Trigger UI updates
            if (GameManager.Instance?.playerData != null)
            {
                GameEvents.TriggerPlayerHealthChanged(persistentData.currentHealth, GameManager.Instance.playerData.maxHealth);
            }

            DebugLog($"Player data restored after doorway transition: Health={persistentData.currentHealth}");
        }
    }

    /// <summary>
    /// Called when scene loads to restore player data if needed
    /// FIX: Only restore for doorway transitions, never for save loads
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (hasPersistentData && !saveManagerIsHandlingRestore)
        {
            StartCoroutine(RestorePlayerDataCoroutine());
        }
    }

    private System.Collections.IEnumerator RestorePlayerDataCoroutine()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f);

        // Double-check that SaveManager isn't handling this
        if (!saveManagerIsHandlingRestore)
        {
            RestorePlayerDataAfterTransition();
        }
        else
        {
            DebugLog("Skipping doorway data restoration - SaveManager is handling it");
        }
    }

    /// <summary>
    /// Get current persistent data for save system
    /// </summary>
    public PlayerPersistentData GetPersistentDataForSave()
    {
        SavePlayerDataForTransition(); // Update with current values
        return new PlayerPersistentData(persistentData); // Return copy
    }

    /// <summary>
    /// Load persistent data from save system
    /// FIX: Clear doorway data when loading from save to prevent conflicts
    /// </summary>
    public void LoadPersistentDataFromSave(PlayerPersistentData saveData)
    {
        if (saveData != null)
        {
            persistentData = new PlayerPersistentData(saveData);

            // FIX: Clear doorway transition data since we're loading from save
            hasPersistentData = false;
            saveManagerIsHandlingRestore = true;

            DebugLog("Player persistent data loaded from save - doorway data cleared");
        }
    }

    /// <summary>
    /// FIX: Reset save manager flag after restoration is complete
    /// </summary>
    public void SaveManagerRestorationComplete()
    {
        saveManagerIsHandlingRestore = false;
        DebugLog("SaveManager restoration complete - doorway system re-enabled");
    }

    /// <summary>
    /// Clear persistent data (for new game)
    /// </summary>
    public void ClearPersistentData()
    {
        persistentData = new PlayerPersistentData();
        hasPersistentData = false;
        saveManagerIsHandlingRestore = false;
        DebugLog("Player persistent data cleared");
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerPersistenceManager] {message}");
        }
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}