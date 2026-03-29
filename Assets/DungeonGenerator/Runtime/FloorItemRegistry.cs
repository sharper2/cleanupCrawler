using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    public static class FloorItemRegistry
    {
        private static readonly HashSet<IFloorItem> Items = new();

        public static void Register(IFloorItem item)
        {
            if (item != null)
            {
                Items.Add(item);
            }
        }

        public static void Unregister(IFloorItem item)
        {
            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public static bool TryGetItemAtCell(Vector2Int cell, out IFloorItem floorItem)
        {
            foreach (var item in Items)
            {
                if (item != null && item.TryGetCell(out var itemCell) && itemCell == cell)
                {
                    floorItem = item;
                    return true;
                }
            }

            floorItem = null;
            return false;
        }
    }
}
