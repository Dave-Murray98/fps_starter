using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Enhanced testing system for scene transitions and save/load functionality
/// </summary>
public class SceneTransitionTester : MonoBehaviour
{
    [Header("Scene Testing")]
    [ValueDropdown("GetAvailableScenes")]
    public string targetScene = "TestLevel02";

    [ValueDropdown("GetSpawnPointIDs")]
    public string spawnPointID = "DefaultSpawn";

    [Header("Save/Load Testing")]
    public bool showOnScreenGUI = true;

    // ===== SCENE TRANSITION TESTING =====

    [Button("Transition to Target Scene", ButtonSizes.Large)]
    public void TransitionToTargetScene()
    {
        if (SceneTransitionManager.Instance != null)
        {
            Debug.Log($"=== TRANSITIONING TO {targetScene} ===");
            SceneTransitionManager.Instance.TransitionToScene(targetScene, spawnPointID);
        }
        else
        {
            Debug.LogError("SceneTransitionManager not found!");
        }
    }

    [Button("Go to TestLevel01")]
    public void GoToTestLevel01()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene("TestLevel01", "DefaultSpawn");
        }
    }

    [Button("Go to TestLevel02")]
    public void GoToTestLevel02()
    {
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.TransitionToScene("TestLevel02", "DefaultSpawn");
        }
    }

    // ===== SAVE/LOAD TESTING =====

    [Button("Save Game", ButtonSizes.Large)]
    public void SaveGame()
    {
        var player = FindFirstObjectByType<PlayerController>();
        var playerManager = GameManager.Instance?.playerManager;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (player != null && playerManager != null)
        {
            Debug.Log($"=== SAVING GAME ===");
            Debug.Log($"Scene: {currentScene}");
            Debug.Log($"Position: {player.transform.position}");
            Debug.Log($"Health: {playerManager.currentHealth}/{GameManager.Instance.playerData.maxHealth}");

            SaveManager.Instance?.SaveGame();
        }
    }

    [Button("Load Game", ButtonSizes.Large)]
    public void LoadGame()
    {
        Debug.Log($"=== LOADING GAME ===");

        if (SaveManager.Instance != null && SaveManager.Instance.SaveExists())
        {
            // Use the enhanced load method that handles scene transitions
            SaveManager.Instance.LoadGameWithSceneTransition();
        }
        else
        {
            Debug.LogWarning("No save file found!");
        }
    }

    [Button("Quick Save Current State")]
    public void QuickSave()
    {
        SaveGame();
    }

    [Button("Quick Load")]
    public void QuickLoad()
    {
        LoadGame();
    }

    // ===== PLAYER STATE TESTING =====

    [Button("Damage Player (-25)")]
    public void DamagePlayer()
    {
        var playerManager = GameManager.Instance?.playerManager;
        if (playerManager != null)
        {
            playerManager.ModifyHealth(-25f);
            Debug.Log($"Player damaged. Health: {playerManager.currentHealth}");
        }
    }

    [Button("Heal Player (+25)")]
    public void HealPlayer()
    {
        var playerManager = GameManager.Instance?.playerManager;
        if (playerManager != null)
        {
            playerManager.ModifyHealth(25f);
            Debug.Log($"Player healed. Health: {playerManager.currentHealth}");
        }
    }

    [Button("Move Player Random")]
    public void MovePlayerRandom()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-10f, 10f),
                0f,
                Random.Range(-10f, 10f)
            );
            player.transform.position += randomOffset;
            Debug.Log($"Player moved to: {player.transform.position}");
        }
    }

    // ===== ON-SCREEN GUI =====

    private void OnGUI()
    {
        if (!showOnScreenGUI) return;

        var player = FindFirstObjectByType<PlayerController>();
        var playerManager = GameManager.Instance?.playerManager;
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        GUILayout.BeginArea(new Rect(10, 10, 400, 500));

        // Current State
        GUILayout.Label($"Scene: {currentScene}", GUI.skin.box);
        if (player != null && playerManager != null)
        {
            GUILayout.Label($"Position: {player.transform.position}");
            GUILayout.Label($"Health: {playerManager.currentHealth:F1}/{GameManager.Instance.playerData.maxHealth:F1}");
        }

        GUILayout.Space(10);

        // Scene Transitions
        GUILayout.Label("Scene Transitions:", GUI.skin.box);
        if (GUILayout.Button("Go to TestLevel01"))
            GoToTestLevel01();
        if (GUILayout.Button("Go to TestLevel02"))
            GoToTestLevel02();

        GUILayout.Space(10);

        // Save/Load
        GUILayout.Label("Save/Load:", GUI.skin.box);
        if (GUILayout.Button("Save Game"))
            SaveGame();
        if (GUILayout.Button("Load Game"))
            LoadGame();

        GUILayout.Space(10);

        // Player State
        GUILayout.Label("Player State:", GUI.skin.box);
        if (GUILayout.Button("Damage (-25)"))
            DamagePlayer();
        if (GUILayout.Button("Heal (+25)"))
            HealPlayer();
        if (GUILayout.Button("Move Random"))
            MovePlayerRandom();

        // Save file info
        GUILayout.Space(10);
        GUILayout.Label("Save File:", GUI.skin.box);
        bool saveExists = SaveManager.Instance != null && SaveManager.Instance.SaveExists();
        GUILayout.Label($"Save exists: {(saveExists ? "YES" : "NO")}");

        GUILayout.EndArea();
    }

    // ===== HELPER METHODS FOR DROPDOWNS =====

    private string[] GetAvailableScenes()
    {
        return new string[] { "TestLevel01", "TestLevel02" };
    }

    private string[] GetSpawnPointIDs()
    {
        var spawnPoints = SpawnPoint.GetAllSpawnPoints();
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            string[] ids = new string[spawnPoints.Length];
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                ids[i] = spawnPoints[i].spawnPointID;
            }
            return ids;
        }
        return new string[] { "DefaultSpawn" };
    }

    // ===== EVENT SUBSCRIPTIONS =====

    private void Start()
    {
        // Subscribe to transition events
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.OnSceneTransitionStarted += OnTransitionStarted;
            SceneTransitionManager.Instance.OnSceneTransitionCompleted += OnTransitionCompleted;
        }

        // Subscribe to save events
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete += OnSaveComplete;
            SaveManager.Instance.OnLoadComplete += OnLoadComplete;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.OnSceneTransitionStarted -= OnTransitionStarted;
            SceneTransitionManager.Instance.OnSceneTransitionCompleted -= OnTransitionCompleted;
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete -= OnSaveComplete;
            SaveManager.Instance.OnLoadComplete -= OnLoadComplete;
        }
    }

    private void OnTransitionStarted(string sceneName)
    {
        Debug.Log($"🔄 Scene transition to '{sceneName}' started");
    }

    private void OnTransitionCompleted(string sceneName)
    {
        Debug.Log($"✅ Scene transition to '{sceneName}' completed");
    }

    private void OnSaveComplete(bool success)
    {
        Debug.Log($"💾 Save completed: {(success ? "SUCCESS" : "FAILED")}");
    }

    private void OnLoadComplete(bool success)
    {
        Debug.Log($"📁 Load completed: {(success ? "SUCCESS" : "FAILED")}");
    }
}