using UnityEngine;

/// <summary>
/// Clothing type enumeration for different body parts
/// </summary>
public enum ClothingType
{
    Head,    // Hats, helmets, scarves, masks
    Torso,   // Shirts, coats, jackets
    Hands,   // Gloves, mittens
    Legs,    // Pants, thermal underwear
    Socks,   // Socks, stockings
    Shoes    // Boots, shoes, sandals
}

/// <summary>
/// Clothing layer enumeration for layered clothing system
/// Uses flags to allow items to be worn in multiple layers
/// </summary>
[System.Flags]
public enum ClothingLayer
{
    None = 0,
    Upper = 1 << 0,     // Upper head slot, outer torso, outer legs
    Lower = 1 << 1,     // Lower head slot, inner torso, inner legs
    Single = 1 << 2,    // Single layer items (hands, socks, shoes)
    Both = Upper | Lower // Items that can be worn in either layer
}

/// <summary>
/// Enhanced clothing data that extends the base ClothingData placeholder.
/// Handles all clothing-specific properties including protection stats,
/// durability, environmental effects, and layer restrictions.
/// </summary>
[System.Serializable]
public class ClothingData
{
    [Header("Clothing Classification")]
    [Tooltip("What type of clothing this is (head, torso, etc.)")]
    public ClothingType clothingType = ClothingType.Torso;

    [Tooltip("Which clothing layers this item can be equipped in")]
    public ClothingLayer validLayers = ClothingLayer.Both;

    [Header("Protection Stats")]
    [Tooltip("Warmth protection provided (0-100)")]
    [Range(0f, 100f)]
    public float warmthProtection = 10f;

    [Tooltip("Physical defense protection (0-50)")]
    [Range(0f, 50f)]
    public float defenseProtection = 5f;

    [Tooltip("Rain/water protection (0-100)")]
    [Range(0f, 100f)]
    public float rainProtection = 0f;

    [Header("Movement Effects")]
    [Tooltip("Speed modifier - positive values increase speed, negative decrease")]
    [Range(-0.5f, 0.2f)]
    public float speedModifier = 0f;

    [Header("Durability System")]
    [Tooltip("Maximum durability of this clothing item")]
    [Range(1f, 200f)]
    public float maxDurability = 100f;

    [Tooltip("Current durability (set at runtime)")]
    [HideInInspector]
    public float currentDurability = 100f;

    [Tooltip("How much durability is lost per damage event")]
    [Range(0.1f, 10f)]
    public float damagePerHit = 2f;

    [Tooltip("How much durability is lost per hour of wear")]
    [Range(0f, 5f)]
    public float wearPerHour = 0.5f;

    [Header("Environmental Properties")]
    [Tooltip("Is this clothing item currently wet?")]
    [HideInInspector]
    public bool isWet = false;

    [Tooltip("Resistance to getting wet (0-100)")]
    [Range(0f, 100f)]
    public float wetResistance = 10f;

    [Tooltip("How quickly this item dries when not in rain (minutes)")]
    [Range(1f, 120f)]
    public float dryingTime = 30f;

    [Header("Repair Properties")]
    [Tooltip("Can this item be repaired?")]
    public bool canBeRepaired = true;

    [Tooltip("Maximum durability after repair (as percentage of original)")]
    [Range(0.5f, 1f)]
    public float maxRepairEfficiency = 0.9f;

    [Header("Visual Properties")]
    [Tooltip("Color tint applied when item is worn")]
    public Color wornTint = Color.white;

    [Tooltip("Sprite used for clothing slot display")]
    public Sprite slotSprite;

    // Runtime properties
    public bool IsDestroyed => currentDurability <= 0f;
    public bool NeedsRepair => currentDurability < maxDurability * 0.5f;
    public float DurabilityPercentage => currentDurability / maxDurability;
    public bool IsFullyRepaired => currentDurability >= maxDurability * maxRepairEfficiency;

    /// <summary>
    /// Initialize durability to maximum when created
    /// </summary>
    public void InitializeDurability()
    {
        currentDurability = maxDurability;
    }

    /// <summary>
    /// Check if this clothing can be equipped in a specific slot
    /// </summary>
    public bool CanEquipInLayer(ClothingLayer layer)
    {
        return (validLayers & layer) != 0;
    }

    /// <summary>
    /// Apply damage to this clothing item
    /// </summary>
    public void TakeDamage(float damage = -1f)
    {
        float actualDamage = damage > 0 ? damage : damagePerHit;
        currentDurability = Mathf.Max(0f, currentDurability - actualDamage);
    }

    /// <summary>
    /// Apply wear over time
    /// </summary>
    public void ApplyWear(float hoursWorn)
    {
        float wearDamage = wearPerHour * hoursWorn;
        currentDurability = Mathf.Max(0f, currentDurability - wearDamage);
    }

    /// <summary>
    /// Repair this clothing item
    /// </summary>
    public float RepairItem(float repairAmount)
    {
        if (!canBeRepaired || IsDestroyed)
            return 0f;

        float maxRepairTarget = maxDurability * maxRepairEfficiency;
        float oldDurability = currentDurability;
        currentDurability = Mathf.Min(maxRepairTarget, currentDurability + repairAmount);

        return currentDurability - oldDurability; // Return actual repair amount
    }

    /// <summary>
    /// Get effective protection values based on current condition
    /// </summary>
    public float GetEffectiveWarmth()
    {
        float conditionMultiplier = DurabilityPercentage;
        float wetMultiplier = isWet ? 0.5f : 1f; // Wet clothes are less warm
        return warmthProtection * conditionMultiplier * wetMultiplier;
    }

    public float GetEffectiveDefense()
    {
        return defenseProtection * DurabilityPercentage;
    }

    public float GetEffectiveRainProtection()
    {
        return rainProtection * DurabilityPercentage;
    }

    public float GetEffectiveSpeedModifier()
    {
        float wetPenalty = isWet ? -0.1f : 0f; // Wet clothes slow you down
        return speedModifier + wetPenalty;
    }

    /// <summary>
    /// Set wetness state
    /// </summary>
    public void SetWetness(bool wet)
    {
        isWet = wet;
    }

    /// <summary>
    /// Check if item should get wet based on resistance
    /// </summary>
    public bool ShouldGetWet(float rainIntensity)
    {
        if (isWet) return false; // Already wet

        float effectiveResistance = wetResistance * DurabilityPercentage;
        return rainIntensity > effectiveResistance;
    }

    /// <summary>
    /// Get a description of the item's current condition
    /// </summary>
    public string GetConditionDescription()
    {
        if (IsDestroyed)
            return "Destroyed";

        float percentage = DurabilityPercentage;

        if (percentage > 0.9f)
            return "Excellent";
        else if (percentage > 0.7f)
            return "Good";
        else if (percentage > 0.5f)
            return "Worn";
        else if (percentage > 0.3f)
            return "Damaged";
        else
            return "Nearly Destroyed";
    }

    /// <summary>
    /// Create a copy of this clothing data for save/load
    /// </summary>
    public ClothingData CreateCopy()
    {
        var copy = new ClothingData();

        // Copy all properties
        copy.clothingType = clothingType;
        copy.validLayers = validLayers;
        copy.warmthProtection = warmthProtection;
        copy.defenseProtection = defenseProtection;
        copy.rainProtection = rainProtection;
        copy.speedModifier = speedModifier;
        copy.maxDurability = maxDurability;
        copy.currentDurability = currentDurability;
        copy.damagePerHit = damagePerHit;
        copy.wearPerHour = wearPerHour;
        copy.isWet = isWet;
        copy.wetResistance = wetResistance;
        copy.dryingTime = dryingTime;
        copy.canBeRepaired = canBeRepaired;
        copy.maxRepairEfficiency = maxRepairEfficiency;
        copy.wornTint = wornTint;
        copy.slotSprite = slotSprite;

        return copy;
    }
}