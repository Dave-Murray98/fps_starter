using UnityEngine;

[System.Serializable]
public class GridItem
{
    [Header("Basic Properties")]
    public string ID;
    public string ItemName; // Added ItemName property
    public Vector2Int GridPosition;

    [Header("Shape Properties")]
    public TetrominoType shapeType;
    public int currentRotation = 0;

    // Cached data for performance
    private TetrominoData _currentShapeData;
    private bool _dataCached = false;

    public GridItem(string id, TetrominoType type, Vector2Int gridPosition)
    {
        ID = id;
        shapeType = type;
        GridPosition = gridPosition;
        currentRotation = 0;
        ItemName = $"Item_{id}"; // Default name
        RefreshShapeData();
    }

    public GridItem(string id, TetrominoType type, Vector2Int gridPosition, string itemName)
    {
        ID = id;
        shapeType = type;
        GridPosition = gridPosition;
        currentRotation = 0;
        ItemName = itemName;
        RefreshShapeData();
    }

    // Get current shape data (cached)
    public TetrominoData CurrentShapeData
    {
        get
        {
            if (!_dataCached)
                RefreshShapeData();
            return _currentShapeData;
        }
    }

    // Get all occupied grid positions relative to GridPosition
    public Vector2Int[] GetOccupiedPositions()
    {
        var shapeData = CurrentShapeData;
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = GridPosition + shapeData.cells[i];
        }

        return positions;
    }

    // Get occupied positions for a specific grid position (for validation)
    public Vector2Int[] GetOccupiedPositionsAt(Vector2Int position)
    {
        var shapeData = CurrentShapeData;
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = position + shapeData.cells[i];
        }

        return positions;
    }

    // Get occupied positions for a specific rotation (for validation)
    public Vector2Int[] GetOccupiedPositionsAt(Vector2Int position, int rotation)
    {
        var shapeData = TetrominoDefinitions.GetRotationState(shapeType, rotation);
        Vector2Int[] positions = new Vector2Int[shapeData.cells.Length];

        for (int i = 0; i < shapeData.cells.Length; i++)
        {
            positions[i] = position + shapeData.cells[i];
        }

        return positions;
    }

    // Rotate the item (cycles through all available rotations)
    public void RotateItem()
    {
        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = (currentRotation + 1) % maxRotations;
        RefreshShapeData();
    }

    // Set specific rotation
    public void SetRotation(int rotation)
    {
        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = Mathf.Clamp(rotation, 0, maxRotations - 1);
        RefreshShapeData();
    }

    // Set grid position
    public void SetGridPosition(Vector2Int position)
    {
        GridPosition = position;
    }

    // Get bounding box of the shape
    public Vector2Int GetBoundingSize()
    {
        var shapeData = CurrentShapeData;
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

    // Get the color of this item
    public Color ItemColor => CurrentShapeData.color;

    // Check if this shape can rotate (has multiple rotation states)
    public bool CanRotate => TetrominoDefinitions.GetRotationCount(shapeType) > 1;

    // Check if item is currently rotated from its base state
    public bool IsRotated => currentRotation != 0;

    // Refresh cached shape data
    private void RefreshShapeData()
    {
        _currentShapeData = TetrominoDefinitions.GetRotationState(shapeType, currentRotation);
        _dataCached = true;
    }
}