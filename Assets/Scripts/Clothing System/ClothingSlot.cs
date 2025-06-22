using UnityEngine;

/// <summary>
/// Represents a single clothing slot where items can be equipped.
/// Handles validation and state management for each equippable layer.
/// </summary>
[System.Serializable]
public class ClothingSlot
{
    [Header("Slot Configuration")]
    [Tooltip("Which clothing layer this slot represents")]
    public ClothingLayer layer;

    [Tooltip("Display name for this slot in the UI")]
    public string displayName;

    [Header("Current State")]
    [Tooltip("ID of the currently equipped item (empty if none)")]
    public string equippedItemId = "";

    /// <summary>
    /// Checks if this slot is currently empty
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(equippedItemId);

    /// <summary>
    /// Checks if this slot has an item equipped
    /// </summary>
    public bool IsOccupied => !IsEmpty;

    /// <summary>
    /// Constructor for creating a clothing slot
    /// </summary>
    public ClothingSlot(ClothingLayer slotLayer)
    {
        layer = slotLayer;
        displayName = GetDefaultDisplayName(slotLayer);
        equippedItemId = "";
    }

    /// <summary>
    /// Constructor with custom display name
    /// </summary>
    public ClothingSlot(ClothingLayer slotLayer, string customDisplayName)
    {
        layer = slotLayer;
        displayName = customDisplayName;
        equippedItemId = "";
    }

    /// <summary>
    /// Checks if the specified clothing item can be equipped to this slot
    /// </summary>
    public bool CanEquip(ClothingData clothingData)
    {
        if (clothingData == null)
            return false;

        return System.Array.Exists(clothingData.validLayers, l => l == layer);
    }

    /// <summary>
    /// Checks if the specified inventory item can be equipped to this slot
    /// </summary>
    public bool CanEquip(InventoryItemData inventoryItem)
    {
        if (inventoryItem?.ItemData?.itemType != ItemType.Clothing)
            return false;

        var clothingData = inventoryItem.ItemData.ClothingData;
        return CanEquip(clothingData);
    }

    /// <summary>
    /// Equips an item to this slot
    /// </summary>
    public void EquipItem(string itemId)
    {
        equippedItemId = itemId;
    }

    /// <summary>
    /// Unequips the current item from this slot
    /// </summary>
    public string UnequipItem()
    {
        string previousItemId = equippedItemId;
        equippedItemId = "";
        return previousItemId;
    }

    /// <summary>
    /// Gets the currently equipped item from the inventory system
    /// </summary>
    public InventoryItemData GetEquippedItem()
    {
        if (IsEmpty || InventoryManager.Instance == null)
            return null;

        return InventoryManager.Instance.InventoryData.GetItem(equippedItemId);
    }

    /// <summary>
    /// Gets the clothing data of the currently equipped item
    /// </summary>
    public ClothingData GetEquippedClothingData()
    {
        var equippedItem = GetEquippedItem();
        return equippedItem?.ItemData?.ClothingData;
    }

    /// <summary>
    /// Gets the display name for the equipped item, or "Empty" if none
    /// </summary>
    public string GetEquippedItemDisplayName()
    {
        var equippedItem = GetEquippedItem();
        return equippedItem?.ItemData?.itemName ?? "Empty";
    }

    /// <summary>
    /// Gets the condition percentage of the equipped item (0-1)
    /// </summary>
    public float GetEquippedItemCondition()
    {
        var clothingData = GetEquippedClothingData();
        return clothingData?.ConditionPercentage ?? 0f;
    }

    /// <summary>
    /// Checks if the equipped item is damaged (condition below 50%)
    /// </summary>
    public bool IsEquippedItemDamaged()
    {
        var clothingData = GetEquippedClothingData();
        return clothingData?.IsDamaged ?? false;
    }

    /// <summary>
    /// Gets default display names for each clothing layer
    /// </summary>
    private string GetDefaultDisplayName(ClothingLayer slotLayer)
    {
        return slotLayer switch
        {
            ClothingLayer.HeadUpper => "Head (Upper)",
            ClothingLayer.HeadLower => "Head (Lower)",
            ClothingLayer.TorsoInner => "Torso (Inner)",
            ClothingLayer.TorsoOuter => "Torso (Outer)",
            ClothingLayer.LegsInner => "Legs (Inner)",
            ClothingLayer.LegsOuter => "Legs (Outer)",
            ClothingLayer.Hands => "Hands",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => slotLayer.ToString()
        };
    }

    /// <summary>
    /// Gets a short display name for UI space constraints
    /// </summary>
    public string GetShortDisplayName()
    {
        return layer switch
        {
            ClothingLayer.HeadUpper => "Hat",
            ClothingLayer.HeadLower => "Scarf",
            ClothingLayer.TorsoInner => "Shirt",
            ClothingLayer.TorsoOuter => "Jacket",
            ClothingLayer.LegsInner => "Underwear",
            ClothingLayer.LegsOuter => "Pants",
            ClothingLayer.Hands => "Gloves",
            ClothingLayer.Socks => "Socks",
            ClothingLayer.Shoes => "Shoes",
            _ => displayName
        };
    }

    /// <summary>
    /// Creates a copy of this clothing slot
    /// </summary>
    public ClothingSlot CreateCopy()
    {
        return new ClothingSlot(layer, displayName)
        {
            equippedItemId = this.equippedItemId
        };
    }

    /// <summary>
    /// Validates that this slot's state is consistent
    /// </summary>
    public bool IsValid()
    {
        // If we have an equipped item, verify it exists in inventory
        if (IsOccupied && InventoryManager.Instance != null)
        {
            var item = InventoryManager.Instance.InventoryData.GetItem(equippedItemId);
            if (item == null)
            {
                Debug.LogWarning($"Clothing slot {layer} references non-existent item {equippedItemId}");
                return false;
            }

            // Verify the item can actually be equipped to this slot
            if (!CanEquip(item))
            {
                Debug.LogWarning($"Item {equippedItemId} cannot be equipped to slot {layer}");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets debug information about this slot
    /// </summary>
    public string GetDebugInfo()
    {
        if (IsEmpty)
            return $"{layer}: Empty";

        var equippedItem = GetEquippedItem();
        if (equippedItem == null)
            return $"{layer}: {equippedItemId} (MISSING!)";

        var condition = GetEquippedItemCondition();
        return $"{layer}: {equippedItem.ItemData?.itemName} ({condition:P0} condition)";
    }
}