using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

/// <summary>
/// Core manager for the clothing system. Handles equipped clothing slots,
/// stat calculations, damage distribution, and integration with inventory.
/// Follows the same pattern as EquippedItemManager for consistency.
/// </summary>
public class ClothingManager : MonoBehaviour
{
    public static ClothingManager Instance { get; private set; }

    [Header("Clothing System State")]
    [SerializeField, ReadOnly] private List<ClothingSlot> clothingSlots = new List<ClothingSlot>();

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float wearUpdateInterval = 300f; // 5 minutes in seconds

    [Header("Audio")]
    [SerializeField] private AudioClip equipClothingSound;
    [SerializeField] private AudioClip unequipClothingSound;

    // Component references
    private InventoryManager inventoryManager;
    private PlayerManager playerManager;

    // Wear tracking
    private float lastWearUpdate;
    private Dictionary<string, float> wearAccumulator = new Dictionary<string, float>();

    // Current stat totals (cached)
    private float totalWarmth = 0f;
    private float totalDefense = 0f;
    private float totalRainProtection = 0f;
    private float totalSpeedModifier = 0f;
    private bool statsCached = false;

    // Events for UI and external systems
    public System.Action<ClothingSlot, InventoryItemData> OnClothingEquipped;
    public System.Action<ClothingSlot> OnClothingUnequipped;
    public System.Action<ClothingSlot> OnClothingDamaged;
    public System.Action OnClothingStatsChanged;

    // Public accessors
    public List<ClothingSlot> AllClothingSlots => new List<ClothingSlot>(clothingSlots);
    public (float warmth, float defense, float rain, float speed) TotalStats => GetTotalStats();

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
        RefreshReferences();
        SubscribeToEvents();
        lastWearUpdate = Time.time;
    }

    /// <summary>
    /// Initialize all clothing slots with proper configuration
    /// </summary>
    private void InitializeClothingSlots()
    {
        clothingSlots.Clear();

        // Head slots (2)
        clothingSlots.Add(new ClothingSlot(ClothingType.Head, ClothingLayer.Upper, "Head Upper", "head_upper"));
        clothingSlots.Add(new ClothingSlot(ClothingType.Head, ClothingLayer.Lower, "Head Lower", "head_lower"));

        // Torso slots (2)
        clothingSlots.Add(new ClothingSlot(ClothingType.Torso, ClothingLayer.Lower, "Torso Inner", "torso_inner"));
        clothingSlots.Add(new ClothingSlot(ClothingType.Torso, ClothingLayer.Upper, "Torso Outer", "torso_outer"));

        // Hand slot (1)
        clothingSlots.Add(new ClothingSlot(ClothingType.Hands, ClothingLayer.Single, "Hands", "hands"));

        // Leg slots (2)
        clothingSlots.Add(new ClothingSlot(ClothingType.Legs, ClothingLayer.Lower, "Legs Inner", "legs_inner"));
        clothingSlots.Add(new ClothingSlot(ClothingType.Legs, ClothingLayer.Upper, "Legs Outer", "legs_outer"));

        // Foot slots (2)
        clothingSlots.Add(new ClothingSlot(ClothingType.Socks, ClothingLayer.Single, "Socks", "socks"));
        clothingSlots.Add(new ClothingSlot(ClothingType.Shoes, ClothingLayer.Single, "Shoes", "shoes"));

        // Subscribe to slot events
        foreach (var slot in clothingSlots)
        {
            slot.OnItemEquipped += OnSlotItemEquipped;
            slot.OnItemRemoved += OnSlotItemRemoved;
        }

        DebugLog($"Initialized {clothingSlots.Count} clothing slots");
    }

    /// <summary>
    /// Refresh component references after scene changes
    /// </summary>
    private void RefreshReferences()
    {
        inventoryManager = InventoryManager.Instance;
        playerManager = GameManager.Instance?.playerManager;
    }

    /// <summary>
    /// Subscribe to external events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved += OnInventoryItemRemoved;
        }

        GameManager.OnManagersRefreshed += RefreshReferences;
    }

    private void Update()
    {
        UpdateWearOverTime();
    }

    /// <summary>
    /// Update wear on all equipped clothing over time
    /// </summary>
    private void UpdateWearOverTime()
    {
        if (Time.time - lastWearUpdate < wearUpdateInterval)
            return;

        float hoursElapsed = (Time.time - lastWearUpdate) / 3600f; // Convert seconds to hours
        lastWearUpdate = Time.time;

        bool anyClothingDamaged = false;

        foreach (var slot in clothingSlots)
        {
            if (slot.isOccupied)
            {
                var item = slot.GetEquippedItem();
                if (item != null)
                {
                    float currentDurability = slot.GetEquippedClothingData()?.currentDurability ?? 0f;
                    slot.ApplyWear(hoursElapsed);

                    // Check if durability changed
                    float newDurability = slot.GetEquippedClothingData()?.currentDurability ?? 0f;
                    if (newDurability < currentDurability)
                    {
                        anyClothingDamaged = true;
                        DebugLog($"Clothing in {slot.slotName} wore down: {newDurability:F1}/{slot.GetEquippedClothingData()?.maxDurability:F1}");
                    }
                }
            }
        }

        if (anyClothingDamaged)
        {
            InvalidateStatsCache();
            OnClothingStatsChanged?.Invoke();
        }
    }

    #region Equipment Management

    /// <summary>
    /// Equip a clothing item from inventory to the best available slot
    /// </summary>
    public bool EquipClothingFromInventory(string itemId)
    {
        if (inventoryManager == null)
        {
            DebugLog("Cannot equip clothing - inventory manager not found");
            return false;
        }

        var inventoryItem = inventoryManager.InventoryData.GetItem(itemId);
        if (inventoryItem?.ItemData == null || inventoryItem.ItemData.itemType != ItemType.Clothing)
        {
            DebugLog($"Cannot equip clothing - item {itemId} not found or not clothing");
            return false;
        }

        var clothingData = inventoryItem.ItemData.ClothingData;
        if (clothingData == null)
        {
            DebugLog($"Cannot equip clothing - no clothing data for {itemId}");
            return false;
        }

        // Find best slot for this item
        var availableSlots = GetAvailableSlotsForItem(inventoryItem);
        if (availableSlots.Count == 0)
        {
            DebugLog($"Cannot equip clothing - no available slots for {inventoryItem.ItemData.itemName}");
            return false;
        }

        // Prefer empty slots, then outer layers
        var targetSlot = availableSlots
            .OrderBy(s => s.isOccupied ? 1 : 0)
            .ThenBy(s => s.slotLayer == ClothingLayer.Upper ? 0 : 1)
            .First();

        return EquipClothingToSlot(itemId, targetSlot.slotID);
    }

    /// <summary>
    /// Equip a clothing item to a specific slot
    /// </summary>
    public bool EquipClothingToSlot(string itemId, string slotId)
    {
        var slot = GetSlotById(slotId);
        if (slot == null)
        {
            DebugLog($"Cannot equip clothing - slot {slotId} not found");
            return false;
        }

        if (inventoryManager == null)
        {
            DebugLog("Cannot equip clothing - inventory manager not found");
            return false;
        }

        var inventoryItem = inventoryManager.InventoryData.GetItem(itemId);
        if (inventoryItem == null)
        {
            DebugLog($"Cannot equip clothing - item {itemId} not found in inventory");
            return false;
        }

        if (!slot.CanEquipItem(inventoryItem))
        {
            DebugLog($"Cannot equip {inventoryItem.ItemData?.itemName} to {slot.slotName} - invalid for slot");
            return false;
        }

        // Handle swapping if slot is occupied
        var displacedItem = slot.GetEquippedItem();

        // Equip the new item
        bool success = slot.EquipItem(inventoryItem);

        if (success)
        {
            DebugLog($"Equipped {inventoryItem.ItemData.itemName} to {slot.slotName}");
            PlayEquipSound();

            // If we displaced an item, it stays in inventory (swap behavior)
            if (displacedItem != null)
            {
                DebugLog($"Displaced {displacedItem.ItemData?.itemName} from {slot.slotName}");
            }

            InvalidateStatsCache();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unequip clothing from a specific slot
    /// </summary>
    public bool UnequipClothingFromSlot(string slotId)
    {
        var slot = GetSlotById(slotId);
        if (slot == null || !slot.isOccupied)
        {
            return false;
        }

        var item = slot.UnequipItem();
        if (item != null)
        {
            DebugLog($"Unequipped {item.ItemData?.itemName} from {slot.slotName}");
            PlayUnequipSound();
            InvalidateStatsCache();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get all slots that can accept a specific clothing item
    /// </summary>
    public List<ClothingSlot> GetAvailableSlotsForItem(InventoryItemData item)
    {
        return clothingSlots.Where(slot => slot.CanEquipItem(item)).ToList();
    }

    /// <summary>
    /// Get slot by ID
    /// </summary>
    public ClothingSlot GetSlotById(string slotId)
    {
        return clothingSlots.FirstOrDefault(s => s.slotID == slotId);
    }

    /// <summary>
    /// Get slots by clothing type
    /// </summary>
    public List<ClothingSlot> GetSlotsByType(ClothingType clothingType)
    {
        return clothingSlots.Where(s => s.acceptedClothingType == clothingType).ToList();
    }

    /// <summary>
    /// Get all equipped clothing items
    /// </summary>
    public List<InventoryItemData> GetAllEquippedClothing()
    {
        var items = new List<InventoryItemData>();
        foreach (var slot in clothingSlots)
        {
            var item = slot.GetEquippedItem();
            if (item != null)
                items.Add(item);
        }
        return items;
    }

    #endregion

    #region Damage and Repair System

    /// <summary>
    /// Apply damage to random equipped clothing (called when player takes damage)
    /// </summary>
    public void ApplyDamageToClothing(float totalDamage)
    {
        var occupiedSlots = clothingSlots.Where(s => s.isOccupied).ToList();
        if (occupiedSlots.Count == 0)
        {
            return; // No clothing to damage
        }

        // Distribute damage among all clothing pieces
        float damagePerPiece = totalDamage / occupiedSlots.Count;

        foreach (var slot in occupiedSlots)
        {
            slot.TakeDamage(damagePerPiece);
            OnClothingDamaged?.Invoke(slot);
        }

        DebugLog($"Applied {totalDamage} damage to {occupiedSlots.Count} clothing pieces");
        InvalidateStatsCache();
        OnClothingStatsChanged?.Invoke();
    }

    /// <summary>
    /// Apply damage to specific clothing slot
    /// </summary>
    public void ApplyDamageToSlot(string slotId, float damage)
    {
        var slot = GetSlotById(slotId);
        if (slot != null && slot.isOccupied)
        {
            slot.TakeDamage(damage);
            OnClothingDamaged?.Invoke(slot);
            InvalidateStatsCache();
            OnClothingStatsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Repair clothing in a specific slot
    /// </summary>
    public float RepairClothing(string slotId, float repairAmount)
    {
        var slot = GetSlotById(slotId);
        if (slot != null && slot.isOccupied)
        {
            float actualRepair = slot.RepairEquippedItem(repairAmount);
            if (actualRepair > 0)
            {
                DebugLog($"Repaired {slot.slotName} for {actualRepair:F1} durability");
                InvalidateStatsCache();
                OnClothingStatsChanged?.Invoke();
            }
            return actualRepair;
        }
        return 0f;
    }

    /// <summary>
    /// Get clothing items that need repair
    /// </summary>
    public List<ClothingSlot> GetClothingNeedingRepair()
    {
        var needsRepair = new List<ClothingSlot>();
        foreach (var slot in clothingSlots)
        {
            var clothingData = slot.GetEquippedClothingData();
            if (clothingData != null && clothingData.NeedsRepair)
            {
                needsRepair.Add(slot);
            }
        }
        return needsRepair;
    }

    #endregion

    #region Environmental System

    /// <summary>
    /// Apply wetness to all clothing based on rain intensity
    /// </summary>
    public void ApplyRainToClothing(float rainIntensity)
    {
        bool anyClothingGotWet = false;

        foreach (var slot in clothingSlots)
        {
            if (slot.isOccupied && slot.ShouldGetWet(rainIntensity))
            {
                slot.SetWetness(true);
                anyClothingGotWet = true;
            }
        }

        if (anyClothingGotWet)
        {
            DebugLog($"Clothing got wet from rain (intensity: {rainIntensity})");
            InvalidateStatsCache();
            OnClothingStatsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Dry clothing over time (called when not raining)
    /// </summary>
    public void DryClothing(float dryingTime)
    {
        bool anyClothingDried = false;

        foreach (var slot in clothingSlots)
        {
            var clothingData = slot.GetEquippedClothingData();
            if (clothingData != null && clothingData.isWet)
            {
                // Simple drying - could be enhanced with more complex logic
                if (dryingTime >= clothingData.dryingTime)
                {
                    slot.SetWetness(false);
                    anyClothingDried = true;
                }
            }
        }

        if (anyClothingDried)
        {
            DebugLog("Some clothing has dried");
            InvalidateStatsCache();
            OnClothingStatsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Get effective rain protection from all clothing
    /// </summary>
    public float GetEffectiveRainProtection()
    {
        return GetTotalStats().rain;
    }

    #endregion

    #region Stats Calculation

    /// <summary>
    /// Get total protection stats from all equipped clothing
    /// </summary>
    public (float warmth, float defense, float rain, float speed) GetTotalStats()
    {
        if (statsCached)
        {
            return (totalWarmth, totalDefense, totalRainProtection, totalSpeedModifier);
        }

        totalWarmth = 0f;
        totalDefense = 0f;
        totalRainProtection = 0f;
        totalSpeedModifier = 0f;

        foreach (var slot in clothingSlots)
        {
            var stats = slot.GetProtectionValues();
            totalWarmth += stats.warmth;
            totalDefense += stats.defense;
            totalRainProtection += stats.rain;
            totalSpeedModifier += stats.speed;
        }

        statsCached = true;
        return (totalWarmth, totalDefense, totalRainProtection, totalSpeedModifier);
    }

    /// <summary>
    /// Invalidate cached stats (call when clothing changes)
    /// </summary>
    private void InvalidateStatsCache()
    {
        statsCached = false;
    }

    /// <summary>
    /// Apply clothing stats to player (called by ClothingEquipmentEffects)
    /// </summary>
    public void ApplyClothingStatsToPlayer()
    {
        if (playerManager == null)
            return;

        var stats = GetTotalStats();

        // This would integrate with your player stat system
        // For now, we just log the values
        DebugLog($"Clothing Stats - Warmth: {stats.warmth:F1}, Defense: {stats.defense:F1}, Rain: {stats.rain:F1}, Speed: {stats.speed:F2}");
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handle slot item equipped event
    /// </summary>
    private void OnSlotItemEquipped(ClothingSlot slot, InventoryItemData item)
    {
        InvalidateStatsCache();
        OnClothingEquipped?.Invoke(slot, item);
        OnClothingStatsChanged?.Invoke();
    }

    /// <summary>
    /// Handle slot item removed event
    /// </summary>
    private void OnSlotItemRemoved(ClothingSlot slot)
    {
        InvalidateStatsCache();
        OnClothingUnequipped?.Invoke(slot);
        OnClothingStatsChanged?.Invoke();
    }

    /// <summary>
    /// Handle inventory item removal (clean up clothing references)
    /// </summary>
    private void OnInventoryItemRemoved(string itemId)
    {
        foreach (var slot in clothingSlots)
        {
            if (slot.isOccupied && slot.equippedItemID == itemId)
            {
                DebugLog($"Removing {slot.slotName} clothing due to inventory item removal");
                slot.UnequipItem();
            }
        }
    }

    #endregion

    #region Audio

    private void PlayEquipSound()
    {
        if (equipClothingSound != null)
        {
            AudioSource.PlayClipAtPoint(equipClothingSound, Vector3.zero);
        }
    }

    private void PlayUnequipSound()
    {
        if (unequipClothingSound != null)
        {
            AudioSource.PlayClipAtPoint(unequipClothingSound, Vector3.zero);
        }
    }

    #endregion

    #region Save System Integration

    /// <summary>
    /// Get clothing data for saving
    /// </summary>
    public ClothingSaveData GetClothingDataForSave()
    {
        var saveData = new ClothingSaveData();

        foreach (var slot in clothingSlots)
        {
            saveData.slotData.Add(slot.ToSaveData());
        }

        return saveData;
    }

    /// <summary>
    /// Load clothing data from save
    /// </summary>
    public void LoadClothingDataFromSave(ClothingSaveData saveData)
    {
        if (saveData?.slotData == null)
        {
            DebugLog("Invalid clothing save data - clearing all slots");
            ClearAllClothing();
            return;
        }

        // Clear existing clothing
        ClearAllClothing();

        // Load each slot
        foreach (var slotSaveData in saveData.slotData)
        {
            var slot = GetSlotById(slotSaveData.slotID);
            if (slot != null)
            {
                slot.FromSaveData(slotSaveData);

                // Validate that the item still exists in inventory
                if (slot.isOccupied)
                {
                    var item = slot.GetEquippedItem();
                    if (item == null)
                    {
                        DebugLog($"Clothing item {slotSaveData.equippedItemID} no longer exists - clearing slot {slot.slotName}");
                        slot.ClearSlot();
                    }
                }
            }
        }

        InvalidateStatsCache();
        OnClothingStatsChanged?.Invoke();
        DebugLog($"Loaded clothing data - {GetAllEquippedClothing().Count} items equipped");
    }

    /// <summary>
    /// Clear all equipped clothing
    /// </summary>
    public void ClearAllClothing()
    {
        foreach (var slot in clothingSlots)
        {
            if (slot.isOccupied)
            {
                slot.UnequipItem();
            }
        }
        InvalidateStatsCache();
    }

    #endregion

    #region Debug Methods

    [Button("Debug Clothing State")]
    private void DebugClothingState()
    {
        Debug.Log("=== CLOTHING SYSTEM DEBUG ===");

        var stats = GetTotalStats();
        Debug.Log($"Total Stats - Warmth: {stats.warmth:F1}, Defense: {stats.defense:F1}, Rain: {stats.rain:F1}, Speed: {stats.speed:F2}");

        foreach (var slot in clothingSlots)
        {
            Debug.Log(slot.GetDebugInfo());
        }

        var equippedCount = clothingSlots.Count(s => s.isOccupied);
        Debug.Log($"Total: {equippedCount}/{clothingSlots.Count} slots occupied");
    }

    [Button("Damage All Clothing")]
    private void DebugDamageAllClothing()
    {
        ApplyDamageToClothing(10f);
    }

    [Button("Repair All Clothing")]
    private void DebugRepairAllClothing()
    {
        foreach (var slot in clothingSlots)
        {
            RepairClothing(slot.slotID, 50f);
        }
    }

    [Button("Clear All Clothing")]
    private void DebugClearAllClothing()
    {
        ClearAllClothing();
    }

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ClothingManager] {message}");
        }
    }

    #endregion

    private void OnDestroy()
    {
        // Clean up event subscriptions
        GameManager.OnManagersRefreshed -= RefreshReferences;

        if (inventoryManager != null)
        {
            inventoryManager.OnItemRemoved -= OnInventoryItemRemoved;
        }

        foreach (var slot in clothingSlots)
        {
            slot.OnItemEquipped -= OnSlotItemEquipped;
            slot.OnItemRemoved -= OnSlotItemRemoved;
        }
    }
}