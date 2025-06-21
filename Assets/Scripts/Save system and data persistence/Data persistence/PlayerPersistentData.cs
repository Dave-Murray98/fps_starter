using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced PlayerPersistentData that supports modular save components
/// ENHANCED: Added dynamic component data storage while maintaining backward compatibility
/// </summary>
[System.Serializable]
public class PlayerPersistentData
{
    [Header("Health System")]
    public float currentHealth = 100f;

    [Header("Abilities")]
    public bool canJump = true;
    public bool canSprint = true;
    public bool canCrouch = true;

    [Header("Inventory - Backward Compatibility")]
    public InventorySaveData inventoryData;

    [Header("Equipment - Backward Compatibility")]
    public EquipmentSaveData equipmentData;

    [Header("Dynamic Component Data")]
    [SerializeField] private Dictionary<string, object> componentData = new Dictionary<string, object>();

    // Default constructor
    public PlayerPersistentData()
    {
        // Default values
        inventoryData = new InventorySaveData();
        equipmentData = new EquipmentSaveData();
        componentData = new Dictionary<string, object>();
    }

    // SIMPLIFIED: Copy constructor using proper copy constructors
    public PlayerPersistentData(PlayerPersistentData other)
    {
        // Copy basic player data
        currentHealth = other.currentHealth;
        canJump = other.canJump;
        canSprint = other.canSprint;
        canCrouch = other.canCrouch;

        // Deep copy inventory data
        if (other.inventoryData != null)
        {
            inventoryData = new InventorySaveData(other.inventoryData.gridWidth, other.inventoryData.gridHeight);
            inventoryData.nextItemId = other.inventoryData.nextItemId;

            // Copy all items
            foreach (var item in other.inventoryData.items)
            {
                var itemCopy = new InventoryItemSaveData(item.itemID, item.itemDataName, item.gridPosition, item.currentRotation);
                itemCopy.stackCount = item.stackCount;
                inventoryData.AddItem(itemCopy);
            }
        }
        else
        {
            inventoryData = new InventorySaveData();
        }

        // SIMPLIFIED: Use the EquipmentSaveData copy constructor
        if (other.equipmentData != null)
        {
            equipmentData = new EquipmentSaveData(other.equipmentData);

            // Debug log to verify copy worked
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            Debug.Log($"[PlayerPersistentData] Copy constructor: Copied equipment data with {assignedCount} hotkey assignments");
        }
        else
        {
            Debug.Log("[PlayerPersistentData] Equipment data is null, creating new EquipmentSaveData");
            equipmentData = new EquipmentSaveData();
        }

        // Deep copy component data dictionary
        componentData = new Dictionary<string, object>();
        if (other.componentData != null)
        {
            foreach (var kvp in other.componentData)
            {
                componentData[kvp.Key] = kvp.Value; // Note: This is a shallow copy of the values
            }
        }
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

        // Handle legacy backward compatibility
        if (saveID == "Inventory_Main" && inventoryData != null)
            return inventoryData as T;

        if (saveID == "Equipment_Main" && equipmentData != null)
            return equipmentData as T;

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

        // Handle legacy backward compatibility
        if (saveID == "Inventory_Main" && data is InventorySaveData invData)
        {
            inventoryData = invData;
            componentData[saveID] = data; // Also store in dynamic storage for consistency
            return;
        }

        if (saveID == "Equipment_Main" && data is EquipmentSaveData eqData)
        {
            equipmentData = eqData;
            componentData[saveID] = data; // Also store in dynamic storage for consistency
            return;
        }

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

        // Check legacy fields
        if (saveID == "Inventory_Main")
            return inventoryData != null;

        if (saveID == "Equipment_Main")
            return equipmentData != null;

        // Check dynamic storage
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

        bool removed = false;

        // Handle legacy fields
        if (saveID == "Inventory_Main")
        {
            inventoryData = null;
            removed = true;
        }

        if (saveID == "Equipment_Main")
        {
            equipmentData = null;
            removed = true;
        }

        // Remove from dynamic storage
        if (componentData.ContainsKey(saveID))
        {
            componentData.Remove(saveID);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// Get all stored component save IDs
    /// </summary>
    /// <returns>Collection of save IDs that have data</returns>
    public IEnumerable<string> GetStoredComponentIDs()
    {
        var ids = new List<string>();

        // Add legacy IDs if they have data
        if (inventoryData != null)
            ids.Add("Inventory_Main");

        if (equipmentData != null)
            ids.Add("Equipment_Main");

        // Add dynamic storage IDs
        ids.AddRange(componentData.Keys);

        return ids;
    }

    /// <summary>
    /// Clear all component data
    /// </summary>
    public void ClearAllComponentData()
    {
        inventoryData = null;
        equipmentData = null;
        componentData.Clear();
    }

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

        // Legacy data info
        info.AppendLine($"Legacy Inventory: {(inventoryData != null ? $"{inventoryData.ItemCount} items" : "null")}");
        info.AppendLine($"Legacy Equipment: {(equipmentData != null ? "present" : "null")}");

        // Dynamic data info
        info.AppendLine($"Dynamic Component Data: {componentData.Count} entries");
        foreach (var kvp in componentData)
        {
            info.AppendLine($"  - {kvp.Key}: {kvp.Value?.GetType().Name ?? "null"}");
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

        // Validate inventory data if present
        if (inventoryData != null && !inventoryData.IsValid())
            return false;

        // Validate equipment data if present
        if (equipmentData != null && !equipmentData.IsValid())
            return false;

        return true;
    }

    #endregion
}