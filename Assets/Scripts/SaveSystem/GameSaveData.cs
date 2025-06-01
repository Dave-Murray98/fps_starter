using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main container for all save data
/// </summary>
[System.Serializable]
public class GameSaveData
{
    [Header("Core Data")]
    public PlayerSaveData playerData = new PlayerSaveData();
    public GameProgressData progressData = new GameProgressData();

    [Header("Scene Data")]
    public Dictionary<string, SceneSaveData> sceneData = new Dictionary<string, SceneSaveData>();

    [Header("Future Systems")]
    public Dictionary<string, object> customData = new Dictionary<string, object>();

    [Header("Meta Data")]
    public string saveVersion = "1.0";
    public DateTime saveTime = DateTime.Now;
    public string currentScene = "";
    public float totalPlayTime = 0f;
}