using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Basic Item Info")]
    public string itemName;
    [TextArea(2, 4)]
    public string description;

    [Header("Shape Configuration")]
    public TetrominoType shapeType = TetrominoType.Single;

    [Header("Visual Configuration")]
    public Sprite itemSprite;
    [Range(0.5f, 4.0f)]
    public float spriteScale = 1.0f;
    public Vector2 spriteOffset = Vector2.zero;
    public Color cellColor = Color.gray; // New: Custom color for this item's cells

    [Header("Item Properties")]
    public int stackSize = 1;
    public bool isRotatable = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Get the color for this item (custom color instead of shape-based)
    public Color CellColor => cellColor;

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

        if (showDebugInfo && Application.isPlaying)
        {
            Debug.Log($"Item: {itemName}, Shape: {shapeType}, Bounding Size: {GetBoundingSize()}, Color: {CellColor}");
        }
    }
}