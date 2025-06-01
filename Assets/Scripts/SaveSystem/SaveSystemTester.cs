using UnityEngine;
using Sirenix.OdinInspector;

public class SaveSystemTester : MonoBehaviour
{
    private void Start()
    {
        // Subscribe to save events for testing
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSaveComplete += OnSaveComplete;
            SaveManager.Instance.OnLoadComplete += OnLoadComplete;
        }
    }

    [Button("Test Save")]
    public void TestSave()
    {
        Debug.Log("=== TESTING SAVE ===");
        SaveManager.Instance?.SaveGame();
    }

    [Button("Test Load")]
    public void TestLoad()
    {
        Debug.Log("=== TESTING LOAD ===");
        SaveManager.Instance?.LoadGame();
    }

    [Button("Test New Game")]
    public void TestNewGame()
    {
        Debug.Log("=== TESTING NEW GAME ===");
        SaveManager.Instance?.NewGame();
    }

    [Button("1. Save Current State")]
    public void SaveCurrentState()
    {
        var player = FindFirstObjectByType<PlayerController>();
        var playerManager = GameManager.Instance?.playerManager;

        if (player != null && playerManager != null)
        {
            Debug.Log($"=== SAVING STATE ===");
            Debug.Log($"Current Position: {player.transform.position}");
            Debug.Log($"Current Health: {playerManager.currentHealth}/{GameManager.Instance.playerData.maxHealth}");

            SaveManager.Instance?.SaveGame();
        }
    }

    [Button("2. Modify Player State")]
    public void ModifyPlayerState()
    {
        var player = FindFirstObjectByType<PlayerController>();
        var playerManager = GameManager.Instance?.playerManager;

        if (player != null && playerManager != null)
        {
            // Move player to a very different position
            Vector3 newPos = player.transform.position + new Vector3(10f, 0f, 10f);
            player.transform.position = newPos;

            // Take significant damage
            playerManager.ModifyHealth(-30f);

            Debug.Log($"=== MODIFIED STATE ===");
            Debug.Log($"New Position: {player.transform.position}");
            Debug.Log($"New Health: {playerManager.currentHealth}/{GameManager.Instance.playerData.maxHealth}");
        }
    }

    [Button("3. Load Saved State")]
    public void LoadSavedState()
    {
        Debug.Log($"=== LOADING SAVED STATE ===");

        if (SaveManager.Instance.SaveExists())
        {
            var saveData = ES3.Load<GameSaveData>("gameData", "GameSave.es3");
            if (saveData?.playerData != null)
            {
                var playerSaveComponent = FindFirstObjectByType<PlayerSaveComponent>();
                if (playerSaveComponent != null)
                {
                    Debug.Log($"Save file contains - Pos: {saveData.playerData.position}, Health: {saveData.playerData.health}");

                    playerSaveComponent.LoadSaveData(saveData.playerData);

                    var player = FindFirstObjectByType<PlayerController>();
                    var playerManager = GameManager.Instance?.playerManager;

                    Debug.Log($"=== AFTER LOADING ===");
                    Debug.Log($"Loaded Position: {player.transform.position}");
                    Debug.Log($"Loaded Health: {playerManager.currentHealth}/{GameManager.Instance.playerData.maxHealth}");
                }
            }
        }
        else
        {
            Debug.LogWarning("No save file found!");
        }
    }

    [Button("Show Current State")]
    public void ShowCurrentState()
    {
        var player = FindFirstObjectByType<PlayerController>();
        var playerManager = GameManager.Instance?.playerManager;

        if (player != null && playerManager != null)
        {
            Debug.Log($"=== CURRENT STATE ===");
            Debug.Log($"Position: {player.transform.position}");
            Debug.Log($"Health: {playerManager.currentHealth}/{GameManager.Instance.playerData.maxHealth}");
        }
    }

    private void OnSaveComplete(bool success)
    {
        Debug.Log($"Save completed: {(success ? "SUCCESS" : "FAILED")}");
    }

    private void OnLoadComplete(bool success)
    {
        Debug.Log($"Load completed: {(success ? "SUCCESS" : "FAILED")}");
    }
}