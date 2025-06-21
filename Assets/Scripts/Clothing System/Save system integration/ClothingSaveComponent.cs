using UnityEngine;
using System.Collections;
using System.Linq;

/// <summary>
/// Handles saving and loading of the clothing system using the modular save architecture.
/// Integrates with PlayerPersistenceManager and follows the same patterns as
/// EquipmentSaveComponent and InventorySaveComponent.
/// </summary>
public class ClothingSaveComponent : SaveComponentBase, IPlayerDependentSaveable
{
    [Header("Component References")]
    [SerializeField] private ClothingManager clothingManager;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    public override SaveDataCategory SaveCategory => SaveDataCategory.PlayerDependent;

    protected override void Awake()
    {
        base.Awake();

        // Fixed ID for clothing
        saveID = "Clothing_Main";
        autoGenerateID = false;

        // Auto-find references if enabled
        if (autoFindReferences)
        {
            FindClothingReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
    }

    /// <summary>
    /// Automatically find clothing-related components
    /// </summary>
    private void FindClothingReferences()
    {
        // Try to find on same GameObject first
        if (clothingManager == null)
            clothingManager = GetComponent<ClothingManager>();

        // If not found on same GameObject, get from Instance
        if (clothingManager == null)
            clothingManager = ClothingManager.Instance;

        // If still not found, search scene
        if (clothingManager == null)
            clothingManager = FindFirstObjectByType<ClothingManager>();

        DebugLog($"Auto-found clothing reference: {clothingManager != null}");
    }

    /// <summary>
    /// Validate that we have necessary references
    /// </summary>
    private void ValidateReferences()
    {
        if (clothingManager == null)
        {
            Debug.LogError($"[{name}] ClothingManager reference is missing! Clothing won't be saved/loaded.");
        }
        else
        {
            DebugLog($"ClothingManager reference validated: {clothingManager.name}");
        }
    }

    /// <summary>
    /// Extract clothing data from ClothingManager
    /// </summary>
    public override object GetDataToSave()
    {
        if (clothingManager == null)
        {
            DebugLog("Cannot save clothing - ClothingManager not found");
            return new ClothingSaveData();
        }

        var saveData = clothingManager.GetClothingDataForSave();
        var equippedCount = saveData.slotData.FindAll(s => s.isOccupied).Count;
        DebugLog($"Extracted clothing data: {equippedCount} items equipped");
        return saveData;
    }

    /// <summary>
    /// Extract clothing data from various save container formats
    /// </summary>
    public override object ExtractRelevantData(object saveContainer)
    {
        DebugLog("ClothingSaveComponent: Extracting clothing save data for persistence");

        if (saveContainer == null)
        {
            DebugLog("ExtractRelevantData: saveContainer is null");
            return new ClothingSaveData();
        }

        // Check PlayerPersistentData first (where rebuilt data is stored)
        if (saveContainer is PlayerPersistentData persistentData)
        {
            var clothingData = persistentData.GetComponentData<ClothingSaveData>(SaveID);
            if (clothingData != null)
            {
                var equippedCount = clothingData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
                DebugLog($"Extracted clothing from persistent data: {equippedCount} items equipped");
                return clothingData;
            }
            else
            {
                DebugLog("No clothing data in persistent data - returning empty clothing");
                return new ClothingSaveData();
            }
        }
        else if (saveContainer is PlayerSaveData playerSaveData)
        {
            // Check if PlayerSaveData has clothing data in its custom stats
            if (playerSaveData.customStats.TryGetValue("clothingData", out object clothingDataObj) &&
                clothingDataObj is ClothingSaveData clothingData)
            {
                var equippedCount = clothingData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
                DebugLog($"Extracted clothing data from PlayerSaveData customStats: {equippedCount} items equipped");
                return clothingData;
            }

            // Check for the component ID in custom stats
            if (playerSaveData.customStats.TryGetValue(SaveID, out object clothingDataObj2) &&
                clothingDataObj2 is ClothingSaveData clothingSaveData)
            {
                var equippedCount = clothingSaveData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
                DebugLog($"Extracted clothing data from PlayerSaveData by SaveID: {equippedCount} items equipped");
                return clothingSaveData;
            }

            DebugLog("No clothing data found in PlayerSaveData - returning empty clothing");
            return new ClothingSaveData();
        }
        else if (saveContainer is ClothingSaveData directClothingData)
        {
            var equippedCount = directClothingData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
            DebugLog($"Extracted direct ClothingSaveData: {equippedCount} items equipped");
            return directClothingData;
        }
        else
        {
            DebugLog($"Invalid save data type - expected PlayerSaveData, ClothingSaveData, or PlayerPersistentData, got {saveContainer.GetType()}");
            return new ClothingSaveData();
        }
    }

    /// <summary>
    /// Restore clothing data to ClothingManager with context awareness
    /// </summary>
    public override void LoadSaveDataWithContext(object data, RestoreContext context)
    {
        if (!(data is ClothingSaveData clothingData))
        {
            DebugLog($"Invalid save data type for clothing. Data type: {data?.GetType()}");
            return;
        }

        DebugLog($"=== RESTORING CLOTHING DATA (Context: {context}) ===");

        // Ensure we have current references (they might have changed after scene load)
        if (autoFindReferences)
        {
            FindClothingReferences();
        }

        if (clothingManager == null)
        {
            DebugLog("Cannot load clothing - ClothingManager not found");
            return;
        }

        // Debug what we're about to load
        var equippedCount = clothingData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
        DebugLog($"Loading clothing: {equippedCount} items equipped");

        try
        {
            // Clothing restoration is the same regardless of context
            clothingManager.LoadClothingDataFromSave(clothingData);
            DebugLog("Clothing restored successfully to manager");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load clothing: {e.Message}");
        }
    }

    #region IPlayerDependentSaveable Implementation

    /// <summary>
    /// Extract clothing data from unified save structure for modular loading
    /// </summary>
    public object ExtractFromUnifiedSave(PlayerPersistentData unifiedData)
    {
        if (unifiedData == null) return null;

        DebugLog("Using modular extraction from unified save data");

        var clothingData = unifiedData.GetComponentData<ClothingSaveData>(SaveID);
        if (clothingData != null)
        {
            var equippedCount = clothingData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
            DebugLog($"Extracted clothing from dynamic storage: {equippedCount} items equipped");
            return clothingData;
        }

        DebugLog("No clothing data found in unified save - returning empty clothing");
        return new ClothingSaveData();
    }

    /// <summary>
    /// Create default clothing data for new games
    /// </summary>
    public object CreateDefaultData()
    {
        DebugLog("Creating default clothing data for new game");

        var defaultData = new ClothingSaveData();
        // Default constructor creates empty slots, which is what we want for new games

        DebugLog("Default clothing data created: all slots empty");
        return defaultData;
    }

    /// <summary>
    /// Contribute clothing data to unified save structure
    /// </summary>
    public void ContributeToUnifiedSave(object componentData, PlayerPersistentData unifiedData)
    {
        if (componentData is ClothingSaveData clothingData && unifiedData != null)
        {
            var equippedCount = clothingData.slotData?.FindAll(s => s.isOccupied)?.Count ?? 0;
            DebugLog($"Contributing clothing data to unified save structure: {equippedCount} items equipped");

            // Store in dynamic storage
            unifiedData.SetComponentData(SaveID, clothingData);

            DebugLog($"Clothing data contributed: {equippedCount} items stored in dynamic storage");
        }
        else
        {
            DebugLog($"Invalid data for contribution - expected ClothingSaveData, got {componentData?.GetType().Name ?? "null"}");
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Called before save operations
    /// </summary>
    public override void OnBeforeSave()
    {
        DebugLog("Preparing clothing for save");

        // Refresh references in case they changed
        if (autoFindReferences)
        {
            FindClothingReferences();
        }
    }

    /// <summary>
    /// Called after load operations
    /// </summary>
    public override void OnAfterLoad()
    {
        DebugLog("Clothing load completed");

        // Force UI refresh after clothing load
        StartCoroutine(RefreshClothingUIAfterLoad());
    }

    /// <summary>
    /// Force refresh clothing UI after load with proper timing
    /// </summary>
    private IEnumerator RefreshClothingUIAfterLoad()
    {
        // Wait a frame to ensure all managers are fully loaded
        yield return null;

        // Wait for UI systems to be ready
        yield return new WaitForEndOfFrame();

        if (clothingManager != null)
        {
            DebugLog("Forcing clothing UI refresh after load");

            // Force refresh all clothing UI by firing events
            foreach (var slot in clothingManager.AllClothingSlots)
            {
                if (slot.isOccupied)
                {
                    var item = slot.GetEquippedItem();
                    if (item != null)
                    {
                        clothingManager.OnClothingEquipped?.Invoke(slot, item);
                    }
                }
                else
                {
                    clothingManager.OnClothingUnequipped?.Invoke(slot);
                }
            }

            // Fire stats changed event
            clothingManager.OnClothingStatsChanged?.Invoke();

            DebugLog("Clothing UI refresh completed");
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Public method to manually set clothing manager reference
    /// </summary>
    public void SetClothingManager(ClothingManager manager)
    {
        clothingManager = manager;
        autoFindReferences = false; // Disable auto-find when manually set
        DebugLog("Clothing manager reference manually set");
    }

    /// <summary>
    /// Get count of equipped clothing items
    /// </summary>
    public int GetEquippedClothingCount()
    {
        if (clothingManager == null) return 0;

        return clothingManager.AllClothingSlots.Count(s => s.isOccupied);
    }

    /// <summary>
    /// Check if clothing manager reference is valid
    /// </summary>
    public bool HasValidReference()
    {
        return clothingManager != null;
    }

    /// <summary>
    /// Force refresh of clothing manager reference
    /// </summary>
    public void RefreshReference()
    {
        if (autoFindReferences)
        {
            FindClothingReferences();
            ValidateReferences();
        }
    }

    /// <summary>
    /// Get debug information about current clothing state
    /// </summary>
    public string GetClothingDebugInfo()
    {
        if (clothingManager == null)
            return "ClothingManager: null";

        var equippedCount = GetEquippedClothingCount();
        var totalSlots = clothingManager.AllClothingSlots.Count;
        var stats = clothingManager.TotalStats;

        return $"Clothing: {equippedCount}/{totalSlots} slots occupied, Stats: W{stats.warmth:F0}/D{stats.defense:F0}/R{stats.rain:F0}/S{stats.speed:F2}";
    }

    /// <summary>
    /// Get clothing item in specific slot
    /// </summary>
    public string GetSlotItemName(string slotId)
    {
        if (clothingManager == null) return null;

        var slot = clothingManager.GetSlotById(slotId);
        if (slot?.isOccupied == true)
        {
            return slot.GetEquippedItem()?.ItemData?.itemName;
        }

        return null;
    }

    /// <summary>
    /// Check if specific slot is occupied
    /// </summary>
    public bool IsSlotOccupied(string slotId)
    {
        if (clothingManager == null) return false;

        var slot = clothingManager.GetSlotById(slotId);
        return slot?.isOccupied == true;
    }

    /// <summary>
    /// Get total protection values
    /// </summary>
    public (float warmth, float defense, float rain, float speed) GetTotalProtection()
    {
        if (clothingManager == null) return (0f, 0f, 0f, 0f);

        return clothingManager.TotalStats;
    }

    #endregion
}