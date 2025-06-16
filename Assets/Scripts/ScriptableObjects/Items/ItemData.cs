using UnityEngine;

/// <summary>
/// Item type enumeration for different item categories
/// </summary>
public enum ItemType
{
    Consumable, // Food, meds, water - can be consumed to affect player stats
    Weapon,     // Guns, knives, grenades - combat items
    Equipment,  // Lock-picks, scanners - interaction tools
    KeyItem,    // Quest items, keys - cannot be dropped
    Ammo        // Ammunition - stackable
}

/// <summary>
/// Degradation type for items
/// </summary>
public enum DegradationType
{
    None,       // Item doesn't degrade
    OverTime,   // Degrades gradually over time (food spoilage)
    OnUse,      // Degrades when used (weapon wear)
    Both        // Degrades both over time and on use
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Item Info")]
    public string itemName;
    [TextArea(2, 4)]
    public string description;

    [Header("Item Type")]
    public ItemType itemType = ItemType.Consumable;

    [Header("Shape Configuration")]
    public TetrominoType shapeType = TetrominoType.Single;

    [Header("Visual Configuration")]
    public Sprite itemSprite;
    [Range(0.5f, 4.0f)]
    public float spriteScale = 1.0f;
    public Vector2 spriteOffset = Vector2.zero;
    public Color cellColor = Color.gray;

    [Header("Item Properties")]
    public int stackSize = 1;
    public bool isRotatable = true;

    [Header("Degradation System")]
    public DegradationType degradationType = DegradationType.None;
    [Range(0f, 100f)]
    public float maxDurability = 100f;
    [Range(0f, 10f)]
    public float degradationRate = 1f; // Units per use or per hour
    [Tooltip("Time in hours for one degradation tick (for OverTime degradation)")]
    public float degradationInterval = 1f;

    [Header("Item Type Specific Settings")]
    [Header("Consumable Settings")]
    [SerializeField] private ConsumableData consumableData;

    [Header("Weapon Settings")]
    [SerializeField] private WeaponData weaponData;

    [Header("Equipment Settings")]
    [SerializeField] private EquipmentData equipmentData;

    [Header("Ammo Settings")]
    [SerializeField] private AmmoData ammoData;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Properties for accessing type-specific data
    public ConsumableData ConsumableData => consumableData;
    public WeaponData WeaponData => weaponData;
    public EquipmentData EquipmentData => equipmentData;
    public AmmoData AmmoData => ammoData;

    // Get the color for this item (custom color instead of shape-based)
    public Color CellColor => cellColor;

    // Check if item can be stacked
    public bool IsStackable => stackSize > 1;

    // Check if item can be dropped
    public bool CanDrop => itemType != ItemType.KeyItem;

    // Check if item degrades
    public bool CanDegrade => degradationType != DegradationType.None && maxDurability > 0;

    // Get bounding size for this item's shape
    public Vector2Int GetBoundingSize()
    {
        var shapeData = TetrominoDefinitions.GetRotationState(shapeType, 0);
        if (shapeData.cells.Length == 0)
            return Vector2Int.one;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in shapeData.cells)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
    }

    // Validation
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemName))
            itemName = name;

        spriteScale = Mathf.Clamp(spriteScale, 0.5f, 4.0f);

        // Auto-configure stack size based on item type
        if (itemType == ItemType.Ammo && stackSize <= 1)
        {
            stackSize = 50; // Default ammo stack size
        }
        else if (itemType != ItemType.Ammo && stackSize > 1)
        {
            // Only ammo should be stackable for now
            stackSize = 1;
        }

        // Validate degradation settings
        if (degradationType != DegradationType.None)
        {
            if (maxDurability <= 0)
                maxDurability = 100f;
            if (degradationRate <= 0)
                degradationRate = 1f;
        }

        if (showDebugInfo && Application.isPlaying)
        {
            Debug.Log($"Item: {itemName}, Type: {itemType}, Shape: {shapeType}, Stackable: {IsStackable}, Can Drop: {CanDrop}");
        }
    }
}