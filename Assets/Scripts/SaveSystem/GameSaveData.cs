using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main container for all save data
/// </summary>
[System.Serializable]
public class GameSaveData
{
    public System.DateTime saveTime;
    public string currentScene;

    // Player data that persists between doorways
    public PlayerPersistentData playerPersistentData;

    // Player position (only for save/load, not doorways)
    public PlayerPositionData playerPositionData;

    // Scene data for all visited scenes
    public Dictionary<string, SceneSaveData> sceneData;
}

[System.Serializable]
public class PlayerPositionData
{
    public Vector3 position;
    public Vector3 rotation;
}