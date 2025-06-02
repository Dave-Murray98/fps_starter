using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple container for scene data
/// </summary>
[System.Serializable]
public class SceneDataContainer
{
    public Dictionary<string, SceneSaveData> sceneData = new Dictionary<string, SceneSaveData>();

    public void SetSceneData(string sceneName, SceneSaveData data)
    {
        sceneData[sceneName] = data;
    }

    public SceneSaveData GetSceneData(string sceneName)
    {
        return sceneData.ContainsKey(sceneName) ? sceneData[sceneName] : null;
    }
}