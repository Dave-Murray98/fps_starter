using UnityEngine;

/// <summary>
/// Represents a single clothing slot that can hold one piece of clothing.
/// Handles slot-specific logic, validation, and state management.
/// Pure data class without visual dependencies.
/// </summary>
[System.Serializable]
public class ClothingSlot
{
    [Header("Slot Configuration")]
    [Tooltip("Type of clothing this slot accepts")]
    public ClothingType acceptedClothingType;

    [Tooltip("Which layer this slot represents")]
    public ClothingLayer slotLayer;

    [Tooltip("Display name for this slot")]
    public string slotName;

    [Tooltip("Unique identifier for this slot")]
    public string slotID;

    [Header("Current State")]
    [Tooltip("ID of the inventory item currently equipped in this slot")]
    public string equippedItemID = "";

    [Tooltip("Is this slot currently occupied?")]
    public bool isOccupied = false;

    // Cached references for performance
    private InventoryItemData _cachedEquippedItem;
    private ClothingData _cachedClothingData;
    private bool _dataCached = false;

    // Events for when slot state changes
    public System.Action<ClothingSlot, InventoryItemData> OnItemEquipped;
    public System.Action<ClothingSlot> OnItemRemoved;

    /// <summary>
    /// Constructor for creating a clothing slot
    /// </summary>
    public ClothingSlot(ClothingType clothingType, ClothingLayer layer, string name, string id)
    {
        acceptedClothingType = clothingType;
        slotLayer = layer;
        slotName = name;
        slotID = id;
        ClearSlot();
    }

    /// <summary>
    /// Copy constructor for save/load operations
    /// </summary>
    public ClothingSlot(ClothingSlot other)
    {
        acceptedClothingType = other.acceptedClothingType;
        slotLayer = other.slotLayer;
        slotName = other.slotName;
        slotID = other.slotID;
        equippedItemID = other.equippedItemID;
        isOccupied = other.isOccupied;
        _dataCached = false; // Force refresh of cached data
    }

    /// <summary>
    /// Default constructor for serialization
    /// </summary>
    public ClothingSlot()
    {
        ClearSlot();
    }

    /// <summary>
    /// Gets the currently equipped inventory item (with caching)
    /// </summary>
    public InventoryItemData GetEquippedItem()
    {
        if (!isOccupied || string.IsNullOrEmpty(equippedItemID))
            return null;

        // Use cached data if available and ID matches
        if (_dataCached && _cachedEquippedItem?.ID == equippedItemID)
            return _cachedEquippedItem;

        // Refresh cache
        RefreshCachedData();
        return _cachedEquippedItem;
    }

    /// <summary>
    /// Gets the clothing data for the equipped item (with caching)
    /// </summary>
    public ClothingData GetEquippedClothingData()
    {
        if (!isOccupied)
            return null;

        // Use cached data if available
        if (_dataCached && _cachedClothingData != null)
            return _cachedClothingData;

        // Refresh cache
        RefreshCachedData();
        return _cachedClothingData;
    }

    /// <summary>
    /// Refresh cached item and clothing data
    /// </summary>
    private void RefreshCachedData()
    {
        _cachedEquippedItem = null;
        _cachedClothingData = null;

        if (!isOccupied || string.IsNullOrEmpty(equippedItemID))
        {
            _dataCached = true;
            return;
        }

        // Get item from inventory
        if (InventoryManager.Instance != null)
        {
            _cachedEquippedItem = InventoryManager.Instance.InventoryData.GetItem(equippedItemID);

            if (_cachedEquippedItem?.ItemData != null)
            {
                // Ensure it's a clothing item
                if (_cachedEquippedItem.ItemData.itemType == ItemType.Clothing)
                {
                    _cachedClothingData = _cachedEquippedItem.ItemData.ClothingData;
                }
            }
        }

        _dataCached = true;
    }

    /// <summary>
    /// Check if an inventory item can be equipped in this slot
    /// </summary>
    public bool CanEquipItem(InventoryItemData item)
    {
        if (item?.ItemData == null)
            return false;

        // Must be a clothing item
        if (item.ItemData.itemType != ItemType.Clothing)
            return false;

        var clothingData = item.ItemData.ClothingData;
        if (clothingData == null)
            return false;

        // Must match clothing type
        if (clothingData.clothingType != acceptedClothingType)
            return false;

        // Must be valid for this layer
        if (!clothingData.CanEquipInLayer(slotLayer))
            return false;

        // Cannot equip destroyed items
        if (clothingData.IsDestroyed)
            return false;

        return true;
    }

    /// <summary>
    /// Equip an item in this slot
    /// </summary>
    public bool EquipItem(InventoryItemData item)
    {
        if (!CanEquipItem(item))
            return false;

        // Clear current item if any
        var previousItem = GetEquippedItem();

        // Set new item
        equippedItemID = item.ID;
        isOccupied = true;
        _dataCached = false; // Force cache refresh

        // Initialize clothing durability if needed
        if (item.ItemData.ClothingData.currentDurability <= 0)
        {
            item.ItemData.ClothingData.InitializeDurability();
        }

        // Fire events
        OnItemEquipped?.Invoke(this, item);

        return true;
    }

    /// <summary>
    /// Remove the currently equipped item
    /// </summary>
    public InventoryItemData UnequipItem()
    {
        if (!isOccupied)
            return null;

        var item = GetEquippedItem();
        ClearSlot();

        // Fire event
        OnItemRemoved?.Invoke(this);

        return item;
    }

    /// <summary>
    /// Clear this slot completely
    /// </summary>
    public void ClearSlot()
    {
        equippedItemID = "";
        isOccupied = false;
        _cachedEquippedItem = null;
        _cachedClothingData = null;
        _dataCached = true;
    }

    /// <summary>
    /// Swap items with another slot
    /// </summary>
    public bool SwapWith(ClothingSlot otherSlot)
    {
        if (otherSlot == null)
            return false;

        var myItem = GetEquippedItem();
        var theirItem = otherSlot.GetEquippedItem();

        // Check if swap is valid
        bool canEquipTheirs = (theirItem == null) || CanEquipItem(theirItem);
        bool canEquipMine = (myItem == null) || otherSlot.CanEquipItem(myItem);

        if (!canEquipTheirs || !canEquipMine)
            return false;

        // Perform swap
        ClearSlot();
        otherSlot.ClearSlot();

        if (theirItem != null)
            EquipItem(theirItem);

        if (myItem != null)
            otherSlot.EquipItem(myItem);

        return true;
    }

    /// <summary>
    /// Get protection values from equipped clothing
    /// </summary>
    public (float warmth, float defense, float rain, float speed) GetProtectionValues()
    {
        var clothingData = GetEquippedClothingData();
        if (clothingData == null)
            return (0f, 0f, 0f, 0f);

        return (
            clothingData.GetEffectiveWarmth(),
            clothingData.GetEffectiveDefense(),
            clothingData.GetEffectiveRainProtection(),
            clothingData.GetEffectiveSpeedModifier()
        );
    }

    /// <summary>
    /// Apply damage to equipped clothing
    /// </summary>
    public void TakeDamage(float damage)
    {
        var clothingData = GetEquippedClothingData();
        if (clothingData != null)
        {
            clothingData.TakeDamage(damage);

            // If item is destroyed, remove it
            if (clothingData.IsDestroyed)
            {
                UnequipItem();
            }
        }
    }

    /// <summary>
    /// Apply wear to equipped clothing
    /// </summary>
    public void ApplyWear(float hoursWorn)
    {
        var clothingData = GetEquippedClothingData();
        if (clothingData != null)
        {
            clothingData.ApplyWear(hoursWorn);

            // If item is destroyed, remove it
            if (clothingData.IsDestroyed)
            {
                UnequipItem();
            }
        }
    }

    /// <summary>
    /// Repair equipped clothing
    /// </summary>
    public float RepairEquippedItem(float repairAmount)
    {
        var clothingData = GetEquippedClothingData();
        if (clothingData != null)
        {
            return clothingData.RepairItem(repairAmount);
        }
        return 0f;
    }

    /// <summary>
    /// Set wetness of equipped clothing
    /// </summary>
    public void SetWetness(bool wet)
    {
        var clothingData = GetEquippedClothingData();
        if (clothingData != null)
        {
            clothingData.SetWetness(wet);
        }
    }

    /// <summary>
    /// Check if equipped clothing should get wet
    /// </summary>
    public bool ShouldGetWet(float rainIntensity)
    {
        var clothingData = GetEquippedClothingData();
        if (clothingData != null)
        {
            return clothingData.ShouldGetWet(rainIntensity);
        }
        return true; // No protection = always get wet
    }

    /// <summary>
    /// Validate that this slot's data is consistent
    /// </summary>
    public bool IsValid()
    {
        // If not occupied, should have no item ID
        if (!isOccupied && !string.IsNullOrEmpty(equippedItemID))
            return false;

        // If occupied, should have an item ID
        if (isOccupied && string.IsNullOrEmpty(equippedItemID))
            return false;

        // If occupied, item should exist and be valid
        if (isOccupied)
        {
            var item = GetEquippedItem();
            if (item == null || !CanEquipItem(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get debug information about this slot
    /// </summary>
    public string GetDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"Slot: {slotName} ({slotID})");
        info.AppendLine($"Type: {acceptedClothingType}, Layer: {slotLayer}");
        info.AppendLine($"Occupied: {isOccupied}");

        if (isOccupied)
        {
            var item = GetEquippedItem();
            if (item != null)
            {
                info.AppendLine($"Item: {item.ItemData?.itemName ?? "Unknown"}");
                var clothingData = GetEquippedClothingData();
                if (clothingData != null)
                {
                    info.AppendLine($"Condition: {clothingData.GetConditionDescription()}");
                    info.AppendLine($"Durability: {clothingData.currentDurability:F1}/{clothingData.maxDurability:F1}");
                    info.AppendLine($"Wet: {clothingData.isWet}");
                }
            }
            else
            {
                info.AppendLine("ERROR: Occupied but no item found!");
            }
        }

        return info.ToString();
    }

    /// <summary>
    /// Convert to save data format
    /// </summary>
    public ClothingSlotSaveData ToSaveData()
    {
        return new ClothingSlotSaveData
        {
            slotID = slotID,
            equippedItemID = equippedItemID,
            isOccupied = isOccupied
        };
    }

    /// <summary>
    /// Load from save data format
    /// </summary>
    public void FromSaveData(ClothingSlotSaveData saveData)
    {
        if (saveData.slotID != slotID)
        {
            Debug.LogWarning($"Slot ID mismatch: expected {slotID}, got {saveData.slotID}");
            return;
        }

        equippedItemID = saveData.equippedItemID;
        isOccupied = saveData.isOccupied;
        _dataCached = false; // Force refresh
    }
}