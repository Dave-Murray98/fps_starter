using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure data representation of the inventory grid - no visual dependencies
/// This persists across scenes and doesn't require any UI to be active
/// </summary>
[System.Serializable]
public class InventoryGridData
{
    [SerializeField] private string[,] grid;
    [SerializeField] private Dictionary<string, InventoryItemData> items;
    [SerializeField] private int gridWidth;
    [SerializeField] private int gridHeight;

    public int Width => gridWidth;
    public int Height => gridHeight;
    public int ItemCount => items?.Count ?? 0;

    public InventoryGridData(int width, int height)
    {
        gridWidth = width;
        gridHeight = height;
        grid = new string[width, height];
        items = new Dictionary<string, InventoryItemData>();

        // Initialize grid with null values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = null;
            }
        }
    }

    public bool IsValidPosition(Vector2Int position, InventoryItemData item)
    {
        return IsValidPosition(position, item, item.currentRotation);
    }

    public bool IsValidPosition(Vector2Int position, InventoryItemData item, int rotation)
    {
        var occupiedPositions = item.GetOccupiedPositionsAt(position, rotation);

        foreach (var pos in occupiedPositions)
        {
            if (pos.x < 0 || pos.x >= gridWidth || pos.y < 0 || pos.y >= gridHeight)
                return false;

            if (grid[pos.x, pos.y] != null && grid[pos.x, pos.y] != item.ID)
                return false;
        }

        return true;
    }

    public bool PlaceItem(InventoryItemData item)
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

    public bool RemoveItem(string itemID)
    {
        if (!items.ContainsKey(itemID))
            return false;

        var item = items[itemID];
        var occupiedPositions = item.GetOccupiedPositions();

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

    public InventoryItemData GetItemAt(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return null;

        string itemID = grid[x, y];
        if (itemID == null)
            return null;

        return items.ContainsKey(itemID) ? items[itemID] : null;
    }

    public InventoryItemData GetItem(string itemID)
    {
        return items.ContainsKey(itemID) ? items[itemID] : null;
    }

    public List<InventoryItemData> GetAllItems()
    {
        return new List<InventoryItemData>(items.Values);
    }

    public bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
            return true;

        return grid[x, y] != null;
    }

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

    public Vector2Int? FindValidPositionForItem(InventoryItemData item, int startX = 0, int startY = 0)
    {
        for (int y = startY; y < gridHeight; y++)
        {
            for (int x = (y == startY ? startX : 0); x < gridWidth; x++)
            {
                Vector2Int testPos = new Vector2Int(x, y);
                if (IsValidPosition(testPos, item))
                {
                    return testPos;
                }
            }
        }
        return null;
    }
}