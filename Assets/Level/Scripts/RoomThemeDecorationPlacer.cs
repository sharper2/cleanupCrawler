using System.Collections.Generic;
using DungeonGenerator;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    public class RoomThemeDecorationPlacer : MonoBehaviour, ILevelDecorationPlacer
    {
        [Header("Themes")]
        [SerializeField] private List<WeightedRoomTheme> roomThemes = new();

        [Header("Corridors")]
        [SerializeField] private List<LevelDecorationEntry> corridorDecorationPool = new();
        [SerializeField, Min(1)] private int corridorSpacingCells = 5;

        [Header("Room spacing")]
        [Tooltip("After placing a wall decoration, remove other wall anchors whose floor cell is within this Manhattan distance (same rule as corridor spacing). Higher = fewer, more spread-out props.")]
        [SerializeField, Min(1)] private int roomWallAnchorSpacingCells = 4;
        [Tooltip("After placing a floor or ceiling decoration, remove candidate cells within this Manhattan distance.")]
        [SerializeField, Min(1)] private int roomFloorDecorationSpacingCells = 3;

        [Header("Placement Heights")]
        [SerializeField] private float floorDecorationYOffset = 0.5f;
        [SerializeField] private float lowWallDecorationYOffset = 0.6f;
        [SerializeField] private float highWallDecorationYOffset = 1.6f;
        [SerializeField] private float ceilingInsetFromTop = 0.2f;

        [Header("Gameplay Safety")]
        [SerializeField] private bool disableDecorationColliders = true;

        private readonly List<GameObject> _spawnedDecorations = new();

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        private struct WallAnchor
        {
            /// <summary>Walkable cell touching the wall.</summary>
            public Vector2Int Cell;
            /// <summary>Grid step from <see cref="Cell"/> toward the non-walkable wall cell (interior → wall).</summary>
            public Vector2Int Direction;
            /// <summary>Unit vector on XZ from room interior toward the wall (world space).</summary>
            public Vector3 IntoWall;
        }

        public void ApplyDecorations(LevelBuildContext context)
        {
            ClearSpawnedDecorations();

            if (context == null || context.DungeonBuilder == null || context.WalkableCells == null || context.WalkableCells.Count == 0)
            {
                return;
            }

            var walkable = new HashSet<Vector2Int>(context.WalkableCells);
            var gridSize = context.DungeonBuilder != null && context.DungeonBuilder.settings != null
                ? context.DungeonBuilder.settings.gridSize
                : Vector2Int.zero;
            var interiorNonWalkable = BuildInteriorNonWalkableCells(walkable, gridSize.x, gridSize.y);
            var roomCore = CollectRoomCoreCells(walkable);
            var roomGroups = GroupConnectedCells(roomCore);
            var roomCellsCombined = new HashSet<Vector2Int>(roomCore);

            for (var i = 0; i < roomGroups.Count; i++)
            {
                var theme = SelectWeightedTheme(roomThemes);
                if (theme == null)
                {
                    break;
                }

                PlaceThemeInRoom(context.DungeonBuilder, walkable, roomGroups[i], theme, interiorNonWalkable, gridSize.x, gridSize.y);
            }

            var corridorCells = new List<Vector2Int>();
            foreach (var cell in walkable)
            {
                if (!roomCellsCombined.Contains(cell))
                {
                    corridorCells.Add(cell);
                }
            }

            PlaceCorridorDecorations(context.DungeonBuilder, walkable, corridorCells, interiorNonWalkable, gridSize.x, gridSize.y);
        }

        private void PlaceThemeInRoom(DungeonBasic3DBuilder builder, HashSet<Vector2Int> walkable, HashSet<Vector2Int> roomCells, RoomThemeDefinition theme, HashSet<Vector2Int> interiorNonWalkable, int gridW, int gridH)
        {
            var perimeterAnchors = BuildPerimeterAnchors(roomCells, walkable, interiorNonWalkable, gridW, gridH);
            var availableFloorCells = new List<Vector2Int>(roomCells);
            int roomCellCount = roomCells.Count;

            PlaceWallCategory(builder, perimeterAnchors, theme.LowWallDecorations, lowWallDecorationYOffset, roomCellCount, roomWallAnchorSpacingCells);
            PlaceWallCategory(builder, perimeterAnchors, theme.HighWallDecorations, highWallDecorationYOffset, roomCellCount, roomWallAnchorSpacingCells);
            PlaceFloorLikeCategory(builder, availableFloorCells, theme.FloorDecorations, floorDecorationYOffset, false, roomCellCount, roomFloorDecorationSpacingCells);
            PlaceFloorLikeCategory(builder, availableFloorCells, theme.CeilingDecorations, ComputeCeilingY(builder), true, roomCellCount, roomFloorDecorationSpacingCells);
        }

        private float ComputeCeilingY(DungeonBasic3DBuilder builder)
        {
            return Mathf.Max(builder.floorHeight, builder.wallHeight - Mathf.Max(0f, ceilingInsetFromTop));
        }

        /// <summary>
        /// Derives spawn count from min/max at a reference size. Walls scale by outer-wall anchor count when
        /// <see cref="ThemeDecorationEntry.ReferenceWallAnchorCount"/> is set, else by room cell count when a floor reference is set.
        /// Floor/ceiling scale by room cell count when <see cref="ThemeDecorationEntry.ReferenceFloorCellCount"/> is set.
        /// </summary>
        private static int ComputeThemeDecorationSpawnCount(ThemeDecorationEntry entry, int roomCellCount, int wallOrFloorMetric, bool isWallCategory)
        {
            float scale;

            if (isWallCategory)
            {
                if (entry.ReferenceWallAnchorCount > 0)
                {
                    scale = wallOrFloorMetric / Mathf.Max(1f, entry.ReferenceWallAnchorCount);
                }
                else if (entry.ReferenceFloorCellCount > 0)
                {
                    scale = roomCellCount / Mathf.Max(1f, entry.ReferenceFloorCellCount);
                }
                else
                {
                    return Random.Range(entry.MinPerRoom, entry.MaxPerRoom + 1);
                }
            }
            else
            {
                if (entry.ReferenceFloorCellCount > 0)
                {
                    scale = wallOrFloorMetric / Mathf.Max(1f, entry.ReferenceFloorCellCount);
                }
                else
                {
                    return Random.Range(entry.MinPerRoom, entry.MaxPerRoom + 1);
                }
            }

            var minS = Mathf.RoundToInt(entry.MinPerRoom * scale);
            var maxS = Mathf.RoundToInt(entry.MaxPerRoom * scale);
            minS = Mathf.Max(0, minS);
            maxS = Mathf.Max(minS, maxS);
            return Random.Range(minS, maxS + 1);
        }

        private void PlaceWallCategory(DungeonBasic3DBuilder builder, List<WallAnchor> anchors, IReadOnlyList<ThemeDecorationEntry> entries, float y, int roomCellCount, int spacingCells)
        {
            if (entries == null || anchors.Count == 0)
            {
                return;
            }

            int initialWallAnchors = anchors.Count;
            int spacing = Mathf.Max(1, spacingCells);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                int count = ComputeThemeDecorationSpawnCount(entry, roomCellCount, initialWallAnchors, true);
                for (var n = 0; n < count && anchors.Count > 0; n++)
                {
                    int index = Random.Range(0, anchors.Count);
                    var anchor = anchors[index];
                    anchors[index] = anchors[anchors.Count - 1];
                    anchors.RemoveAt(anchors.Count - 1);

                    var position = WallFaceWorldPosition(builder, anchor, y);
                    var rotation = Quaternion.LookRotation(-anchor.IntoWall, Vector3.up);
                    SpawnDecoration(entry.Prefab, position, rotation, entry.PositionOffset, entry.RotationOffsetEuler, entry.ApplyPrefabTransformOffsets);

                    RemoveWallAnchorsNearCell(anchors, anchor.Cell, spacing);
                }
            }
        }

        private void PlaceFloorLikeCategory(DungeonBasic3DBuilder builder, List<Vector2Int> cells, IReadOnlyList<ThemeDecorationEntry> entries, float y, bool alignToCeiling, int roomCellCount, int spacingCells)
        {
            if (entries == null || cells.Count == 0)
            {
                return;
            }

            int initialFloorCells = cells.Count;
            int spacing = Mathf.Max(1, spacingCells);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                int count = ComputeThemeDecorationSpawnCount(entry, roomCellCount, initialFloorCells, false);
                for (var n = 0; n < count && cells.Count > 0; n++)
                {
                    int index = Random.Range(0, cells.Count);
                    var cell = cells[index];
                    cells[index] = cells[cells.Count - 1];
                    cells.RemoveAt(cells.Count - 1);

                    var position = builder.CellCenterToWorld(cell, y);
                    var rotation = alignToCeiling ? Quaternion.Euler(180f, Random.Range(0f, 360f), 0f) : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    SpawnDecoration(entry.Prefab, position, rotation, entry.PositionOffset, entry.RotationOffsetEuler, entry.ApplyPrefabTransformOffsets);

                    RemoveFloorCellsNear(cells, cell, spacing);
                }
            }
        }

        private static void RemoveWallAnchorsNearCell(List<WallAnchor> anchors, Vector2Int placedCell, int spacingCells)
        {
            anchors.RemoveAll(a => ManhattanDistance(a.Cell, placedCell) < spacingCells);
        }

        private static void RemoveFloorCellsNear(List<Vector2Int> cells, Vector2Int placedCell, int spacingCells)
        {
            cells.RemoveAll(c => ManhattanDistance(c, placedCell) < spacingCells);
        }

        private void PlaceCorridorDecorations(DungeonBasic3DBuilder builder, HashSet<Vector2Int> walkable, List<Vector2Int> corridorCells, HashSet<Vector2Int> interiorNonWalkable, int gridW, int gridH)
        {
            if (corridorCells.Count == 0 || corridorDecorationPool == null || corridorDecorationPool.Count == 0)
            {
                return;
            }

            var anchors = BuildCorridorWallAnchors(walkable, corridorCells, interiorNonWalkable, gridW, gridH);
            if (anchors.Count == 0)
            {
                return;
            }

            int spacing = Mathf.Max(1, corridorSpacingCells);

            while (anchors.Count > 0)
            {
                int index = Random.Range(0, anchors.Count);
                var anchor = anchors[index];
                anchors[index] = anchors[anchors.Count - 1];
                anchors.RemoveAt(anchors.Count - 1);

                var entry = SelectWeightedDecoration(corridorDecorationPool);
                if (entry != null && entry.Prefab != null)
                {
                    var position = WallFaceWorldPosition(builder, anchor, highWallDecorationYOffset);
                    var rotation = Quaternion.LookRotation(-anchor.IntoWall, Vector3.up);
                    SpawnDecoration(entry.Prefab, position, rotation, entry.PositionOffset, entry.RotationOffsetEuler, entry.ApplyPrefabTransformOffsets);
                }

                anchors.RemoveAll(a => ManhattanDistance(a.Cell, anchor.Cell) < spacing);
            }
        }

        private void SpawnDecoration(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 positionOffset, Vector3 rotationOffsetEuler, bool applyPrefabTransformOffsets)
        {
            var prefabPositionOffset = applyPrefabTransformOffsets ? prefab.transform.localPosition : Vector3.zero;
            var prefabRotationOffset = applyPrefabTransformOffsets ? prefab.transform.localRotation : Quaternion.identity;

            var finalRotation = rotation * prefabRotationOffset * Quaternion.Euler(rotationOffsetEuler);
            // Prefab root position is in placement space; theme offsets are in the decoration's final local space.
            var finalPosition = position + rotation * prefabPositionOffset + finalRotation * positionOffset;

            var instance = Instantiate(prefab, finalPosition, finalRotation, transform);
            _spawnedDecorations.Add(instance);

            if (!disableDecorationColliders)
            {
                return;
            }

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private static Vector3 WallFaceWorldPosition(DungeonBasic3DBuilder builder, WallAnchor anchor, float y)
        {
            var center = builder.CellCenterToWorld(anchor.Cell, y);
            var intoWall = anchor.IntoWall;
            return center + intoWall * (builder.cellSize * 0.5f);
        }

        private static List<WallAnchor> BuildPerimeterAnchors(HashSet<Vector2Int> roomCells, HashSet<Vector2Int> walkable, HashSet<Vector2Int> interiorNonWalkable, int gridW, int gridH)
        {
            var anchors = new List<WallAnchor>();

            foreach (var cell in roomCells)
            {
                for (var i = 0; i < Directions.Length; i++)
                {
                    var direction = Directions[i];
                    var outside = cell + direction;
                    if (walkable.Contains(outside))
                    {
                        continue;
                    }

                    if (IsNonWalkableInteriorHoleFace(outside, interiorNonWalkable, gridW, gridH))
                    {
                        continue;
                    }

                    var intoWall = GridDirectionToWorldXZ(direction);
                    anchors.Add(new WallAnchor { Cell = cell, Direction = direction, IntoWall = intoWall });
                }
            }

            return anchors;
        }

        private static List<WallAnchor> BuildCorridorWallAnchors(HashSet<Vector2Int> walkable, List<Vector2Int> corridorCells, HashSet<Vector2Int> interiorNonWalkable, int gridW, int gridH)
        {
            var anchors = new List<WallAnchor>();

            for (var c = 0; c < corridorCells.Count; c++)
            {
                var cell = corridorCells[c];
                for (var i = 0; i < Directions.Length; i++)
                {
                    var direction = Directions[i];
                    var outside = cell + direction;
                    if (walkable.Contains(outside))
                    {
                        continue;
                    }

                    if (IsNonWalkableInteriorHoleFace(outside, interiorNonWalkable, gridW, gridH))
                    {
                        continue;
                    }

                    var intoWall = GridDirectionToWorldXZ(direction);
                    anchors.Add(new WallAnchor { Cell = cell, Direction = direction, IntoWall = intoWall });
                }
            }

            return anchors;
        }

        /// <summary>
        /// Non-walkable cells reachable from the grid border through other non-walkable cells are exterior (map edge / outer walls).
        /// Remaining in-bounds non-walkable cells are interior holes or pillars — faces toward those read as "middle of the room", not outer walls.
        /// </summary>
        private static HashSet<Vector2Int> BuildInteriorNonWalkableCells(HashSet<Vector2Int> walkable, int gridW, int gridH)
        {
            var interior = new HashSet<Vector2Int>();
            if (gridW <= 0 || gridH <= 0 || walkable == null)
            {
                return interior;
            }

            var exteriorReachable = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            void TrySeedBorder(Vector2Int cell)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= gridW || cell.y >= gridH)
                {
                    return;
                }

                if (walkable.Contains(cell) || exteriorReachable.Contains(cell))
                {
                    return;
                }

                exteriorReachable.Add(cell);
                queue.Enqueue(cell);
            }

            for (var x = 0; x < gridW; x++)
            {
                TrySeedBorder(new Vector2Int(x, 0));
                TrySeedBorder(new Vector2Int(x, gridH - 1));
            }

            for (var y = 0; y < gridH; y++)
            {
                TrySeedBorder(new Vector2Int(0, y));
                TrySeedBorder(new Vector2Int(gridW - 1, y));
            }

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (var i = 0; i < Directions.Length; i++)
                {
                    var n = c + Directions[i];
                    if (n.x < 0 || n.y < 0 || n.x >= gridW || n.y >= gridH)
                    {
                        continue;
                    }

                    if (walkable.Contains(n) || exteriorReachable.Contains(n))
                    {
                        continue;
                    }

                    exteriorReachable.Add(n);
                    queue.Enqueue(n);
                }
            }

            for (var x = 0; x < gridW; x++)
            {
                for (var y = 0; y < gridH; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (walkable.Contains(cell) || exteriorReachable.Contains(cell))
                    {
                        continue;
                    }

                    interior.Add(cell);
                }
            }

            return interior;
        }

        private static bool IsNonWalkableInteriorHoleFace(Vector2Int outside, HashSet<Vector2Int> interiorNonWalkable, int gridW, int gridH)
        {
            if (interiorNonWalkable == null || interiorNonWalkable.Count == 0)
            {
                return false;
            }

            if (outside.x < 0 || outside.y < 0 || outside.x >= gridW || outside.y >= gridH)
            {
                return false;
            }

            return interiorNonWalkable.Contains(outside);
        }

        private static Vector3 GridDirectionToWorldXZ(Vector2Int gridDirection)
        {
            return new Vector3(gridDirection.x, 0f, gridDirection.y).normalized;
        }

        private static HashSet<Vector2Int> CollectRoomCoreCells(HashSet<Vector2Int> walkable)
        {
            var result = new HashSet<Vector2Int>();

            foreach (var cell in walkable)
            {
                var neighborCount = 0;
                for (var i = 0; i < Directions.Length; i++)
                {
                    if (walkable.Contains(cell + Directions[i]))
                    {
                        neighborCount++;
                    }
                }

                if (neighborCount >= 3)
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        private static List<HashSet<Vector2Int>> GroupConnectedCells(HashSet<Vector2Int> cells)
        {
            var groups = new List<HashSet<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();

            foreach (var cell in cells)
            {
                if (visited.Contains(cell))
                {
                    continue;
                }

                var group = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(cell);
                visited.Add(cell);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);

                    for (var i = 0; i < Directions.Length; i++)
                    {
                        var next = current + Directions[i];
                        if (!cells.Contains(next) || visited.Contains(next))
                        {
                            continue;
                        }

                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private static RoomThemeDefinition SelectWeightedTheme(IReadOnlyList<WeightedRoomTheme> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var totalWeight = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.Theme != null)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            var roll = Random.Range(0, totalWeight);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Theme == null)
                {
                    continue;
                }

                roll -= entry.Weight;
                if (roll < 0)
                {
                    return entry.Theme;
                }
            }

            return null;
        }

        private static LevelDecorationEntry SelectWeightedDecoration(IReadOnlyList<LevelDecorationEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return null;
            }

            var totalWeight = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.Prefab != null)
                {
                    totalWeight += entry.Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            var roll = Random.Range(0, totalWeight);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                roll -= entry.Weight;
                if (roll < 0)
                {
                    return entry;
                }
            }

            return null;
        }

        private void ClearSpawnedDecorations()
        {
            for (var i = 0; i < _spawnedDecorations.Count; i++)
            {
                if (_spawnedDecorations[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(_spawnedDecorations[i]);
                }
                else
                {
                    DestroyImmediate(_spawnedDecorations[i]);
                }
            }

            _spawnedDecorations.Clear();
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
