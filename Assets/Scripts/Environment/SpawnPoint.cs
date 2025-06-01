using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


/// <summary>
/// Defines spawn points for scene transitions
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Spawn Point Settings")]
    public string spawnPointID = "DefaultSpawn";
    [Tooltip("Description for designers")]
    public string description = "";

    [Header("Visual Helpers")]
    public bool showGizmo = true;
    public Color gizmoColor = Color.green;
    public float gizmoSize = 1f;

    private static SpawnPoint[] allSpawnPoints;

    private void Awake()
    {
        // Cache all spawn points in the scene
        allSpawnPoints = FindObjectsByType<SpawnPoint>(sortMode: FindObjectsSortMode.None);
    }

    /// <summary>
    /// Find a spawn point by ID in the current scene
    /// </summary>
    public static SpawnPoint FindSpawnPoint(string id)
    {
        if (allSpawnPoints == null)
            allSpawnPoints = FindObjectsByType<SpawnPoint>(sortMode: FindObjectsSortMode.None);

        foreach (var spawnPoint in allSpawnPoints)
        {
            if (spawnPoint.spawnPointID == id)
                return spawnPoint;
        }

        // Fallback to default spawn if specific ID not found
        if (id != "DefaultSpawn")
        {
            Debug.LogWarning($"Spawn point with ID '{id}' not found. Returning default spawn point.");
            foreach (var spawnPoint in allSpawnPoints)
            {
                if (spawnPoint.spawnPointID == "DefaultSpawn")
                    return spawnPoint;
            }
        }

        return null;
    }

    /// <summary>
    /// Get all spawn points in the current scene
    /// </summary>
    public static SpawnPoint[] GetAllSpawnPoints()
    {
        if (allSpawnPoints == null)
            allSpawnPoints = FindObjectsByType<SpawnPoint>(sortMode: FindObjectsSortMode.None);
        return allSpawnPoints;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo) return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * gizmoSize);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoSize);

        // Draw ID label
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoSize + 0.5f), spawnPointID);
#endif
    }

    private void OnValidate()
    {
        // Ensure we have a valid spawn point ID
        if (string.IsNullOrEmpty(spawnPointID))
            spawnPointID = "DefaultSpawn";
    }
}