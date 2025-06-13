using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Pure data representation of an inventory item - no visual dependencies
/// </summary>
[System.Serializable]
public class InventoryItemData
{
    [Header("Basic Properties")]
    public string ID;
    public Vector2Int GridPosition;

    [Header("Item Data")]
    public string itemDataName; // Name of the ItemData ScriptableObject
    [NonSerialized] private ItemData _cachedItemData; // Cache for performance

    [Header("Shape Properties")]
    public TetrominoType shapeType;
    public int currentRotation = 0;
    public int stackCount = 1;

    // Cached shape data
    private TetrominoData _currentShapeData;
    private bool _dataCached = false;

    public InventoryItemData(string id, ItemData itemData, Vector2Int gridPosition)
    {
        ID = id;
        itemDataName = itemData.name;
        _cachedItemData = itemData;
        shapeType = itemData.shapeType;
        GridPosition = gridPosition;
        currentRotation = 0;
        stackCount = 1;
        RefreshShapeData();
    }

    public ItemData ItemData
    {
        get
        {
            if (_cachedItemData == null)
            {
                _cachedItemData = FindItemDataByName(itemDataName);
            }
            return _cachedItemData;
        }
    }

    public TetrominoData CurrentShapeData
    {
        get
        {
            if (!_dataCached)
                RefreshShapeData();
            return _currentShapeData;
        }
    }

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

    public void RotateItem()
    {
        if (!CanRotate) return;

        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = (currentRotation + 1) % maxRotations;
        RefreshShapeData();
    }

    public void SetRotation(int rotation)
    {
        int maxRotations = TetrominoDefinitions.GetRotationCount(shapeType);
        currentRotation = Mathf.Clamp(rotation, 0, maxRotations - 1);
        RefreshShapeData();
    }

    public void SetGridPosition(Vector2Int position)
    {
        GridPosition = position;
    }

    public bool CanRotate
    {
        get
        {
            var itemData = ItemData;
            if (itemData != null)
                return itemData.isRotatable && TetrominoDefinitions.GetRotationCount(shapeType) > 1;
            return TetrominoDefinitions.GetRotationCount(shapeType) > 1;
        }
    }

    private void RefreshShapeData()
    {
        _currentShapeData = TetrominoDefinitions.GetRotationState(shapeType, currentRotation);
        _dataCached = true;
    }

    private ItemData FindItemDataByName(string dataName)
    {
        ItemData itemData = Resources.Load<ItemData>(dataName);
        if (itemData != null)
            return itemData;

        ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
        foreach (var data in allItemData)
        {
            if (data.name == dataName)
                return data;
        }

        Debug.LogWarning($"ItemData '{dataName}' not found. Make sure it exists in Resources folder or is loaded in memory.");
        return null;
    }

    // Convert to/from InventoryItemSaveData
    public InventoryItemSaveData ToSaveData()
    {
        return new InventoryItemSaveData(ID, itemDataName, GridPosition, currentRotation)
        {
            stackCount = stackCount
        };
    }

    public static InventoryItemData FromSaveData(InventoryItemSaveData saveData)
    {
        var itemData = Resources.Load<ItemData>(saveData.itemDataName);
        if (itemData == null)
        {
            ItemData[] allItemData = Resources.FindObjectsOfTypeAll<ItemData>();
            foreach (var data in allItemData)
            {
                if (data.name == saveData.itemDataName)
                {
                    itemData = data;
                    break;
                }
            }
        }

        if (itemData == null)
        {
            Debug.LogError($"Cannot create InventoryItemData - ItemData '{saveData.itemDataName}' not found");
            return null;
        }

        var item = new InventoryItemData(saveData.itemID, itemData, saveData.gridPosition);
        item.SetRotation(saveData.currentRotation);
        item.stackCount = saveData.stackCount;
        return item;
    }
}