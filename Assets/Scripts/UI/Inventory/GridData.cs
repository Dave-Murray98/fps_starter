using System;
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GridData
{
    private string[,] grid;
    private Dictionary<string, GridItem> items;
    private int gridWidth;
    private int gridHeight;

    public int Width => gridWidth;
    public int Height => gridHeight;

    public GridData(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        grid = new string[width, height];
        items = new Dictionary<string, GridItem>();

        // Initialize grid with null values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = null;
            }
        }
    }

    // Check if a position is valid for a complex shape
    public bool IsValidPosition(Vector2Int position, GridItem item)
    {
        return IsValidPosition(position, item, item.currentRotation);
    }

    // Check if a position is valid for a complex shape with specific rotation
    public bool IsValidPosition(Vector2Int position, GridItem item, int rotation)
    {
        var occupiedPositions = item.GetOccupiedPositionsAt(position, rotation);

        foreach (var pos in occupiedPositions)
        {
            // Check bounds
            if (pos.x < 0 || pos.x >= gridWidth || pos.y < 0 || pos.y >= gridHeight)
                return false;

            // Check if cell is occupied by another item
            if (grid[pos.x, pos.y] != null && grid[pos.x, pos.y] != item.ID)
                return false;
        }

        return true;
    }

    // Legacy method for simple rectangular validation
    public bool IsValidPosition(int x, int y, int width, int height)
    {
        // Check bounds
        if (x < 0 || y < 0 || x + width > gridWidth || y + height > gridHeight)
            return false;

        // Check if any cell in the rectangle is occupied
        for (int checkX = x; checkX < x + width; checkX++)
        {
            for (int checkY = y; checkY < y + height; checkY++)
            {
                if (grid[checkX, checkY] != null)
                    return false;
            }
        }

        return true;
    }

    // Place a complex item on the grid
    public bool PlaceItem(GridItem item)
    {
        if (!IsValidPosition(item.GridPosition, item))
            return false;

        // Remove item if it already exists (for repositioning)
        if (items.ContainsKey(item.ID))
            RemoveItem(item.ID);

        // Place the item
        var occupiedPositions = item.GetOccupiedPositions();
        foreach (var pos in occupiedPositions)
        {
            grid[pos.x, pos.y] = item.ID;
        }

        items[item.ID] = item;
        return true;
    }

    // Legacy method for placing rectangular items
    public bool PlaceItem(int x, int y, int width, int height, string itemID)
    {
        if (!IsValidPosition(x, y, width, height))
            return false;

        // Remove existing item if repositioning
        if (items.ContainsKey(itemID))
            RemoveItem(itemID);

        // Place rectangular item
        for (int checkX = x; checkX < x + width; checkX++)
        {
            for (int checkY = y; checkY < y + height; checkY++)
            {
                grid[checkX, checkY] = itemID;
            }
        }

        // Create a complex grid item for backwards compatibility
        var item = GridItem.CreateRectangular(itemID, width, height, Color.white, new Vector2Int(x, y));
        items[itemID] = item;

        return true;
    }

    // Remove an item by ID
    public bool RemoveItem(string itemID)
    {
        if (!items.ContainsKey(itemID))
            return false;

        var item = items[itemID];
        var occupiedPositions = item.GetOccupiedPositions();

        // Clear grid cells
        foreach (var pos in occupiedPositions)
        {
            if (pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight)
            {
                grid[pos.x, pos.y] = null;
            }
        }

        items.Remove(itemID);
        return true;
    }

    // Legacy method for removing rectangular items
    public void RemoveItem(int x, int y, int width, int height)
    {
        // Find item at this position and remove it
        for (int checkX = x; checkX < x + width && checkX < gridWidth; checkX++)
        {
            for (int checkY = y; checkY < y + height && checkY < gridHeight; checkY++)
            {
                if (checkX >= 0 && checkY >= 0 && grid[checkX, checkY] != null)
                {
                    string itemID = grid[checkX, checkY];
                    RemoveItem(itemID);
                    return; // Remove only the first item found
                }
            }
        }
    }

    // Get item at specific grid position
    public GridItem GetItemAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return null;

        string itemID = grid[x, y];
        if (itemID == null)
            return null;

        return items.ContainsKey(itemID) ? items[itemID] : null;
    }

    // Get item by ID
    public GridItem GetItem(string itemID)
    {
        return items.ContainsKey(itemID) ? items[itemID] : null;
    }

    // Get all items
    public List<GridItem> GetAllItems()
    {
        return new List<GridItem>(items.Values);
    }

    // Check if a cell is occupied
    public bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return true; // Out of bounds is considered occupied

        return grid[x, y] != null;
    }

    // Clear all items
    public void Clear()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y] = null;
            }
        }
        items.Clear();
    }

    // Get debug string representation
    public string GetDebugString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int y = gridHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                sb.Append(grid[x, y] != null ? "X" : ".");
                sb.Append(" ");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}