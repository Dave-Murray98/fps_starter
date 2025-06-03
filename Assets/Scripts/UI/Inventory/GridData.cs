using System;
using UnityEngine;

[System.Serializable]
public class GridData
{
    [SerializeField] private int width;
    [SerializeField] private int height;
    [SerializeField] private int[,] grid;

    public int Width => width;
    public int Height => height;

    public GridData(int width, int height)
    {
        this.width = width;
        this.height = height;
        grid = new int[width, height];
        ClearGrid();
    }

    public void ClearGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = 0; // 0 = empty, positive numbers = item IDs
            }
        }
    }

    public bool IsValidPosition(int startX, int startY, int itemWidth, int itemHeight)
    {
        // Check bounds
        if (startX < 0 || startY < 0 ||
            startX + itemWidth > width ||
            startY + itemHeight > height)
        {
            return false;
        }

        // Check if cells are occupied
        for (int x = startX; x < startX + itemWidth; x++)
        {
            for (int y = startY; y < startY + itemHeight; y++)
            {
                if (grid[x, y] != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool PlaceItem(int startX, int startY, int itemWidth, int itemHeight, int itemId)
    {
        if (!IsValidPosition(startX, startY, itemWidth, itemHeight))
        {
            return false;
        }

        // Place the item
        for (int x = startX; x < startX + itemWidth; x++)
        {
            for (int y = startY; y < startY + itemHeight; y++)
            {
                grid[x, y] = itemId;
            }
        }

        return true;
    }

    public void RemoveItem(int startX, int startY, int itemWidth, int itemHeight)
    {
        for (int x = startX; x < startX + itemWidth; x++)
        {
            for (int y = startY; y < startY + itemHeight; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    grid[x, y] = 0;
                }
            }
        }
    }

    public int GetCellValue(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return -1; // Out of bounds
        }
        return grid[x, y];
    }

    public Vector2Int? FindItem(int itemId)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == itemId)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return null;
    }
}