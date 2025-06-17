using UnityEngine;

/// <summary>
/// Save component for the equipment system
/// FIXED: Now properly extracts saved equipment data instead of current state
/// </summary>
public class EquipmentSaveComponent : SaveComponentBase
{
    private EquippedItemManager equipmentManager;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();
        saveID = "Equipment_Main";
        autoGenerateID = false;
    }

    private void Start()
    {
        equipmentManager = EquippedItemManager.Instance;
    }

    public override object GetDataToSave()
    {
        if (equipmentManager == null)
        {
            DebugLog("Cannot save equipment - EquippedItemManager not found");
            return new EquipmentSaveData();
        }

        // CLEANED: Call the simplified GetDataToSave method
        var saveData = equipmentManager.GetDataToSave();

        // Debug log what we're saving
        if (saveData?.hotkeyBindings != null)
        {
            var assignedCount = saveData.hotkeyBindings.FindAll(h => h.isAssigned).Count;
            DebugLog($"Saving equipment: {assignedCount} hotkey assignments, equipped: {saveData.equippedItem?.isEquipped == true}");

            // Debug first hotkey specifically
            var binding1 = saveData.hotkeyBindings.Find(h => h.slotNumber == 1);
            if (binding1?.isAssigned == true)
            {
                DebugLog($"Saving hotkey 1: {binding1.itemDataName} (ID: {binding1.itemId})");
            }
        }

        return saveData;
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("EquipmentSaveComponent: Extracting equipment save data");

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // FIXED: Extract the actual saved equipment data from PlayerSaveData
            if (playerSaveData.equipmentData != null)
            {
                // Debug log what we're extracting
                var assignedCount = playerSaveData.equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
                DebugLog($"Extracting equipment data from PlayerSaveData - {assignedCount} hotkey assignments, equipped: {playerSaveData.equipmentData.equippedItem?.isEquipped == true}");

                // Debug first hotkey specifically
                var binding1 = playerSaveData.equipmentData.hotkeyBindings?.Find(h => h.slotNumber == 1);
                if (binding1?.isAssigned == true)
                {
                    DebugLog($"Extracting hotkey 1: {binding1.itemDataName} (ID: {binding1.itemId})");
                }

                return playerSaveData.equipmentData; // Return the SAVED data
            }
            else
            {
                DebugLog("No equipment data in PlayerSaveData - creating empty");
                return new EquipmentSaveData();
            }
        }
        else if (saveContainer is EquipmentSaveData equipmentData)
        {
            DebugLog("Extracting direct EquipmentSaveData");
            return equipmentData;
        }
        else
        {
            DebugLog($"No equipment data found in save container (type: {saveContainer?.GetType()}) - creating empty");
            return new EquipmentSaveData();
        }
    }

    public override void LoadSaveData(object data)
    {
        if (!(data is EquipmentSaveData equipmentData))
        {
            DebugLog($"Invalid save data type for equipment. Data type: {data?.GetType()}");
            return;
        }

        if (equipmentManager == null)
        {
            equipmentManager = EquippedItemManager.Instance;
            if (equipmentManager == null)
            {
                DebugLog("Cannot load equipment - EquippedItemManager not found");
                return;
            }
        }

        // Debug log what we're loading
        var assignedCount = equipmentData.hotkeyBindings?.FindAll(h => h.isAssigned)?.Count ?? 0;
        DebugLog($"Loading equipment: {assignedCount} hotkey assignments, equipped: {equipmentData.equippedItem?.isEquipped == true}");

        // Debug first hotkey specifically
        var binding1 = equipmentData.hotkeyBindings?.Find(h => h.slotNumber == 1);
        if (binding1?.isAssigned == true)
        {
            DebugLog($"Loading hotkey 1: {binding1.itemDataName} (ID: {binding1.itemId})");
        }

        try
        {
            // CLEANED: Call the simplified LoadSaveData method
            equipmentManager.LoadSaveData(equipmentData);
            DebugLog("Equipment loaded successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load equipment: {e.Message}");
        }
    }

    public override void OnBeforeSave()
    {
        DebugLog("Preparing equipment for save");
    }

    public override void OnAfterLoad()
    {
        DebugLog("Equipment load completed");
    }
}