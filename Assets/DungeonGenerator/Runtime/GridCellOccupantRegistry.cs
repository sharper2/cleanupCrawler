using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    public static class GridCellOccupantRegistry
    {
        private static readonly HashSet<IGridCellOccupant> Occupants = new();
        private static readonly List<IGridCellOccupant> IterationBuffer = new();

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

        /// <summary>
        /// All registered occupants whose occupied cell is within Chebyshev distance (max of |dx|, |dy|) of <paramref name="center"/>, inclusive.
        /// Uses a snapshot so callbacks can destroy occupants without mutating the registry during iteration.
        /// </summary>
        public static void ForEachOccupantInChebyshevRadius(Vector2Int center, int radiusCells, Action<IGridCellOccupant> action)
        {
            if (action == null || radiusCells < 0)
            {
                return;
            }

            IterationBuffer.Clear();

            foreach (var occupant in Occupants)
            {
                if (occupant == null || !occupant.TryGetOccupiedCell(out var cell))
                {
                    continue;
                }

                var dx = Mathf.Abs(cell.x - center.x);
                var dy = Mathf.Abs(cell.y - center.y);
                if (Mathf.Max(dx, dy) > radiusCells)
                {
                    continue;
                }

                IterationBuffer.Add(occupant);
            }

            for (var i = 0; i < IterationBuffer.Count; i++)
            {
                action(IterationBuffer[i]);
            }
        }
    }
}
