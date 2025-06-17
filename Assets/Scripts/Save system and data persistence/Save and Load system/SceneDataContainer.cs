using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

[System.Serializable]
public class SceneDataContainer
{
    [ShowInInspector]
    public Dictionary<string, SceneSaveData> sceneData = new Dictionary<string, SceneSaveData>();

    public void SetSceneData(string sceneName, SceneSaveData data)
    {
        sceneData[sceneName] = data;

        // IMMEDIATE debug output
        //        Debug.Log($"[SceneDataContainer] SetSceneData called for '{sceneName}' with {data.objectData.Count} objects");

        // Log specific SceneItemStateManager data if present
        if (data.objectData.ContainsKey("SceneItemStateManager"))
        {
            Debug.Log($"[SceneDataContainer] SceneItemStateManager data saved to container");
        }
        else
        {
            Debug.Log($"[SceneDataContainer] WARNING: SceneItemStateManager data NOT found in scene data");
        }

    }

    public SceneSaveData GetSceneData(string sceneName)
    {
        if (sceneData.TryGetValue(sceneName, out SceneSaveData data))
        {
            // Debug.Log($"[SceneDataContainer] Retrieved scene data for '{sceneName}' with {data.objectData.Count} objects");
            return data;
        }

        Debug.Log($"[SceneDataContainer] No scene data found for '{sceneName}'");
        return null;
    }
}