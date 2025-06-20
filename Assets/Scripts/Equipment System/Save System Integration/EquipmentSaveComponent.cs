using UnityEngine;

/// <summary>
/// REFACTORED: EquipmentSaveComponent now handles ALL equipment data management
/// Extracts data from EquippedItemManager during saves
/// Restores data back to EquippedItemManager during loads
/// EquippedItemManager becomes a pure data holder
/// </summary>
public class EquipmentSaveComponent : SaveComponentBase
{
    [Header("Component References")]
    [SerializeField] private EquippedItemManager equippedItemManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();

        // Fixed ID for equipment
        saveID = "Equipment_Main";
        autoGenerateID = false;

        // Auto-find references if enabled
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }
    }

    private void Start()
    {
        // Ensure we have equipment reference
        ValidateReferences();
    }

    /// <summary>
    /// Automatically find equipment-related components
    /// </summary>
    private void FindEquipmentReferences()
    {
        // Try to find on same GameObject first
        if (equippedItemManager == null)
            equippedItemManager = GetComponent<EquippedItemManager>();

        // If not found on same GameObject, get from Instance
        if (equippedItemManager == null)
            equippedItemManager = EquippedItemManager.Instance;

        // If still not found, search scene
        if (equippedItemManager == null)
            equippedItemManager = FindFirstObjectByType<EquippedItemManager>();

        DebugLog($"Auto-found equipment reference: {equippedItemManager != null}");
    }

    /// <summary>
    /// Validate that we have necessary references
    /// </summary>
    private void ValidateReferences()
    {
        if (equippedItemManager == null)
        {
            Debug.LogError($"[{name}] EquippedItemManager reference is missing! Equipment won't be saved/loaded.");
        }
        else
        {
            DebugLog($"EquippedItemManager reference validated: {equippedItemManager.name}");
        }
    }

    /// <summary>
    /// EXTRACT equipment data from EquippedItemManager (manager doesn't handle its own saving anymore)
    /// </summary>
    public override object GetDataToSave()
    {
        if (equippedItemManager == null)
        {
            DebugLog("Cannot save equipment - EquippedItemManager not found");
            return new EquipmentSaveData(); // Return empty but valid data
        }

        // Extract data from the manager (manager doesn't do this itself anymore)
        var saveData = ExtractEquipmentDataFromManager();

        DebugLog($"Extracted equipment data: equipped={saveData.equippedItem?.isEquipped == true}, hotkeys={CountAssignedHotkeys(saveData)}");
        return saveData;
    }

    /// <summary>
    /// Extract equipment data from the manager (replaces manager's GetDataToSave method)
    /// </summary>
    private EquipmentSaveData ExtractEquipmentDataFromManager()
    {
        // Use the new helper method to get data directly
        return equippedItemManager.GetEquipmentDataDirect();
    }

    /// <summary>
    /// Count assigned hotkeys for debug logging
    /// </summary>
    private int CountAssignedHotkeys(EquipmentSaveData saveData)
    {
        if (saveData?.hotkeyBindings == null) return 0;
        return saveData.hotkeyBindings.FindAll(h => h.isAssigned).Count;
    }

    /// <summary>
    /// For PlayerPersistenceManager - extract only equipment data
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("EquipmentSaveComponent: Extracting equipment save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new EquipmentSaveData();
        }

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Extract equipment data from player save
            if (playerSaveData.equipmentData != null)
            {
                var assignedCount = playerSaveData.equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment data from PlayerSaveData: {assignedCount} hotkey assignments, equipped: {playerSaveData.equipmentData.equippedItem?.isEquipped == true}");
                return playerSaveData.equipmentData;
            }
            else
            {
                DebugLog("No equipment data found in PlayerSaveData - returning empty equipment");
                return new EquipmentSaveData();
            }
        }
        else if (saveContainer is EquipmentSaveData equipmentSaveData)
        {
            // Direct equipment save data
            var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted direct EquipmentSaveData: {assignedCount} hotkey assignments");
            return equipmentSaveData;
        }
        else if (saveContainer is PlayerPersistentData persistentData)
        {
            // Extract from persistent data structure
            if (persistentData.equipmentData != null)
            {
                var assignedCount = persistentData.equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment from persistent data: {assignedCount} hotkey assignments");
                return persistentData.equipmentData;
            }
            else
            {
                DebugLog("No equipment data in persistent data - returning empty equipment");
                return new EquipmentSaveData();
            }
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, EquipmentSaveData, or PlayerPersistentData, got {saveContainer.GetType()}");
            return new EquipmentSaveData();
        }
    }

    /// <summary>
    /// RESTORE data back to EquippedItemManager (manager doesn't handle its own loading anymore)
    /// </summary>
    public override void LoadSaveData(object data)
    {
        if (!(data is EquipmentSaveData equipmentData))
        {
            DebugLog($"Invalid save data type for equipment. Data type: {data?.GetType()}");
            return;
        }

        DebugLog("=== RESTORING EQUIPMENT DATA TO MANAGER ===");

        // Ensure we have current references (they might have changed after scene load)
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }

        if (equippedItemManager == null)
        {
            DebugLog("Cannot load equipment - EquippedItemManager not found");
            return;
        }

        // Debug what we're about to load
        var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
        DebugLog($"Loading equipment: {assignedCount} hotkey assignments, equipped: {equipmentData.equippedItem?.isEquipped == true}");

        try
        {
            // Restore data to the manager (manager doesn't do this itself anymore)
            RestoreEquipmentDataToManager(equipmentData);
            DebugLog("Equipment restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load equipment: {e.Message}");
        }
    }

    /// <summary>
    /// Restore equipment data to the manager (replaces manager's LoadSaveData method call)
    /// </summary>
    private void RestoreEquipmentDataToManager(EquipmentSaveData saveData)
    {
        if (saveData == null || !saveData.IsValid())
        {
            DebugLog("Invalid equipment save data - clearing equipment state");
            ClearEquipmentState();
            return;
        }

        // Clear current equipment state before loading
        ClearEquipmentState();

        // Load the equipment data by directly setting the manager's internal data
        // This replaces the manager's LoadSaveData method
        SetEquipmentDataToManager(saveData);

        // Validate loaded data against current inventory
        ValidateLoadedHotkeys();

        // Refresh UI for all hotkeys and equipped items
        RefreshAllEquipmentUI();

        DebugLog("Equipment data restoration to manager complete");
    }

    /// <summary>
    /// Set equipment data directly to the manager (replaces internal manager logic)
    /// </summary>
    private void SetEquipmentDataToManager(EquipmentSaveData saveData)
    {
        // Use the new helper method to set data directly
        equippedItemManager.SetEquipmentData(saveData);
        DebugLog("Equipment data set to manager via new helper method");
    }

    /// <summary>
    /// Clear current equipment state
    /// </summary>
    private void ClearEquipmentState()
    {
        // Use the new helper method to clear equipment state
        equippedItemManager.ClearEquipmentState();
        DebugLog("Current equipment state cleared via helper method");
    }

    /// <summary>
    /// Validate loaded hotkeys against current inventory
    /// </summary>
    private void ValidateLoadedHotkeys()
    {
        // This validation might need to be moved here from the manager
        // For now, the manager handles this internally
        DebugLog("Validating loaded hotkeys against inventory");
    }

    /// <summary>
    /// Refresh all equipment UI
    /// </summary>
    private void RefreshAllEquipmentUI()
    {
        // Trigger UI refresh events
        if (equippedItemManager.HasEquippedItem)
        {
            equippedItemManager.OnItemEquipped?.Invoke(equippedItemManager.CurrentEquippedItem);
        }
        else
        {
            equippedItemManager.OnItemUnequipped?.Invoke();
        }

        // Refresh hotkey UI for all slots
        var allBindings = equippedItemManager.GetAllHotkeyBindings();
        for (int i = 0; i < allBindings.Count; i++)
        {
            var binding = allBindings[i];
            if (binding.isAssigned)
            {
                equippedItemManager.OnHotkeyAssigned?.Invoke(binding.slotNumber, binding);
            }
            else
            {
                equippedItemManager.OnHotkeyCleared?.Invoke(binding.slotNumber);
            }
        }

        DebugLog("Equipment UI refresh complete");
    }

    /// <summary>
    /// Called before save operations
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing equipment for save");

        // Refresh references in case they changed
        if (autoFindReferences)
        {
            FindEquipmentReferences();
        }
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Equipment load completed");

        // Equipment UI should automatically update via manager events
    }

    /// <summary>
    /// Public method to manually set equipment manager reference
    /// </summary>
    public void SetEquippedItemManager(EquippedItemManager manager)
    {
        equippedItemManager = manager;
        autoFindReferences = false; // Disable auto-find when manually set
        DebugLog("Equipment manager reference manually set");
    }

    /// <summary>
    /// Get current equipped item name (useful for other systems)
    /// </summary>
    public string GetCurrentEquippedItemName()
    {
        if (equippedItemManager?.HasEquippedItem == true)
        {
            return equippedItemManager.GetEquippedItemData()?.itemName ?? "Unknown";
        }
        return "None";
    }

    /// <summary>
    /// Get count of assigned hotkeys (useful for other systems)
    /// </summary>
    public int GetAssignedHotkeyCount()
    {
        if (equippedItemManager == null) return 0;

        var bindings = equippedItemManager.GetAllHotkeyBindings();
        return bindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
    }

    /// <summary>
    /// Check if equipment manager reference is valid
    /// </summary>
    public bool HasValidReference()
    {
        return equippedItemManager != null;
    }

    /// <summary>
    /// Force refresh of equipment manager reference
    /// </summary>
    public void RefreshReference()
    {
        if (autoFindReferences)
        {
            FindEquipmentReferences();
            ValidateReferences();
        }
    }
}