using System;
using System.Collections.Generic;
using UnityEngine;

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

        // Basic player data
        playersaveData.currentHealth = playerPersistentData.currentHealth;
        playersaveData.canJump = playerPersistentData.canJump;
        playersaveData.canSprint = playerPersistentData.canSprint;
        playersaveData.canCrouch = playerPersistentData.canCrouch;
        playersaveData.position = playerPositionData.position;
        playersaveData.rotation = playerPositionData.rotation;
        playersaveData.currentScene = currentScene;

        // FIXED: Copy inventory data
        playersaveData.inventoryData = playerPersistentData.inventoryData;

        // FIXED: Copy equipment data
        playersaveData.equipmentData = playerPersistentData.equipmentData;

        Debug.Log($"Set playersavedata - inventory: {playersaveData.inventoryData?.ItemCount ?? 0} items, equipment: {(playersaveData.equipmentData?.equippedItem?.isEquipped == true ? "has equipped item" : "no equipped item")}");
    }
}

[System.Serializable]
public class PlayerPositionData
{
    public Vector3 position;
    public Vector3 rotation;
}