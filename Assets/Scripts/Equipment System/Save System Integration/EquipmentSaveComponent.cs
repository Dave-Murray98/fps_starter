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

        var saveData = equipmentManager.GetDataToSave() as EquipmentSaveData;
        DebugLog($"Saved equipment: {(saveData.equippedItem?.isEquipped == true ? "has equipped item" : "no equipped item")}");
        return saveData;
    }

    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("EquipmentSaveComponent: Extracting equipment save data");

        if (saveContainer is PlayerSaveData playerSaveData)
        {
            // FIXED: Extract the actual saved equipment data, not current state!
            if (playerSaveData.equipmentData != null)
            {
                DebugLog("Extracting equipment data from PlayerSaveData");
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
            DebugLog("No equipment data found in save container - creating empty");
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

        DebugLog($"Loading equipment: {(equipmentData.equippedItem?.isEquipped == true ? "has equipped item" : "no equipped item")}");

        try
        {
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