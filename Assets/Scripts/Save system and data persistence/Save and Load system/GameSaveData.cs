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