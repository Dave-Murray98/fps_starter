using UnityEngine;
using System.Collections;

/// <summary>
/// ENHANCED: EquipmentSaveComponent now implements IPlayerDependentSaveable for true modularity
/// Handles its own data extraction, default creation, and contribution to unified saves
/// No longer requires hardcoded knowledge in PlayerPersistenceManager
/// UPDATED: Simplified to use only context-aware loading
/// </summary>
public class EquipmentSaveComponent : SaveComponentBase, IPlayerDependentSaveable
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
        // Use the helper method to get data directly
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
    /// FIXED: For PlayerPersistenceManager - extract only equipment data
    /// The issue was checking PlayerSaveData.customStats instead of PlayerPersistentData first
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("EquipmentSaveComponent: Extracting equipment save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new EquipmentSaveData();
        }

        // FIXED: Check PlayerPersistentData FIRST since that's where the rebuilt data is stored
        if (saveContainer is PlayerPersistentData persistentData)
        {
            // Extract from dynamic component storage
            var equipmentData = persistentData.GetComponentData<EquipmentSaveData>(SaveID);
            if (equipmentData != null)
            {
                var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment from persistent data dynamic storage: {assignedCount} hotkey assignments, equipped: {equipmentData.equippedItem?.isEquipped == true}");
                return equipmentData;
            }
            else
            {
                DebugLog("No equipment data in persistent data - returning empty equipment");
                return new EquipmentSaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Check if PlayerSaveData has equipment data in its custom stats
            if (playerSaveData.customStats.TryGetValue("equipmentData", out object eqDataObj) &&
                eqDataObj is EquipmentSaveData eqData)
            {
                var assignedCount = eqData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment data from PlayerSaveData customStats: {assignedCount} hotkey assignments, equipped: {eqData.equippedItem?.isEquipped == true}");
                return eqData;
            }

            // ALSO check for the component ID in custom stats
            if (playerSaveData.customStats.TryGetValue(SaveID, out object equipmentDataObj) &&
                equipmentDataObj is EquipmentSaveData equipmentSaveData)
            {
                var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracted equipment data from PlayerSaveData custom stats by SaveID: {assignedCount} hotkey assignments, equipped: {equipmentSaveData.equippedItem?.isEquipped == true}");
                return equipmentSaveData;
            }

            DebugLog("No equipment data found in PlayerSaveData - returning empty equipment");
            return new EquipmentSaveData();
        }
        else if (saveContainer is EquipmentSaveData equipmentSaveData)
        {
            // Direct equipment save data
            var assignedCount = equipmentSaveData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted direct EquipmentSaveData: {assignedCount} hotkey assignments");
            return equipmentSaveData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, EquipmentSaveData, or PlayerPersistentData, got {saveContainer.GetType()}");
            return new EquipmentSaveData();
        }
    }

    /// <summary>
    /// RESTORE data back to EquippedItemManager (manager doesn't handle its own loading anymore)
    /// UPDATED: Now uses context-aware loading
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is EquipmentSaveData equipmentData))
        {
            DebugLog($"Invalid save data type for equipment. Data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING EQUIPMENT DATA TO MANAGER (Context: {context}) ===");

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
            // Equipment restoration is the same regardless of context
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
            equippedItemManager.ClearEquipmentState();
            return;
        }

        // Use the helper method to set data directly
        equippedItemManager.SetEquipmentData(saveData);

        DebugLog("Equipment data restoration to manager complete");
    }

    #region IPlayerDependentSaveable Implementation - NEW MODULAR INTERFACE

    /// <summary>
    /// MODULAR: Extract equipment data from unified save structure
    /// This component knows how to get its data from PlayerPersistentData
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        // Get from dynamic component data storage
        var equipmentData = unifiedData.GetComponentData<EquipmentSaveData>(SaveID);
        if (equipmentData != null)
        {
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Extracted equipment from dynamic storage: {assignedCount} hotkey assignments");
            return equipmentData;
        }

        // Return empty equipment if nothing found
        DebugLog("No equipment data found in unified save - returning empty equipment");
        return new EquipmentSaveData();
    }

    /// <summary>
    /// MODULAR: Create default equipment data for new games
    /// This component knows what its default state should be
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default equipment data for new game");

        var defaultData = new EquipmentSaveData();

        // Default constructor already sets up 10 empty hotkey slots and empty equipped item
        DebugLog($"Default equipment data created: {defaultData.hotkeyBindings.Count} hotkey slots, no equipped item");
        return defaultData;
    }

    /// <summary>
    /// MODULAR: Contribute equipment data to unified save structure
    /// This component knows how to store its data in PlayerPersistentData
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is EquipmentSaveData equipmentData && unifiedData != null)
        {
            var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
            DebugLog($"Contributing equipment data to unified save structure: {assignedCount} hotkey assignments");

            // Store in dynamic storage
            unifiedData.SetComponentData(SaveID, equipmentData);

            DebugLog($"Equipment data contributed: {assignedCount} hotkey assignments stored in dynamic storage");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected EquipmentSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Lifecycle and Utility Methods

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

        // IMPORTANT: Force UI refresh after equipment load
        // This ensures hotkey bar and other UI elements display correctly after save/load
        StartCoroutine(RefreshEquipmentUIAfterLoad());
    }

    /// <summary>
    /// Force refresh equipment UI after load with proper timing
    /// </summary>
    private System.Collections.IEnumerator RefreshEquipmentUIAfterLoad()
    {
        // Wait a frame to ensure all managers are fully loaded
        yield return null;

        // Wait for UI systems to be ready
        yield return new WaitForEndOfFrame();

        if (equippedItemManager != null)
        {
            DebugLog("Forcing equipment UI refresh after load");

            // Force refresh all hotkey UI
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

            // Force refresh equipped item UI
            if (equippedItemManager.HasEquippedItem)
            {
                equippedItemManager.OnItemEquipped?.Invoke(equippedItemManager.CurrentEquippedItem);
            }
            else
            {
                equippedItemManager.OnItemUnequipped?.Invoke();
            }

            DebugLog("Equipment UI refresh completed");
        }
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

    /// <summary>
    /// Check if any item is currently equipped
    /// </summary>
    public bool HasEquippedItem()
    {
        return equippedItemManager?.HasEquippedItem == true;
    }

    /// <summary>
    /// Get equipped item type (useful for other systems)
    /// </summary>
    public ItemType? GetEquippedItemType()
    {
        if (!HasEquippedItem()) return null;

        return equippedItemManager.GetEquippedItemData()?.itemType;
    }

    /// <summary>
    /// Get debug information about current equipment state
    /// </summary>
    public string GetEquipmentDebugInfo()
    {
        if (equippedItemManager == null)
            return "EquippedItemManager: null";

        var equippedItemName = GetCurrentEquippedItemName();
        var hotkeyCount = GetAssignedHotkeyCount();

        return $"Equipment: {equippedItemName} equipped, {hotkeyCount}/10 hotkeys assigned";
    }

    /// <summary>
    /// Check if a specific hotkey slot is assigned
    /// </summary>
    public bool IsHotkeySlotAssigned(int slotNumber)
    {
        if (equippedItemManager == null) return false;

        var binding = equippedItemManager.GetHotkeyBinding(slotNumber);
        return binding?.isAssigned == true;
    }

    /// <summary>
    /// Get the item assigned to a specific hotkey slot
    /// </summary>
    public string GetHotkeySlotItemName(int slotNumber)
    {
        if (equippedItemManager == null) return null;

        var binding = equippedItemManager.GetHotkeyBinding(slotNumber);
        if (binding?.isAssigned == true)
        {
            return binding.GetCurrentItemData()?.itemName;
        }

        return null;
    }

    #endregion
}