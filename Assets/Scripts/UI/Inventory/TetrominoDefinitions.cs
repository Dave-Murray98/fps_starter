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
    Comb,      // Yellow comb/rake shape (was Cross)
    Corner     // Orange corner/truncated L shape (was ZShape)
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

        // Comb/Rake Shape - Yellow (4 horizontal cubes with cubes hanging from 2nd and 4th positions)
        _rotationStates[TetrominoType.Comb] = new TetrominoData[]
        {
            // 0° - Horizontal comb with teeth pointing down
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Left tooth base
                new Vector2Int(1, 0),     // Left middle base
                new Vector2Int(2, 0),     // Right middle base
                new Vector2Int(3, 0),     // Right tooth base
                new Vector2Int(1, 1),     // Left tooth hanging down
                new Vector2Int(3, 1)      // Right tooth hanging down
            }, new Color(1f, 1f, 0.2f)), // Yellow
            
            // 90° - Vertical comb with teeth pointing right
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Top base
                new Vector2Int(0, 1),     // Upper middle base
                new Vector2Int(0, 2),     // Lower middle base
                new Vector2Int(0, 3),     // Bottom base
                new Vector2Int(1, 1),     // Upper tooth pointing right
                new Vector2Int(1, 3)      // Lower tooth pointing right
            }, new Color(1f, 1f, 0.2f)),
            
            // 180° - Horizontal comb with teeth pointing up
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(1, 0),     // Left tooth hanging up
                new Vector2Int(3, 0),     // Right tooth hanging up
                new Vector2Int(0, 1),     // Left base
                new Vector2Int(1, 1),     // Left middle base
                new Vector2Int(2, 1),     // Right middle base
                new Vector2Int(3, 1)      // Right base
            }, new Color(1f, 1f, 0.2f)),
            
            // 270° - Vertical comb with teeth pointing left
            new TetrominoData(TetrominoType.Comb, new Vector2Int[]
            {
                new Vector2Int(1, 1),     // Upper tooth pointing left
                new Vector2Int(1, 3),     // Lower tooth pointing left
                new Vector2Int(2, 0),     // Top base
                new Vector2Int(2, 1),     // Upper middle base
                new Vector2Int(2, 2),     // Lower middle base
                new Vector2Int(2, 3)      // Bottom base
            }, new Color(1f, 1f, 0.2f))
        };

        // Corner/Truncated L Shape - Orange (3-cell L without the long arm)
        _rotationStates[TetrominoType.Corner] = new TetrominoData[]
        {
            // 0° - Corner pointing right and down
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Base corner
                new Vector2Int(1, 0),     // Right extension
                new Vector2Int(0, 1)      // Down extension
            }, new Color(1f, 0.5f, 0.2f)), // Orange
            
            // 90° - Corner pointing down and left
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Right top
                new Vector2Int(0, 1),     // Base corner
                new Vector2Int(1, 1)      // Left extension
            }, new Color(1f, 0.5f, 0.2f)),
            
            // 180° - Corner pointing left and up
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(1, 0),     // Up extension
                new Vector2Int(0, 1),     // Left extension
                new Vector2Int(1, 1)      // Base corner
            }, new Color(1f, 0.5f, 0.2f)),
            
            // 270° - Corner pointing up and right
            new TetrominoData(TetrominoType.Corner, new Vector2Int[]
            {
                new Vector2Int(0, 0),     // Left extension
                new Vector2Int(1, 0),     // Base corner
                new Vector2Int(1, 1)      // Down extension
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