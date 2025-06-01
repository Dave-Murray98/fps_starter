using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-specific save data
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    [Header("Transform")]
    public Vector3 position = Vector3.zero;
    public Vector3 rotation = Vector3.zero;
    public string currentScene = "";

    [Header("Stats")]
    public float health = 100f;
    public float maxHealth = 100f;
    public int level = 1;
    public float experience = 0f;

    [Header("Settings")]
    public float lookSensitivity = 2f;
    public float masterVolume = 1f;
    public float sfxVolume = 1f;
    public float musicVolume = 1f;

    [Header("Abilities")]
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;

    // Easy to expand for future player stats
    public Dictionary<string, object> customStats = new Dictionary<string, object>();
}