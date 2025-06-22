using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Core clothing system manager handling equipment, stats, and UI coordination.
/// Follows the same patterns as InventoryManager for consistency and modularity.
/// Persists across scenes and fires events for UI synchronization.
/// </summary>
public class ClothingManager : MonoBehaviour
{
    public static ClothingManager Instance { get; private set; }

    [Header("Clothing Slots Configuration")]
    [SerializeField] private ClothingSlot[] clothingSlots;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Events for UI synchronization (following InventoryManager pattern)
    public event Action<ClothingSlot, InventoryItemData> OnItemEquipped;
    public event Action<ClothingSlot, string> OnItemUnequipped;
    public event Action<string, float> OnClothingConditionChanged;
    public event Action OnClothingDataChanged;

    // Cached stats for performance
    private float cachedTotalDefense = 0f;
    private float cachedTotalWarmth = 0f;
    private float cachedTotalRainResistance = 0f;
    private bool statsCacheValid = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeClothingSlots();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Subscribe to inventory events to handle item removal
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemRemoved += OnInventoryItemRemoved;
        }
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnItemRemoved -= OnInventoryItemRemoved;
        }
    }

    /// <summary>
    /// Initialize the clothing slots array with all available layers
    /// </summary>
    private void InitializeClothingSlots()
    {
        if (clothingSlots == null || clothingSlots.Length == 0)
        {
            DebugLog("Initializing default clothing slots");

            clothingSlots = new ClothingSlot[]
            {
                new ClothingSlot(ClothingLayer.HeadUpper),
                new ClothingSlot(ClothingLayer.HeadLower),
                new ClothingSlot(ClothingLayer.TorsoInner),
                new ClothingSlot(ClothingLayer.TorsoOuter),
                new ClothingSlot(ClothingLayer.LegsInner),
                new ClothingSlot(ClothingLayer.LegsOuter),
                new ClothingSlot(ClothingLayer.Hands),
                new ClothingSlot(ClothingLayer.Socks),
                new ClothingSlot(ClothingLayer.Shoes)
            };
        }

        InvalidateStatsCache();
        DebugLog($"Clothing system initialized with {clothingSlots.Length} slots");
    }

    /// <summary>
    /// Equips an item from inventory to the specified clothing layer
    /// </summary>
    public bool EquipItemToLayer(string itemId, ClothingLayer targetLayer)
    {
        if (InventoryManager.Instance == null)
        {
            DebugLog("Cannot equip item - InventoryManager not found");
            return false;
        }

        var item = InventoryManager.Instance.InventoryData.GetItem(itemId);
        if (item == null)
        {
            DebugLog($"Cannot equip item - item {itemId} not found in inventory");
            return false;
        }

        if (item.ItemData?.itemType != ItemType.Clothing)
        {
            DebugLog($"Cannot equip item {itemId} - not a clothing item");
            return false;
        }

        var clothingData = item.ItemData.ClothingData;
        if (clothingData == null)
        {
            DebugLog($"Cannot equip item {itemId} - no clothing data");
            return false;
        }

        if (!clothingData.CanEquipToLayer(targetLayer))
        {
            DebugLog($"Cannot equip {item.ItemData.itemName} to layer {targetLayer} - not compatible");
            return false;
        }

        var targetSlot = GetSlot(targetLayer);
        if (targetSlot == null)
        {
            DebugLog($"Cannot equip item - no slot found for layer {targetLayer}");
            return false;
        }

        // Handle slot swapping if occupied
        string previousItemId = null;
        if (targetSlot.IsOccupied)
        {
            previousItemId = targetSlot.UnequipItem();
            DebugLog($"Unequipped {previousItemId} from {targetLayer} for swapping");
        }

        // Equip the new item
        targetSlot.EquipItem(itemId);
        InvalidateStatsCache();

        // Fire events
        OnItemEquipped?.Invoke(targetSlot, item);
        OnClothingDataChanged?.Invoke();

        DebugLog($"Equipped {item.ItemData.itemName} to {targetLayer}");

        // If there was a previous item, it stays in inventory (swap behavior)
        if (!string.IsNullOrEmpty(previousItemId))
        {
            DebugLog($"Swapped items - {previousItemId} returned to inventory");
        }

        return true;
    }

    /// <summary>
    /// Unequips an item from the specified layer, returning it to inventory
    /// </summary>
    public bool UnequipItemFromLayer(ClothingLayer layer)
    {
        var slot = GetSlot(layer);
        if (slot == null || slot.IsEmpty)
        {
            DebugLog($"Cannot unequip from {layer} - slot empty or not found");
            return false;
        }

        string itemId = slot.UnequipItem();
        InvalidateStatsCache();

        // Fire events
        OnItemUnequipped?.Invoke(slot, itemId);
        OnClothingDataChanged?.Invoke();

        DebugLog($"Unequipped item {itemId} from {layer}");
        return true;
    }

    /// <summary>
    /// Gets the clothing slot for the specified layer
    /// </summary>
    public ClothingSlot GetSlot(ClothingLayer layer)
    {
        return System.Array.Find(clothingSlots, slot => slot.layer == layer);
    }

    /// <summary>
    /// Gets all clothing slots
    /// </summary>
    public ClothingSlot[] GetAllSlots()
    {
        return clothingSlots;
    }

    /// <summary>
    /// Gets all currently equipped items
    /// </summary>
    public List<InventoryItemData> GetEquippedItems()
    {
        var equippedItems = new List<InventoryItemData>();

        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                var item = slot.GetEquippedItem();
                if (item != null)
                {
                    equippedItems.Add(item);
                }
            }
        }

        return equippedItems;
    }

    /// <summary>
    /// Checks if an item is currently equipped in any slot
    /// </summary>
    public bool IsItemEquipped(string itemId)
    {
        return System.Array.Exists(clothingSlots, slot => slot.equippedItemId == itemId);
    }

    /// <summary>
    /// Gets the slot where the specified item is equipped, or null if not equipped
    /// </summary>
    public ClothingSlot GetSlotForItem(string itemId)
    {
        return System.Array.Find(clothingSlots, slot => slot.equippedItemId == itemId);
    }

    #region Stats Calculation

    /// <summary>
    /// Gets total defense value from all equipped clothing
    /// </summary>
    public float GetTotalDefense()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalDefense;
    }

    /// <summary>
    /// Gets total warmth value from all equipped clothing
    /// </summary>
    public float GetTotalWarmth()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalWarmth;
    }

    /// <summary>
    /// Gets total rain resistance from all equipped clothing
    /// </summary>
    public float GetTotalRainResistance()
    {
        if (!statsCacheValid)
            RecalculateStats();
        return cachedTotalRainResistance;
    }

    /// <summary>
    /// Recalculates all clothing stats from equipped items
    /// </summary>
    private void RecalculateStats()
    {
        cachedTotalDefense = 0f;
        cachedTotalWarmth = 0f;
        cachedTotalRainResistance = 0f;

        foreach (var slot in clothingSlots)
        {
            var clothingData = slot.GetEquippedClothingData();
            if (clothingData != null)
            {
                cachedTotalDefense += clothingData.GetEffectiveDefense();
                cachedTotalWarmth += clothingData.GetEffectiveWarmth();
                cachedTotalRainResistance += clothingData.GetEffectiveRainResistance();
            }
        }

        statsCacheValid = true;
        DebugLog($"Recalculated stats - Defense: {cachedTotalDefense:F1}, Warmth: {cachedTotalWarmth:F1}, Rain: {cachedTotalRainResistance:F1}");
    }

    /// <summary>
    /// Invalidates the stats cache, forcing recalculation on next access
    /// </summary>
    private void InvalidateStatsCache()
    {
        statsCacheValid = false;
    }

    #endregion

    #region Damage and Condition

    /// <summary>
    /// Applies damage to equipped clothing when player takes damage
    /// </summary>
    public void OnPlayerTakeDamage(float damageAmount)
    {
        // Random chance to damage clothing
        float damageChance = 0.25f; // 25% chance per damage event

        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied && UnityEngine.Random.value < damageChance)
            {
                var clothingData = slot.GetEquippedClothingData();
                if (clothingData != null)
                {
                    float conditionBefore = clothingData.currentCondition;
                    clothingData.TakeDamage();

                    if (clothingData.currentCondition != conditionBefore)
                    {
                        OnClothingConditionChanged?.Invoke(slot.equippedItemId, clothingData.currentCondition);
                        InvalidateStatsCache();

                        DebugLog($"Clothing {slot.equippedItemId} damaged - condition now {clothingData.currentCondition:F1}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Simple repair method for changing clothing condition
    /// </summary>
    public bool RepairClothingItem(string itemId, float repairAmount)
    {
        var slot = GetSlotForItem(itemId);
        if (slot == null)
        {
            DebugLog($"Cannot repair {itemId} - item not equipped");
            return false;
        }

        var clothingData = slot.GetEquippedClothingData();
        if (clothingData == null)
        {
            DebugLog($"Cannot repair {itemId} - no clothing data");
            return false;
        }

        float conditionBefore = clothingData.currentCondition;
        bool success = clothingData.RepairCondition(repairAmount);

        if (success && clothingData.currentCondition != conditionBefore)
        {
            OnClothingConditionChanged?.Invoke(itemId, clothingData.currentCondition);
            InvalidateStatsCache();

            DebugLog($"Repaired {itemId} - condition now {clothingData.currentCondition:F1}");
        }

        return success;
    }

    #endregion

    #region Data Management

    /// <summary>
    /// Sets clothing data directly (used by save system)
    /// </summary>
    public void SetClothingData(ClothingSlot[] newSlots)
    {
        clothingSlots = newSlots ?? new ClothingSlot[0];
        InvalidateStatsCache();
        OnClothingDataChanged?.Invoke();

        DebugLog($"Clothing data set with {clothingSlots.Length} slots");
    }

    /// <summary>
    /// Clears all equipped clothing
    /// </summary>
    public void ClearAllClothing()
    {
        foreach (var slot in clothingSlots)
        {
            if (slot.IsOccupied)
            {
                string itemId = slot.UnequipItem();
                OnItemUnequipped?.Invoke(slot, itemId);
            }
        }

        InvalidateStatsCache();
        OnClothingDataChanged?.Invoke();

        DebugLog("All clothing cleared");
    }

    /// <summary>
    /// Handles inventory item removal - unequip if currently equipped
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        var slot = GetSlotForItem(itemId);
        if (slot != null)
        {
            DebugLog($"Item {itemId} removed from inventory - unequipping from {slot.layer}");
            slot.UnequipItem();
            InvalidateStatsCache();
            OnItemUnequipped?.Invoke(slot, itemId);
            OnClothingDataChanged?.Invoke();
        }
    }

    #endregion

    #region Debug Methods

    [Button("Debug Clothing Stats")]
    private void DebugClothingStats()
    {
        DebugLog("=== CLOTHING STATS DEBUG ===");
        DebugLog($"Total Defense: {GetTotalDefense():F1}");
        DebugLog($"Total Warmth: {GetTotalWarmth():F1}");
        DebugLog($"Total Rain Resistance: {GetTotalRainResistance():F1}");

        DebugLog("\n=== EQUIPPED ITEMS ===");
        foreach (var slot in clothingSlots)
        {
            DebugLog(slot.GetDebugInfo());
        }
    }

    [Button("Clear All Clothing")]
    private void DebugClearAllClothing()
    {
        ClearAllClothing();
    }

    [Button("Damage Random Clothing")]
    private void DebugDamageRandomClothing()
    {
        OnPlayerTakeDamage(10f);
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ClothingManager] {message}");
        }
    }

    #endregion
}