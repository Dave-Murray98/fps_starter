using UnityEngine;

[System.Serializable]
public class GridItem
{
    [SerializeField] private int id;
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private Color itemColor = Color.white;
    [SerializeField] private string itemName;

    // Current position in grid
    [System.NonSerialized] private Vector2Int gridPosition;
    [System.NonSerialized] private bool isRotated = false;

    public int ID => id;
    public int Width => isRotated ? height : width;
    public int Height => isRotated ? width : height;
    public int OriginalWidth => width;
    public int OriginalHeight => height;
    public Color ItemColor => itemColor;
    public string ItemName => itemName;
    public Vector2Int GridPosition => gridPosition;
    public bool IsRotated => isRotated;

    public GridItem(int id, int width, int height, Color color, string name = "")
    {
        this.id = id;
        this.width = width;
        this.height = height;
        this.itemColor = color;
        this.itemName = string.IsNullOrEmpty(name) ? $"Item_{id}" : name;
        this.gridPosition = Vector2Int.zero;
    }

    public void SetGridPosition(Vector2Int position)
    {
        gridPosition = position;
    }

    public void RotateItem()
    {
        isRotated = !isRotated;
    }

    public void ResetRotation()
    {
        isRotated = false;
    }

    // Get all grid positions this item occupies
    public Vector2Int[] GetOccupiedPositions()
    {
        Vector2Int[] positions = new Vector2Int[Width * Height];
        int index = 0;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                positions[index] = new Vector2Int(gridPosition.x + x, gridPosition.y + y);
                index++;
            }
        }

        return positions;
    }
}