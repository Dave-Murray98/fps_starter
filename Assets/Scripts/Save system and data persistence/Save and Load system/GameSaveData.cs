using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game save data structure
/// CLEANED: Now fully modular - no hardcoded component field assignments
/// </summary>
[System.Serializable]
public class GameSaveData
{
    public System.DateTime saveTime;
    public string currentScene;

    public PlayerSaveData playersaveData;

    // Player data that persists between doorways
    public PlayerPersistentData playerPersistentData;

    // Player position (only for save/load, not doorways)
    public PlayerPositionData playerPositionData;

    // Scene data for all visited scenes
    public Dictionary<string, SceneSaveData> sceneData;

    /// <summary>
    /// MODULAR: Set PlayerSaveData from PlayerPersistentData using component system
    /// No longer hardcodes specific component fields
    /// </summary>
    public void SetPlayerSaveDataToPlayerPersistentData()
    {
        if (playersaveData == null)
            playersaveData = new PlayerSaveData();

        // Copy basic player data
        playersaveData.currentHealth = playerPersistentData.currentHealth;
        playersaveData.canJump = playerPersistentData.canJump;
        playersaveData.canSprint = playerPersistentData.canSprint;
        playersaveData.canCrouch = playerPersistentData.canCrouch;
        playersaveData.position = playerPositionData.position;
        playersaveData.rotation = playerPositionData.rotation;
        playersaveData.currentScene = currentScene;

        // MODULAR: Copy all component data from persistent data to custom stats
        foreach (string componentID in playerPersistentData.GetStoredComponentIDs())
        {
            var componentData = playerPersistentData.GetComponentData<object>(componentID);
            if (componentData != null)
            {
                // Store component data in PlayerSaveData's custom stats
                playersaveData.SetCustomData(componentID, componentData);
                Debug.Log($"[GameSaveData] Copied component data for {componentID}: {componentData.GetType().Name}");
            }
        }

        Debug.Log($"[GameSaveData] Set PlayerSaveData - Health: {playersaveData.currentHealth}, Components: {playersaveData.CustomDataCount}");
    }

    /// <summary>
    /// Get debug information about this save data
    /// </summary>
    /// <returns>Debug string</returns>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== GameSaveData Debug Info ===");
        info.AppendLine($"Save Time: {saveTime}");
        info.AppendLine($"Current Scene: {currentScene}");

        if (playersaveData != null)
        {
            info.AppendLine($"Player Save Data: Health={playersaveData.currentHealth}, Position={playersaveData.position}");
            info.AppendLine($"  Custom Data Entries: {playersaveData.CustomDataCount}");
        }
        else
        {
            info.AppendLine("Player Save Data: null");
        }

        if (playerPersistentData != null)
        {
            info.AppendLine($"Player Persistent Data: Health={playerPersistentData.currentHealth}");
            info.AppendLine($"  Component Data Entries: {playerPersistentData.ComponentDataCount}");
        }
        else
        {
            info.AppendLine("Player Persistent Data: null");
        }

        if (playerPositionData != null)
        {
            info.AppendLine($"Player Position: {playerPositionData.position}");
        }
        else
        {
            info.AppendLine("Player Position Data: null");
        }

        if (sceneData != null)
        {
            info.AppendLine($"Scene Data: {sceneData.Count} scenes");
            foreach (var kvp in sceneData)
            {
                info.AppendLine($"  - {kvp.Key}: {kvp.Value.objectData.Count} objects");
            }
        }
        else
        {
            info.AppendLine("Scene Data: null");
        }

        return info.ToString();
    }

    /// <summary>
    /// Validate that save data is consistent
    /// </summary>
    /// <returns>True if data appears valid</returns>
    public bool IsValid()
    {
        // Basic validation
        if (string.IsNullOrEmpty(currentScene))
            return false;

        if (saveTime == default(DateTime))
            return false;

        // Validate player data if present
        if (playersaveData != null && !playersaveData.IsValid())
            return false;

        if (playerPersistentData != null && !playerPersistentData.IsValid())
            return false;

        // Validate scene data if present
        if (sceneData != null)
        {
            foreach (var kvp in sceneData)
            {
                if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Get statistics about this save data
    /// </summary>
    /// <returns>Save data statistics</returns>
    public SaveDataStats GetStats()
    {
        var stats = new SaveDataStats();

        stats.SaveTime = saveTime;
        stats.CurrentScene = currentScene;
        stats.HasPlayerData = playersaveData != null;
        stats.HasPersistentData = playerPersistentData != null;
        stats.HasPositionData = playerPositionData != null;

        if (playersaveData != null)
        {
            stats.PlayerCustomDataCount = playersaveData.CustomDataCount;
            stats.PlayerHealth = playersaveData.currentHealth;
            stats.PlayerLevel = playersaveData.level;
        }

        if (playerPersistentData != null)
        {
            stats.PersistentComponentCount = playerPersistentData.ComponentDataCount;
        }

        if (sceneData != null)
        {
            stats.SceneCount = sceneData.Count;
            stats.TotalSceneObjects = 0;
            foreach (var scene in sceneData.Values)
            {
                stats.TotalSceneObjects += scene.objectData.Count;
            }
        }

        return stats;
    }
}

/// <summary>
/// Player position data (separate from persistent data for save/load distinction)
/// </summary>
[System.Serializable]
public class PlayerPositionData
{
    public Vector3 position;
    public Vector3 rotation;
}

/// <summary>
/// Statistics about save data for debugging and UI display
/// </summary>
[System.Serializable]
public class SaveDataStats
{
    public DateTime SaveTime;
    public string CurrentScene;
    public bool HasPlayerData;
    public bool HasPersistentData;
    public bool HasPositionData;
    public int PlayerCustomDataCount;
    public float PlayerHealth;
    public int PlayerLevel;
    public int PersistentComponentCount;
    public int SceneCount;
    public int TotalSceneObjects;

    public override string ToString()
    {
        return $"Save: {CurrentScene} @ {SaveTime:yyyy-MM-dd HH:mm}, " +
               $"Health: {PlayerHealth}, Level: {PlayerLevel}, " +
               $"Components: {PersistentComponentCount}, Scenes: {SceneCount}";
    }
}