using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    public static class GridCellOccupantRegistry
    {
        private static readonly HashSet<IGridCellOccupant> Occupants = new();

        public static void Register(IGridCellOccupant occupant)
        {
            if (occupant != null)
            {
                Occupants.Add(occupant);
            }
        }

        public static void Unregister(IGridCellOccupant occupant)
        {
            if (occupant != null)
            {
                Occupants.Remove(occupant);
            }
        }

        public static bool IsCellOccupied(Vector2Int cell)
        {
            foreach (var occupant in Occupants)
            {
                if (occupant != null && occupant.TryGetOccupiedCell(out var occupiedCell) && occupiedCell == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
