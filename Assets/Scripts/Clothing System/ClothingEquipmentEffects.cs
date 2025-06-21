using System.Linq;
using UnityEngine;

/// <summary>
/// Handles the application of clothing effects to player stats and abilities.
/// Integrates with your existing PlayerManager and PlayerData systems.
/// Monitors clothing changes and updates player stats accordingly.
/// </summary>
public class ClothingEquipmentEffects : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private ClothingManager clothingManager;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private PlayerController playerController;

    [Header("Auto-Find Settings")]
    [SerializeField] private bool autoFindReferences = true;

    [Header("Effect Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float updateInterval = 1f; // How often to update effects

    [Header("Speed Effect Settings")]
    [SerializeField] private bool applySpeedEffects = true;
    [SerializeField] private float maxSpeedModifier = 0.5f; // Clamp speed effects
    [SerializeField] private float minSpeedModifier = -0.8f;

    // Current applied effects (for cleanup)
    private float currentWarmthBonus = 0f;
    private float currentDefenseBonus = 0f;
    private float currentRainProtection = 0f;
    private float currentSpeedModifier = 0f;

    // Base player stats (to restore when clothing removed)
    private float baseWalkSpeed;
    private float baseRunSpeed;
    private bool baseStatsStored = false;

    // Update timing
    private float lastUpdateTime = 0f;

    private void Awake()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }
    }

    private void Start()
    {
        ValidateReferences();
        SubscribeToEvents();
        StoreBasePlayerStats();

        // Initial effect application
        UpdateClothingEffects();
    }

    /// <summary>
    /// Automatically find required component references
    /// </summary>
    private void FindReferences()
    {
        if (clothingManager == null)
            clothingManager = ClothingManager.Instance ?? FindFirstObjectByType<ClothingManager>();

        if (playerManager == null)
            playerManager = GameManager.Instance?.playerManager ?? FindFirstObjectByType<PlayerManager>();

        if (playerData == null)
            playerData = GameManager.Instance?.playerData;

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        DebugLog($"Auto-found references - Clothing: {clothingManager != null}, Player: {playerManager != null}, Data: {playerData != null}, Controller: {playerController != null}");
    }

    /// <summary>
    /// Validate that all necessary references are available
    /// </summary>
    private void ValidateReferences()
    {
        if (clothingManager == null)
        {
            Debug.LogWarning("ClothingEquipmentEffects: ClothingManager not found - clothing effects disabled");
        }

        if (playerManager == null)
        {
            Debug.LogWarning("ClothingEquipmentEffects: PlayerManager not found - some effects may not work");
        }

        if (playerData == null)
        {
            Debug.LogWarning("ClothingEquipmentEffects: PlayerData not found - speed effects disabled");
        }

        if (playerController == null)
        {
            Debug.LogWarning("ClothingEquipmentEffects: PlayerController not found - movement effects disabled");
        }
    }

    /// <summary>
    /// Subscribe to clothing system events
    /// </summary>
    private void SubscribeToEvents()
    {
        if (clothingManager != null)
        {
            clothingManager.OnClothingStatsChanged += OnClothingStatsChanged;
        }

        // Subscribe to manager refresh events
        GameManager.OnManagersRefreshed += OnManagersRefreshed;
    }

    /// <summary>
    /// Store base player stats for restoration
    /// </summary>
    private void StoreBasePlayerStats()
    {
        if (playerData != null && !baseStatsStored)
        {
            baseWalkSpeed = playerData.walkSpeed;
            baseRunSpeed = playerData.runSpeed;
            baseStatsStored = true;
            DebugLog($"Stored base player stats - Walk: {baseWalkSpeed}, Run: {baseRunSpeed}");
        }
    }

    private void Update()
    {
        // Periodic update for time-based effects
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdatePeriodicEffects();
        }
    }

    /// <summary>
    /// Handle clothing stats changed event
    /// </summary>
    private void OnClothingStatsChanged()
    {
        UpdateClothingEffects();
    }

    /// <summary>
    /// Handle manager refresh events
    /// </summary>
    private void OnManagersRefreshed()
    {
        if (autoFindReferences)
        {
            FindReferences();
        }
    }

    /// <summary>
    /// Update all clothing effects on the player
    /// </summary>
    private void UpdateClothingEffects()
    {
        if (clothingManager == null)
            return;

        var stats = clothingManager.TotalStats;

        // Store new effect values
        currentWarmthBonus = stats.warmth;
        currentDefenseBonus = stats.defense;
        currentRainProtection = stats.rain;
        currentSpeedModifier = Mathf.Clamp(stats.speed, minSpeedModifier, maxSpeedModifier);

        // Apply effects to player systems
        ApplySpeedEffects();
        ApplyDefenseEffects();
        ApplyWarmthEffects();
        ApplyRainProtectionEffects();

        DebugLog($"Updated clothing effects - W:{currentWarmthBonus:F1} D:{currentDefenseBonus:F1} R:{currentRainProtection:F1} S:{currentSpeedModifier:F2}");
    }

    #region Effect Application Methods

    /// <summary>
    /// Apply speed modifications from clothing
    /// </summary>
    private void ApplySpeedEffects()
    {
        if (!applySpeedEffects || playerData == null || !baseStatsStored)
            return;

        // Calculate new speeds based on clothing modifier
        float speedMultiplier = 1f + currentSpeedModifier;

        playerData.walkSpeed = baseWalkSpeed * speedMultiplier;
        playerData.runSpeed = baseRunSpeed * speedMultiplier;

        DebugLog($"Applied speed effects - Multiplier: {speedMultiplier:F2}, Walk: {playerData.walkSpeed:F1}, Run: {playerData.runSpeed:F1}");
    }

    /// <summary>
    /// Apply defense effects (could integrate with damage reduction system)
    /// </summary>
    private void ApplyDefenseEffects()
    {
        // This is where you would integrate with your damage system
        // For now, we just store the value for other systems to use

        // Example: Could modify PlayerManager to use clothing defense
        // if (playerManager != null)
        // {
        //     playerManager.additionalDefense = currentDefenseBonus;
        // }

        DebugLog($"Defense bonus available: {currentDefenseBonus:F1}");
    }

    /// <summary>
    /// Apply warmth effects (could integrate with environmental system)
    /// </summary>
    private void ApplyWarmthEffects()
    {
        // This is where you would integrate with your environmental/temperature system
        // For now, we just store the value for other systems to use

        DebugLog($"Warmth protection available: {currentWarmthBonus:F1}");
    }

    /// <summary>
    /// Apply rain protection effects
    /// </summary>
    private void ApplyRainProtectionEffects()
    {
        // This is where you would integrate with your weather system
        // For now, we just store the value for other systems to use

        DebugLog($"Rain protection available: {currentRainProtection:F1}");
    }

    #endregion

    #region Periodic Effects

    /// <summary>
    /// Handle periodic effects that need regular updates
    /// </summary>
    private void UpdatePeriodicEffects()
    {
        // Could handle things like:
        // - Temperature regulation based on warmth
        // - Gradual health regeneration from comfortable clothing
        // - Status effects from wearing damaged clothing

        CheckClothingConditionEffects();
    }

    /// <summary>
    /// Check for effects based on clothing condition
    /// </summary>
    private void CheckClothingConditionEffects()
    {
        if (clothingManager == null)
            return;

        bool hasDestroyedClothing = false;
        bool hasWetClothing = false;
        int totalClothingPieces = 0;
        int damagedPieces = 0;

        foreach (var slot in clothingManager.AllClothingSlots)
        {
            if (slot.isOccupied)
            {
                totalClothingPieces++;
                var clothingData = slot.GetEquippedClothingData();

                if (clothingData != null)
                {
                    if (clothingData.IsDestroyed)
                        hasDestroyedClothing = true;

                    if (clothingData.isWet)
                        hasWetClothing = true;

                    if (clothingData.NeedsRepair)
                        damagedPieces++;
                }
            }
        }

        // Apply condition-based effects
        if (hasDestroyedClothing)
        {
            // Could apply negative effects for wearing destroyed clothing
            DebugLog("Warning: Wearing destroyed clothing!");
        }

        if (hasWetClothing)
        {
            // Could apply cold/discomfort effects for wet clothing
            DebugLog("Warning: Wearing wet clothing!");
        }

        if (damagedPieces > totalClothingPieces / 2)
        {
            // Could apply effects for wearing mostly damaged clothing
            DebugLog("Warning: Most clothing is damaged!");
        }
    }

    #endregion

    #region Public Interface

    /// <summary>
    /// Get current defense bonus from clothing
    /// </summary>
    public float GetClothingDefenseBonus()
    {
        return currentDefenseBonus;
    }

    /// <summary>
    /// Get current warmth protection from clothing
    /// </summary>
    public float GetClothingWarmthProtection()
    {
        return currentWarmthBonus;
    }

    /// <summary>
    /// Get current rain protection from clothing
    /// </summary>
    public float GetClothingRainProtection()
    {
        return currentRainProtection;
    }

    /// <summary>
    /// Get current speed modifier from clothing
    /// </summary>
    public float GetClothingSpeedModifier()
    {
        return currentSpeedModifier;
    }

    /// <summary>
    /// Check if player is well-protected from rain
    /// </summary>
    public bool IsWellProtectedFromRain()
    {
        return currentRainProtection >= 50f; // Configurable threshold
    }

    /// <summary>
    /// Check if player is well-protected from cold
    /// </summary>
    public bool IsWellProtectedFromCold()
    {
        return currentWarmthBonus >= 30f; // Configurable threshold
    }

    /// <summary>
    /// Get overall clothing protection level (0-1)
    /// </summary>
    public float GetOverallProtectionLevel()
    {
        if (clothingManager == null)
            return 0f;

        int occupiedSlots = clothingManager.AllClothingSlots.Count(s => s.isOccupied);
        int totalSlots = clothingManager.AllClothingSlots.Count;

        float coverageRatio = (float)occupiedSlots / totalSlots;
        float effectivenessRatio = Mathf.Clamp01((currentWarmthBonus + currentDefenseBonus) / 100f);

        return (coverageRatio + effectivenessRatio) / 2f;
    }

    /// <summary>
    /// Force refresh of all clothing effects
    /// </summary>
    public void RefreshClothingEffects()
    {
        UpdateClothingEffects();
    }

    #endregion

    #region Damage Integration

    /// <summary>
    /// Called when player takes damage - distribute to clothing
    /// </summary>
    public void OnPlayerTakeDamage(float damage)
    {
        if (clothingManager != null)
        {
            // Reduce damage based on defense bonus
            float damageReduction = Mathf.Min(damage * 0.1f, currentDefenseBonus * 0.02f);
            float reducedDamage = Mathf.Max(0f, damage - damageReduction);

            // Apply some of the damage to clothing
            float clothingDamage = damage * 0.3f; // 30% of damage goes to clothing
            clothingManager.ApplyDamageToClothing(clothingDamage);

            DebugLog($"Player damage: {damage:F1} -> {reducedDamage:F1} (reduced by {damageReduction:F1}), clothing damage: {clothingDamage:F1}");
        }
    }

    #endregion

    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ClothingEffects] {message}");
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        if (clothingManager != null)
        {
            clothingManager.OnClothingStatsChanged -= OnClothingStatsChanged;
        }

        GameManager.OnManagersRefreshed -= OnManagersRefreshed;

        // Restore base player stats
        if (baseStatsStored && playerData != null)
        {
            playerData.walkSpeed = baseWalkSpeed;
            playerData.runSpeed = baseRunSpeed;
        }
    }
}