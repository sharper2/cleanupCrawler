using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryItem", menuName = "Inventory/Item Definition")]
public class InventoryItemDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Item";
    [SerializeField] private Sprite icon;
    [SerializeField] private int value = 1;
    [SerializeField] private List<RectInt> shapeRectangles = new() { new RectInt(0, 0, 1, 1) };
    [SerializeField] private List<Vector2Int> shapeOffsets = new() { Vector2Int.zero };

    [NonSerialized] private Vector2Int[][] _cachedRotations;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int Value => Mathf.Max(0, value);

    public IReadOnlyList<Vector2Int> GetOccupiedOffsets(int rotationSteps)
    {
        EnsureCache();
        return _cachedRotations[NormalizeRotation(rotationSteps)];
    }

    private void OnValidate()
    {
        if (shapeRectangles == null)
        {
            shapeRectangles = new List<RectInt>();
        }

        if (shapeOffsets == null)
        {
            shapeOffsets = new List<Vector2Int>();
        }

        if (shapeRectangles.Count == 0 && shapeOffsets.Count == 0)
        {
            shapeRectangles.Add(new RectInt(0, 0, 1, 1));
        }

        _cachedRotations = null;
    }

    private void EnsureCache()
    {
        if (_cachedRotations != null)
        {
            return;
        }

        _cachedRotations = new Vector2Int[4][];
        var baseOffsets = BuildBaseOffsets();

        for (var step = 0; step < 4; step++)
        {
            var rotated = new Vector2Int[baseOffsets.Length];
            for (var i = 0; i < baseOffsets.Length; i++)
            {
                rotated[i] = Rotate(baseOffsets[i], step);
            }

            _cachedRotations[step] = rotated;
        }
    }

    private Vector2Int[] BuildBaseOffsets()
    {
        var cells = new HashSet<Vector2Int>();

        for (var i = 0; i < shapeRectangles.Count; i++)
        {
            var rect = shapeRectangles[i];
            var width = Mathf.Max(0, rect.width);
            var height = Mathf.Max(0, rect.height);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    cells.Add(new Vector2Int(rect.x + x, rect.y + y));
                }
            }
        }

        if (cells.Count == 0)
        {
            for (var i = 0; i < shapeOffsets.Count; i++)
            {
                cells.Add(shapeOffsets[i]);
            }
        }

        if (cells.Count == 0)
        {
            cells.Add(Vector2Int.zero);
        }

        var minX = int.MaxValue;
        var minY = int.MaxValue;

        foreach (var cell in cells)
        {
            if (cell.x < minX)
            {
                minX = cell.x;
            }

            if (cell.y < minY)
            {
                minY = cell.y;
            }
        }

        var result = new Vector2Int[cells.Count];
        var index = 0;
        foreach (var cell in cells)
        {
            result[index++] = new Vector2Int(cell.x - minX, cell.y - minY);
        }

        return result;
    }

    private static int NormalizeRotation(int rotationSteps)
    {
        var value = rotationSteps % 4;
        return value < 0 ? value + 4 : value;
    }

    private static Vector2Int Rotate(Vector2Int point, int rotationSteps)
    {
        return NormalizeRotation(rotationSteps) switch
        {
            0 => point,
            1 => new Vector2Int(point.y, -point.x),
            2 => new Vector2Int(-point.x, -point.y),
            3 => new Vector2Int(-point.y, point.x),
            _ => point
        };
    }
}
