using UnityEngine;

namespace DungeonGenerator
{
    public interface IFloorItem
    {
        bool TryGetCell(out Vector2Int cell);
        bool TryCollect(GameObject collector);
    }
}
