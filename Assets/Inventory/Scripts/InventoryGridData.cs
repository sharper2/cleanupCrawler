using System.Collections.Generic;
using UnityEngine;

public class InventoryGridData
{
    private readonly InventoryItemInstance[,] _occupancy;
    private readonly Dictionary<InventoryItemInstance, Vector2Int> _origins = new();
    private readonly List<InventoryItemInstance> _items = new();

    public InventoryGridData(int width, int height)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        _occupancy = new InventoryItemInstance[Width, Height];
    }

    public int Width { get; }
    public int Height { get; }

    public IReadOnlyList<InventoryItemInstance> Items => _items;

    public bool TryGetOrigin(InventoryItemInstance item, out Vector2Int origin)
    {
        return _origins.TryGetValue(item, out origin);
    }

    public InventoryItemInstance GetItemAt(Vector2Int cell)
    {
        return IsWithinBounds(cell) ? _occupancy[cell.x, cell.y] : null;
    }

    public bool CanPlaceItem(IInventoryItem item, Vector2Int origin)
    {
        if (item == null || item.Definition == null)
        {
            return false;
        }

        var occupiedOffsets = item.GetOccupiedOffsets();

        for (var i = 0; i < occupiedOffsets.Count; i++)
        {
            var cell = origin + occupiedOffsets[i];
            if (!IsWithinBounds(cell))
            {
                return false;
            }

            var existing = _occupancy[cell.x, cell.y];
            if (existing != null && !ReferenceEquals(existing, item))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryPlaceItem(InventoryItemInstance item, Vector2Int origin)
    {
        if (!CanPlaceItem(item, origin))
        {
            return false;
        }

        RemoveItem(item);

        var occupiedOffsets = item.GetOccupiedOffsets();
        for (var i = 0; i < occupiedOffsets.Count; i++)
        {
            var cell = origin + occupiedOffsets[i];
            _occupancy[cell.x, cell.y] = item;
        }

        _origins[item] = origin;
        if (!_items.Contains(item))
        {
            _items.Add(item);
        }

        return true;
    }

    public bool RemoveItem(InventoryItemInstance item)
    {
        if (item == null || !_origins.TryGetValue(item, out var origin))
        {
            return false;
        }

        var occupiedOffsets = item.GetOccupiedOffsets();
        for (var i = 0; i < occupiedOffsets.Count; i++)
        {
            var cell = origin + occupiedOffsets[i];
            if (IsWithinBounds(cell) && ReferenceEquals(_occupancy[cell.x, cell.y], item))
            {
                _occupancy[cell.x, cell.y] = null;
            }
        }

        _origins.Remove(item);
        _items.Remove(item);
        return true;
    }

    public bool TryFindFirstFit(InventoryItemInstance item, out Vector2Int origin)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (CanPlaceItem(item, candidate))
                {
                    origin = candidate;
                    return true;
                }
            }
        }

        origin = default;
        return false;
    }

    private bool IsWithinBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }
}
