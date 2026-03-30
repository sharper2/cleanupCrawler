using UnityEngine;

namespace DungeonGenerator
{
    public interface IGridCellOccupant
    {
        bool TryGetOccupiedCell(out Vector2Int cell);
    }
}
