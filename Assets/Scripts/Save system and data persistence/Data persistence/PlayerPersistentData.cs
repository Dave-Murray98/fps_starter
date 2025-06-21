using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Clean PlayerPersistentData that supports fully modular save components
/// CLEANED: Removed legacy backward compatibility fields - all data now uses dynamic storage
/// This is the final, clean version with no hardcoded component knowledge
/// </summary>
[System.Serializable]
public class PlayerPersistentData
{
    [Header("Basic Player Stats")]
    public float currentHealth = 100f;
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;

    [Header("Dynamic Component Data")]
    [SerializeField] private Dictionary<string, object> componentData = new Dictionary<string, object>();

    // Default constructor
    public PlayerPersistentData()
    {
        componentData = new Dictionary<string, object>();
    }

    // Copy constructor - now much simpler without legacy fields
    public PlayerPersistentData(PlayerPersistentData other)
    {
        // Copy basic player data
        currentHealth = other.currentHealth;
        canJump = other.canJump;
        canSprint = other.canSprint;
        canCrouch = other.canCrouch;

        // Deep copy component data dictionary
        componentData = new Dictionary<string, object>();
        if (other.componentData != null)
        {
            foreach (var kvp in other.componentData)
            {
                componentData[kvp.Key] = kvp.Value; // Note: This is a shallow copy of the values
            }
        }

        Debug.Log($"[PlayerPersistentData] Copy constructor: Copied {componentData.Count} component data entries");
    }

    #region Dynamic Component Data Management

    /// <summary>
    /// Get component data by save ID with type safety
    /// </summary>
    /// <typeparam name="T">Expected data type</typeparam>
    /// <param name="saveID">Component's save ID</param>
    /// <returns>Component data or null if not found</returns>
    public T GetComponentData<T>(string saveID) where T : class
    {
        if (string.IsNullOrEmpty(saveID))
            return null;

        // Check dynamic storage
        if (componentData.TryGetValue(saveID, out object data))
        {
            return data as T;
        }

        return null;
    }

    /// <summary>
    /// Set component data by save ID
    /// </summary>
    /// <param name="saveID">Component's save ID</param>
    /// <param name="data">Data to store</param>
    public void SetComponentData(string saveID, object data)
    {
        if (string.IsNullOrEmpty(saveID))
            return;

        // Store in dynamic storage
        componentData[saveID] = data;
    }

    /// <summary>
    /// Check if component data exists for a save ID
    /// </summary>
    /// <param name="saveID">Component's save ID</param>
    /// <returns>True if data exists</returns>
    public bool HasComponentData(string saveID)
    {
        if (string.IsNullOrEmpty(saveID))
            return false;

        return componentData.ContainsKey(saveID);
    }

    /// <summary>
    /// Remove component data by save ID
    /// </summary>
    /// <param name="saveID">Component's save ID</param>
    /// <returns>True if data was removed</returns>
    public bool RemoveComponentData(string saveID)
    {
        if (string.IsNullOrEmpty(saveID))
            return false;

        return componentData.Remove(saveID);
    }

    /// <summary>
    /// Get all stored component save IDs
    /// </summary>
    /// <returns>Collection of save IDs that have data</returns>
    public IEnumerable<string> GetStoredComponentIDs()
    {
        return componentData.Keys;
    }

    /// <summary>
    /// Clear all component data
    /// </summary>
    public void ClearAllComponentData()
    {
        componentData.Clear();
    }

    /// <summary>
    /// Get count of stored component data entries
    /// </summary>
    public int ComponentDataCount => componentData.Count;

    #endregion

    #region Debug and Utility

    /// <summary>
    /// Get debug information about stored data
    /// </summary>
    /// <returns>Debug string with component data info</returns>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine("=== PlayerPersistentData Debug Info ===");
        info.AppendLine($"Health: {currentHealth}");
        info.AppendLine($"Abilities: Jump={canJump}, Sprint={canSprint}, Crouch={canCrouch}");

        // Component data info
        info.AppendLine($"Component Data: {componentData.Count} entries");
        foreach (var kvp in componentData)
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
    /// Validate data integrity
    /// </summary>
    /// <returns>True if data appears valid</returns>
    public bool IsValid()
    {
        // Basic validation
        if (currentHealth < 0)
            return false;

        // Validate that component data dictionary is not null
        if (componentData == null)
            return false;

        // Validate specific component data if present
        foreach (var kvp in componentData)
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
    /// Get component data entries as a list for debugging
    /// </summary>
    /// <returns>List of component data entries</returns>
    public List<ComponentDataEntry> GetComponentDataEntries()
    {
        var entries = new List<ComponentDataEntry>();
        foreach (var kvp in componentData)
        {
            entries.Add(new ComponentDataEntry
            {
                SaveID = kvp.Key,
                DataType = kvp.Value?.GetType().Name ?? "null",
                HasData = kvp.Value != null
            });
        }
        return entries;
    }

    /// <summary>
    /// Merge data from another PlayerPersistentData instance
    /// Useful for combining data from different sources
    /// </summary>
    /// <param name="other">Other data to merge</param>
    /// <param name="overwriteExisting">Whether to overwrite existing entries</param>
    public void MergeFrom(PlayerPersistentData other, bool overwriteExisting = true)
    {
        if (other == null) return;

        // Merge basic stats (always overwrite)
        currentHealth = other.currentHealth;
        canJump = other.canJump;
        canSprint = other.canSprint;
        canCrouch = other.canCrouch;

        // Merge component data
        foreach (var kvp in other.componentData)
        {
            if (overwriteExisting || !componentData.ContainsKey(kvp.Key))
            {
                componentData[kvp.Key] = kvp.Value;
            }
        }

        Debug.Log($"[PlayerPersistentData] Merged data from other instance: {other.componentData.Count} entries");
    }

    /// <summary>
    /// Create a snapshot with only specific component data
    /// Useful for partial saves or specific system backups
    /// </summary>
    /// <param name="saveIDs">Component IDs to include</param>
    /// <returns>New instance with only specified data</returns>
    public PlayerPersistentData CreatePartialSnapshot(params string[] saveIDs)
    {
        var snapshot = new PlayerPersistentData();

        // Copy basic stats
        snapshot.currentHealth = currentHealth;
        snapshot.canJump = canJump;
        snapshot.canSprint = canSprint;
        snapshot.canCrouch = canCrouch;

        // Copy only specified component data
        foreach (string saveID in saveIDs)
        {
            if (componentData.TryGetValue(saveID, out object data))
            {
                snapshot.componentData[saveID] = data;
            }
        }

        return snapshot;
    }

    #endregion
}

/// <summary>
/// Helper class for debugging component data entries
/// </summary>
[System.Serializable]
public class ComponentDataEntry
{
    public string SaveID;
    public string DataType;
    public bool HasData;

    public override string ToString()
    {
        return $"{SaveID}: {DataType} (HasData: {HasData})";
    }
}