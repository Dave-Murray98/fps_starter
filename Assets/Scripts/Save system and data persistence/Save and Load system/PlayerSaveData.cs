using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player-specific save data
/// CLEANED: Removed hardcoded inventory and equipment fields - now fully modular
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    [Header("Transform")]
    public Vector3 position = Vector3.zero;
    public Vector3 rotation = Vector3.zero;
    public string currentScene = "";

    [Header("Stats")]
    public float currentHealth = 100f;
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

    [Header("Dynamic Component Data")]
    public Dictionary<string, object> customStats = new Dictionary<string, object>();

    public PlayerSaveData()
    {
        customStats = new Dictionary<string, object>();
    }

    #region Dynamic Data Management

    /// <summary>
    /// Get custom stat/component data by key with type safety
    /// </summary>
    /// <typeparam name="T">Expected data type</typeparam>
    /// <param name="key">Data key</param>
    /// <returns>Data or null if not found</returns>
    public T GetCustomData<T>(string key) where T : class
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (customStats.TryGetValue(key, out object data))
        {
            return data as T;
        }

        return null;
    }

    /// <summary>
    /// Set custom stat/component data by key
    /// </summary>
    /// <param name="key">Data key</param>
    /// <param name="data">Data to store</param>
    public void SetCustomData(string key, object data)
    {
        if (string.IsNullOrEmpty(key))
            return;

        customStats[key] = data;
    }

    /// <summary>
    /// Check if custom data exists for a key
    /// </summary>
    /// <param name="key">Data key</param>
    /// <returns>True if data exists</returns>
    public bool HasCustomData(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return customStats.ContainsKey(key);
    }

    /// <summary>
    /// Remove custom data by key
    /// </summary>
    /// <param name="key">Data key</param>
    /// <returns>True if data was removed</returns>
    public bool RemoveCustomData(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return customStats.Remove(key);
    }

    /// <summary>
    /// Get all stored custom data keys
    /// </summary>
    /// <returns>Collection of keys that have data</returns>
    public IEnumerable<string> GetCustomDataKeys()
    {
        return customStats.Keys;
    }

    /// <summary>
    /// Clear all custom data
    /// </summary>
    public void ClearAllCustomData()
    {
        customStats.Clear();
    }

    /// <summary>
    /// Get count of stored custom data entries
    /// </summary>
    public int CustomDataCount => customStats.Count;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get debug information about this save data
    /// </summary>
    /// <returns>Debug string</returns>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== PlayerSaveData Debug Info ===");
        info.AppendLine($"Position: {position}");
        info.AppendLine($"Scene: {currentScene}");
        info.AppendLine($"Health: {currentHealth}/{maxHealth}");
        info.AppendLine($"Level: {level} (XP: {experience})");
        info.AppendLine($"Abilities: Jump={canJump}, Sprint={canSprint}, Crouch={canCrouch}");

        info.AppendLine($"Custom Data: {customStats.Count} entries");
        foreach (var kvp in customStats)
        {
            string dataInfo = "null";
            if (kvp.Value != null)
            {
                dataInfo = kvp.Value.GetType().Name;

                // Add more specific info for known types
                if (kvp.Value is InventorySaveData invData)
                {
                    dataInfo += $" ({invData.ItemCount} items)";
                }
                else if (kvp.Value is EquipmentSaveData eqData)
                {
                    var assignedCount = eqData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                    dataInfo += $" ({assignedCount} hotkeys assigned)";
                }
            }
            info.AppendLine($"  - {kvp.Key}: {dataInfo}");
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
        if (currentHealth < 0 || maxHealth <= 0)
            return false;

        if (string.IsNullOrEmpty(currentScene))
            return false;

        if (customStats == null)
            return false;

        // Validate specific component data if present
        foreach (var kvp in customStats)
        {
            if (string.IsNullOrEmpty(kvp.Key))
                return false;

            // Validate specific data types
            if (kvp.Value is InventorySaveData invData && !invData.IsValid())
                return false;

            if (kvp.Value is EquipmentSaveData eqData && !eqData.IsValid())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Create a copy with only basic player stats (no custom data)
    /// </summary>
    /// <returns>New PlayerSaveData with only basic stats</returns>
    public PlayerSaveData CreateBasicCopy()
    {
        return new PlayerSaveData
        {
            position = position,
            rotation = rotation,
            currentScene = currentScene,
            currentHealth = currentHealth,
            maxHealth = maxHealth,
            level = level,
            experience = experience,
            lookSensitivity = lookSensitivity,
            masterVolume = masterVolume,
            sfxVolume = sfxVolume,
            musicVolume = musicVolume,
            canJump = canJump,
            canSprint = canSprint,
            canCrouch = canCrouch
            // Note: customStats is left empty
        };
    }

    /// <summary>
    /// Merge custom data from another PlayerSaveData instance
    /// </summary>
    /// <param name="other">Other save data to merge from</param>
    /// <param name="overwriteExisting">Whether to overwrite existing entries</param>
    public void MergeCustomDataFrom(PlayerSaveData other, bool overwriteExisting = true)
    {
        if (other?.customStats == null) return;

        foreach (var kvp in other.customStats)
        {
            if (overwriteExisting || !customStats.ContainsKey(kvp.Key))
            {
                customStats[kvp.Key] = kvp.Value;
            }
        }
    }

    #endregion
}