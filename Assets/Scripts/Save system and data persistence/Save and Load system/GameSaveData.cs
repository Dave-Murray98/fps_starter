using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main container for all save data for the save/load system.
/// This includes both player-specific data (ie player data that persists between scenes, such as player health) and scene specific save data (ie data that is scene-dependent such as whether a scene's door is locked/unlocked).
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

    public void SetPlayerSaveDataToPlayerPersistentData()
    {
        if (playersaveData == null)
            playersaveData = new PlayerSaveData();

        playersaveData.currentHealth = playerPersistentData.currentHealth;
        playersaveData.canJump = playerPersistentData.canJump;
        playersaveData.canSprint = playerPersistentData.canSprint;
        playersaveData.canCrouch = playerPersistentData.canCrouch;
        playersaveData.position = playerPositionData.position;
        playersaveData.rotation = playerPositionData.rotation;
        playersaveData.currentScene = currentScene;

        playersaveData.inventoryData = playerPersistentData.inventoryData;
        //Debug.Log($"Set playersavedata.inventorydata, itemcount: {playersaveData.inventoryData.ItemCount} items");
    }
}

[System.Serializable]
public class PlayerPositionData
{
    public Vector3 position;
    public Vector3 rotation;
}