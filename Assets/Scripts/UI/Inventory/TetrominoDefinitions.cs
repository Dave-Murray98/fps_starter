using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public enum TetrominoType
{
    Single,    // Pink 1x1 square
    Line2,     // Green 1x2 line
    Square,    // Blue 2x2 square
    Line4,     // Purple 1x4 line
    LShape,    // Red L-shape
    Cross,     // Yellow cross/plus shape
    ZShape     // Orange Z-shape
}

[System.Serializable]
public struct TetrominoData
{
    public TetrominoType type;
    public Vector2Int[] cells;
    public Color color;

    public TetrominoData(TetrominoType type, Vector2Int[] cells, Color color)
    {
        this.type = type;
        this.cells = cells;
        this.color = color;
    }
}

public static class TetrominoDefinitions
{
    private static readonly Dictionary<TetrominoType, TetrominoData[]> _rotationStates =
        new Dictionary<TetrominoType, TetrominoData[]>();

    static TetrominoDefinitions()
    {
        InitializeRotationStates();
    }

    private static void InitializeRotationStates()
    {
        // Single (1x1) - Pink
        _rotationStates[TetrominoType.Single] = new TetrominoData[]
        {
            new TetrominoData(TetrominoType.Single, new Vector2Int[]
            {
                new Vector2Int(0, 0)
            }, new Color(1f, 0.75f, 0.8f)) // Light pink
        };

        // Line2 (1x2) - Green
        _rotationStates[TetrominoType.Line2] = new TetrominoData[]
        {
            // Vertical
            new TetrominoData(TetrominoType.Line2, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1)
            }, new Color(0.2f, 0.8f, 0.2f)), // Green
            
            // Horizontal
            new TetrominoData(TetrominoType.Line2, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0)
            }, new Color(0.2f, 0.8f, 0.2f))
        };

        // Square (2x2) - Blue
        _rotationStates[TetrominoType.Square] = new TetrominoData[]
        {
            new TetrominoData(TetrominoType.Square, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1)
            }, new Color(0.2f, 0.6f, 1f)) // Blue
        };

        // Line4 (1x4) - Purple
        _rotationStates[TetrominoType.Line4] = new TetrominoData[]
        {
            // Horizontal
            new TetrominoData(TetrominoType.Line4, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(3, 0)
            }, new Color(0.7f, 0.3f, 1f)), // Purple
            
            // Vertical
            new TetrominoData(TetrominoType.Line4, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(0, 3)
            }, new Color(0.7f, 0.3f, 1f))
        };

        // LShape - Red
        _rotationStates[TetrominoType.LShape] = new TetrominoData[]
        {
            // 0° - L pointing right and down
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(2, 1)
            }, new Color(1f, 0.2f, 0.2f)), // Red
            
            // 90° - L pointing down and left
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2)
            }, new Color(1f, 0.2f, 0.2f)),
            
            // 180° - L pointing left and up
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1)
            }, new Color(1f, 0.2f, 0.2f)),
            
            // 270° - L pointing up and right
            new TetrominoData(TetrominoType.LShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(1, 2)
            }, new Color(1f, 0.2f, 0.2f))
        };

        // Cross/Plus Shape - Yellow
        _rotationStates[TetrominoType.Cross] = new TetrominoData[]
        {
            new TetrominoData(TetrominoType.Cross, new Vector2Int[]
            {
                new Vector2Int(1, 0),     // Top
                new Vector2Int(0, 1),     // Left
                new Vector2Int(1, 1),     // Center
                new Vector2Int(2, 1),     // Right
                new Vector2Int(3, 1),     // Far Right
                new Vector2Int(1, 2),     // Bottom Left
                new Vector2Int(2, 2)      // Bottom Right
            }, new Color(1f, 1f, 0.2f)) // Yellow
        };

        // ZShape - Orange
        _rotationStates[TetrominoType.ZShape] = new TetrominoData[]
        {
            // 0° - Z horizontal
            new TetrominoData(TetrominoType.ZShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1)
            }, new Color(1f, 0.5f, 0.2f)), // Orange
            
            // 90° - Z vertical
            new TetrominoData(TetrominoType.ZShape, new Vector2Int[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(0, 2)
            }, new Color(1f, 0.5f, 0.2f)),
            
            // 180° - Z horizontal flipped
            new TetrominoData(TetrominoType.ZShape, new Vector2Int[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1)
            }, new Color(1f, 0.5f, 0.2f)),
            
            // 270° - Z vertical flipped
            new TetrominoData(TetrominoType.ZShape, new Vector2Int[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(0, 2)
            }, new Color(1f, 0.5f, 0.2f))
        };
    }

    public static TetrominoData[] GetRotationStates(TetrominoType type)
    {
        return _rotationStates.TryGetValue(type, out var states) ? states : new TetrominoData[0];
    }

    public static TetrominoData GetRotationState(TetrominoType type, int rotation)
    {
        var states = GetRotationStates(type);
        if (states.Length == 0) return default;

        int clampedRotation = rotation % states.Length;
        return states[clampedRotation];
    }

    public static int GetRotationCount(TetrominoType type)
    {
        return GetRotationStates(type).Length;
    }
}