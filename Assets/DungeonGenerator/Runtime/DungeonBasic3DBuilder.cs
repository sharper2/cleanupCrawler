using System.Collections.Generic;
using DungeonGenerator.Data;
using UnityEngine;

namespace DungeonGenerator
{
    [ExecuteAlways]
    public class DungeonBasic3DBuilder : MonoBehaviour
    {
        public enum PreviewStyle
        {
            SolidVolume,
            DiagramLike
        }

        [Header("Generation")]
        public GeneratorSettings settings = new GeneratorSettings();
        public bool useExternalGraphPreview;
        public DungeonGraphGizmoPreview externalGraphPreview;

        [Header("Preview")]
        public PreviewStyle previewStyle = PreviewStyle.SolidVolume;

        [Header("Layout")]
        public float cellSize = 2f;
        public float floorHeight = 0.1f;
        public float wallHeight = 2f;
        [Min(1)] public int corridorWidthCells = 1;
        public float connectionWidth = 0.35f;
        public Vector3 worldOffset = Vector3.zero;

        [Header("Prefabs / Materials")]
        public GameObject floorPrefab;
        public GameObject wallPrefab;
        public GameObject ceilingPrefab;
        public Material floorMaterial;
        public Material wallMaterial;
        public Material ceilingMaterial;

        [Header("Floor Items")]
        public bool spawnFloorItems = true;
        public List<GameObject> floorItemPrefabs = new List<GameObject>();
        public int minFloorItemsPerRoom = 0;
        public int maxFloorItemsPerRoom = 2;
        public int minFloorItemsPerCorridor = 0;
        public int maxFloorItemsPerCorridor = 1;
        public float floorItemYOffset = 0.5f;

        [Header("Fallback Colors")]
        public Color fallbackFloorColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        public Color fallbackCorridorColor = new Color(0.5f, 0.7f, 0.55f, 1f);
        public Color fallbackWallColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        public Color fallbackCeilingColor = new Color(0.2f, 0.2f, 0.24f, 1f);
        public Color fallbackStartColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        public Color fallbackExitColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        public Color fallbackConnectionColor = new Color(0.35f, 0.65f, 0.95f, 1f);

        [SerializeField] private Transform _generatedRoot;
        [SerializeField] private int _lastGeneratedNodeCount;
        [SerializeField] private Vector2Int _lastStartCell;
        [SerializeField] private bool _hasStartCell;
        [SerializeField] private List<Vector2Int> _serializedWalkableCells = new List<Vector2Int>();

        private readonly HashSet<Vector2Int> _walkableCells = new HashSet<Vector2Int>();

        private Material _generatedFloorMaterial;
        private Material _generatedCorridorMaterial;
        private Material _generatedWallMaterial;
        private Material _generatedCeilingMaterial;
        private Material _generatedStartMaterial;
        private Material _generatedExitMaterial;
        private Material _generatedConnectionMaterial;

        public int WalkableCellCount => _walkableCells.Count;

        private void OnEnable()
        {
            RestoreWalkableCache();
        }

        public void BuildDungeon()
        {
            ClearDungeon();
            InvalidateGeneratedMaterialCache();

            if (settings == null)
                settings = new GeneratorSettings();

            DungeonGraph graph = null;

            if (useExternalGraphPreview && externalGraphPreview != null)
            {
                if (externalGraphPreview.PreviewGraph == null)
                    externalGraphPreview.Regenerate();

                graph = externalGraphPreview.PreviewGraph;
            }

            if (graph == null)
                graph = new RoomPlacementGenerator().Generate(settings);

            _lastGeneratedNodeCount = graph != null ? graph.NodeCount : 0;

            _walkableCells.Clear();
            _serializedWalkableCells.Clear();
            _hasStartCell = false;

            if (graph != null)
            {
                var generatedFloorCells = BuildFloorCells(graph);

                foreach (var cell in generatedFloorCells)
                {
                    _walkableCells.Add(cell);
                    _serializedWalkableCells.Add(cell);
                }

                _hasStartCell = TryGetStartCellFromGraph(graph, out _lastStartCell);
            }

            EnsureRoot();

            if (previewStyle == PreviewStyle.DiagramLike)
            {
                CreateDiagramRoomObjects(graph);
                CreateDiagramConnectionObjects(graph);
                return;
            }

            var floorCells = new HashSet<Vector2Int>(_walkableCells);
            var roomCells = BuildRoomCells(graph);
            CreateFloorObjects(floorCells, roomCells);
            CreateCeilingObjects(floorCells);
            CreateWallObjects(floorCells);
            CreateBorderWallObjects();
            if (spawnFloorItems)
                CreateFloorItemObjects(graph, floorCells, roomCells);
        }

        /// <summary>
        /// Clears cached Lit materials so the next build picks up current fallback colors (e.g. after a level profile change).
        /// </summary>
        public void InvalidateGeneratedMaterialCache()
        {
            DestroyGeneratedMaterial(ref _generatedFloorMaterial);
            DestroyGeneratedMaterial(ref _generatedCorridorMaterial);
            DestroyGeneratedMaterial(ref _generatedWallMaterial);
            DestroyGeneratedMaterial(ref _generatedCeilingMaterial);
            DestroyGeneratedMaterial(ref _generatedStartMaterial);
            DestroyGeneratedMaterial(ref _generatedExitMaterial);
            DestroyGeneratedMaterial(ref _generatedConnectionMaterial);
        }

        private static void DestroyGeneratedMaterial(ref Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }

            material = null;
        }

        public void ClearDungeon()
        {
            _walkableCells.Clear();
            _serializedWalkableCells.Clear();
            _hasStartCell = false;

            if (_generatedRoot == null)
                return;

            for (int i = _generatedRoot.childCount - 1; i >= 0; i--)
            {
                var child = _generatedRoot.GetChild(i).gameObject;

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        private void CreateFloorItemObjects(DungeonGraph graph, HashSet<Vector2Int> floorCells, HashSet<Vector2Int> roomCells)
        {
            if (graph == null || floorCells == null || floorCells.Count == 0 || roomCells == null)
                return;

            if (floorItemPrefabs == null || floorItemPrefabs.Count == 0)
                return;

            var validPrefabs = new List<GameObject>();
            for (int i = 0; i < floorItemPrefabs.Count; i++)
            {
                if (floorItemPrefabs[i] != null)
                    validPrefabs.Add(floorItemPrefabs[i]);
            }

            if (validPrefabs.Count == 0)
                return;

            var usedCells = new HashSet<Vector2Int>();

            foreach (var node in graph.Nodes)
            {
                var roomCandidateCells = new List<Vector2Int>();

                for (int x = node.GridPosition.x; x < node.GridPosition.x + node.Size.x; x++)
                {
                    for (int y = node.GridPosition.y; y < node.GridPosition.y + node.Size.y; y++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (_hasStartCell && cell == _lastStartCell)
                            continue;
                        if (usedCells.Contains(cell))
                            continue;

                        roomCandidateCells.Add(cell);
                    }
                }

                SpawnItemsOnCells(roomCandidateCells, minFloorItemsPerRoom, maxFloorItemsPerRoom, validPrefabs, usedCells);
            }

            var corridorCandidateCellsByEdge = BuildCorridorCellGroups(graph, roomCells);
            for (int i = 0; i < corridorCandidateCellsByEdge.Count; i++)
            {
                var corridorCells = corridorCandidateCellsByEdge[i];
                corridorCells.RemoveAll(cell => usedCells.Contains(cell) || (_hasStartCell && cell == _lastStartCell));
                SpawnItemsOnCells(corridorCells, minFloorItemsPerCorridor, maxFloorItemsPerCorridor, validPrefabs, usedCells);
            }
        }

        private void SpawnItemsOnCells(List<Vector2Int> candidateCells, int minCount, int maxCount, List<GameObject> validPrefabs, HashSet<Vector2Int> usedCells)
        {
            if (candidateCells == null || candidateCells.Count == 0)
                return;

            int clampedMin = Mathf.Max(0, minCount);
            int clampedMax = Mathf.Max(clampedMin, maxCount);
            int desiredCount = Mathf.Clamp(Random.Range(clampedMin, clampedMax + 1), 0, candidateCells.Count);

            for (int i = 0; i < desiredCount && candidateCells.Count > 0; i++)
            {
                int cellIndex = Random.Range(0, candidateCells.Count);
                Vector2Int cell = candidateCells[cellIndex];
                candidateCells.RemoveAt(cellIndex);
                usedCells.Add(cell);

                var prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
                CreateFloorItemObject(cell, prefab);
            }
        }

        private List<List<Vector2Int>> BuildCorridorCellGroups(DungeonGraph graph, HashSet<Vector2Int> roomCells)
        {
            var corridorGroups = new List<List<Vector2Int>>();
            var uniqueEdges = new HashSet<string>();

            foreach (var node in graph.Nodes)
            {
                foreach (var edge in graph.GetEdgesForNode(node.ID))
                {
                    string otherID = edge.GetOtherNodeID(node.ID);
                    if (otherID == null)
                        continue;

                    string a = string.CompareOrdinal(node.ID, otherID) <= 0 ? node.ID : otherID;
                    string b = string.CompareOrdinal(node.ID, otherID) <= 0 ? otherID : node.ID;
                    string key = a + "|" + b;

                    if (!uniqueEdges.Add(key))
                        continue;

                    DungeonNode other = graph.GetNodeByID(otherID);
                    if (other == null)
                        continue;

                    Vector2Int from = GetNodeCenterCell(node);
                    Vector2Int to = GetNodeCenterCell(other);

                    var corridorCells = new HashSet<Vector2Int>();
                    AddCorridorCellsLShaped(corridorCells, from, to, Mathf.Max(1, corridorWidthCells));

                    var filteredCells = new List<Vector2Int>();
                    foreach (var cell in corridorCells)
                    {
                        if (!roomCells.Contains(cell))
                            filteredCells.Add(cell);
                    }

                    if (filteredCells.Count > 0)
                        corridorGroups.Add(filteredCells);
                }
            }

            return corridorGroups;
        }

        private void CreateFloorItemObject(Vector2Int cell, GameObject prefab)
        {
            if (prefab == null)
                return;

            var instance = Instantiate(prefab, _generatedRoot);

            instance.name = "FloorItem_" + cell.x + "_" + cell.y;
            instance.transform.position = CellCenterToWorld(cell, floorItemYOffset);
        }

        public bool IsCellWalkable(Vector2Int cell)
        {
            if (_walkableCells.Count == 0 && _serializedWalkableCells.Count > 0)
                RestoreWalkableCache();

            return _walkableCells.Contains(cell);
        }

        public bool TryGetStartCell(out Vector2Int cell)
        {
            cell = _lastStartCell;
            return _hasStartCell;
        }

        public bool TryWorldToCell(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = default;

            if (cellSize <= 0f)
                return false;

            Vector3 basePos = transform.position + worldOffset;
            Vector3 local = worldPosition - basePos;

            cell = new Vector2Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.z / cellSize)
            );

            return true;
        }

        private void CreateDiagramRoomObjects(DungeonGraph graph)
        {
            Material roomMaterial = ResolveMaterial(floorMaterial, fallbackFloorColor, ref _generatedFloorMaterial, "Generated_DungeonRoomMaterial");
            Material startMaterial = ResolveMaterial(floorMaterial, fallbackStartColor, ref _generatedStartMaterial, "Generated_DungeonStartMaterial");
            Material exitMaterial = ResolveMaterial(floorMaterial, fallbackExitColor, ref _generatedExitMaterial, "Generated_DungeonExitMaterial");

            foreach (var node in graph.Nodes)
            {
                var go = CreateObject(floorPrefab, "Room_" + node.ID);
                go.transform.localScale = new Vector3(node.Size.x * cellSize, floorHeight, node.Size.y * cellSize);
                go.transform.position = GetNodeCenterWorld(node, floorHeight * 0.5f);

                Material material = roomMaterial;
                if (node.NodeType == DungeonNodeType.Start)
                    material = startMaterial;
                else if (node.NodeType == DungeonNodeType.Exit)
                    material = exitMaterial;

                ApplyMaterial(go, material);
            }
        }

        private void CreateDiagramConnectionObjects(DungeonGraph graph)
        {
            Material connectionMaterial = ResolveMaterial(wallMaterial, fallbackConnectionColor, ref _generatedConnectionMaterial, "Generated_DungeonConnectionMaterial");
            var uniqueEdges = new HashSet<string>();

            foreach (var node in graph.Nodes)
            {
                foreach (var edge in graph.GetEdgesForNode(node.ID))
                {
                    string otherID = edge.GetOtherNodeID(node.ID);
                    if (otherID == null)
                        continue;

                    string a = string.CompareOrdinal(node.ID, otherID) <= 0 ? node.ID : otherID;
                    string b = string.CompareOrdinal(node.ID, otherID) <= 0 ? otherID : node.ID;
                    if (!uniqueEdges.Add(a + "|" + b))
                        continue;

                    var other = graph.GetNodeByID(otherID);
                    if (other == null)
                        continue;

                    Vector3 from = GetNodeCenterWorld(node, floorHeight * 0.5f);
                    Vector3 to = GetNodeCenterWorld(other, floorHeight * 0.5f);
                    CreateConnectionBeam(from, to, connectionMaterial, a + "_to_" + b);
                }
            }
        }

        private void CreateConnectionBeam(Vector3 from, Vector3 to, Material material, string name)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
                return;

            var go = CreateObject(floorPrefab, "Connection_" + name);
            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            go.transform.localScale = new Vector3(connectionWidth, floorHeight, distance);
            ApplyMaterial(go, material);
        }

        private void EnsureRoot()
        {
            if (_generatedRoot != null)
                return;

            var rootObject = new GameObject("GeneratedDungeon");
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;
        }

        private HashSet<Vector2Int> BuildFloorCells(DungeonGraph graph)
        {
            var floorCells = new HashSet<Vector2Int>();

            foreach (var node in graph.Nodes)
            {
                for (int x = node.GridPosition.x; x < node.GridPosition.x + node.Size.x; x++)
                {
                    for (int y = node.GridPosition.y; y < node.GridPosition.y + node.Size.y; y++)
                        floorCells.Add(new Vector2Int(x, y));
                }
            }

            var uniqueEdges = new HashSet<string>();
            foreach (var node in graph.Nodes)
            {
                foreach (var edge in graph.GetEdgesForNode(node.ID))
                {
                    string otherID = edge.GetOtherNodeID(node.ID);
                    if (otherID == null)
                        continue;

                    string a = string.CompareOrdinal(node.ID, otherID) <= 0 ? node.ID : otherID;
                    string b = string.CompareOrdinal(node.ID, otherID) <= 0 ? otherID : node.ID;
                    string key = a + "|" + b;

                    if (!uniqueEdges.Add(key))
                        continue;

                    DungeonNode other = graph.GetNodeByID(otherID);
                    if (other == null)
                        continue;

                    Vector2Int from = GetNodeCenterCell(node);
                    Vector2Int to = GetNodeCenterCell(other);

                    AddCorridorCellsLShaped(floorCells, from, to, Mathf.Max(1, corridorWidthCells));
                }
            }

            return floorCells;
        }

        private HashSet<Vector2Int> BuildRoomCells(DungeonGraph graph)
        {
            var roomCells = new HashSet<Vector2Int>();

            foreach (var node in graph.Nodes)
            {
                for (int x = node.GridPosition.x; x < node.GridPosition.x + node.Size.x; x++)
                {
                    for (int y = node.GridPosition.y; y < node.GridPosition.y + node.Size.y; y++)
                        roomCells.Add(new Vector2Int(x, y));
                }
            }

            return roomCells;
        }

        private static Vector2Int GetNodeCenterCell(DungeonNode node)
        {
            return new Vector2Int(
                node.GridPosition.x + (node.Size.x / 2),
                node.GridPosition.y + (node.Size.y / 2)
            );
        }

        private static void AddCorridorCellsLShaped(HashSet<Vector2Int> cells, Vector2Int from, Vector2Int to, int width)
        {
            int clampedWidth = Mathf.Max(1, width);
            int radius = clampedWidth / 2;

            void AddWideCell(Vector2Int center)
            {
                for (int ox = -radius; ox <= radius; ox++)
                {
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        cells.Add(new Vector2Int(center.x + ox, center.y + oy));
                    }
                }
            }

            int stepX = from.x <= to.x ? 1 : -1;
            int stepY = from.y <= to.y ? 1 : -1;

            for (int x = from.x; x != to.x; x += stepX)
                AddWideCell(new Vector2Int(x, from.y));
            AddWideCell(new Vector2Int(to.x, from.y));

            for (int y = from.y; y != to.y; y += stepY)
                AddWideCell(new Vector2Int(to.x, y));
            AddWideCell(to);
        }

        private void CreateFloorObjects(HashSet<Vector2Int> floorCells, HashSet<Vector2Int> roomCells)
        {
            Material roomMaterial = ResolveMaterial(floorMaterial, fallbackFloorColor, ref _generatedFloorMaterial, "Generated_DungeonRoomMaterial");
            Material corridorMaterial = ResolveMaterial(floorMaterial, fallbackCorridorColor, ref _generatedCorridorMaterial, "Generated_DungeonCorridorMaterial");

            foreach (var cell in floorCells)
            {
                var go = CreateTileObject(floorPrefab, "Floor", cell);
                go.transform.localScale = new Vector3(cellSize, floorHeight, cellSize);
                go.transform.position = CellCenterToWorld(cell, floorHeight * 0.5f);

                Material material = roomCells.Contains(cell) ? roomMaterial : corridorMaterial;
                ApplyMaterial(go, material);
            }
        }

        private void CreateCeilingObjects(HashSet<Vector2Int> floorCells)
        {
            Material material = ResolveMaterial(ceilingMaterial, fallbackCeilingColor, ref _generatedCeilingMaterial, "Generated_DungeonCeilingMaterial");
            float ceilingY = Mathf.Max(floorHeight * 0.5f, wallHeight - floorHeight * 0.5f);

            foreach (var cell in floorCells)
            {
                var go = CreateTileObject(ceilingPrefab != null ? ceilingPrefab : floorPrefab, "Ceiling", cell);
                go.transform.localScale = new Vector3(cellSize, floorHeight, cellSize);
                go.transform.position = CellCenterToWorld(cell, ceilingY);
                ApplyMaterial(go, material);
            }
        }

        private void CreateWallObjects(HashSet<Vector2Int> floorCells)
        {
            int width = Mathf.Max(0, settings.gridSize.x);
            int height = Mathf.Max(0, settings.gridSize.y);
            Material material = ResolveMaterial(wallMaterial, fallbackWallColor, ref _generatedWallMaterial, "Generated_DungeonWallMaterial");

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (floorCells.Contains(cell))
                        continue;

                    var go = CreateTileObject(wallPrefab, "Wall", cell);
                    go.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);
                    go.transform.position = CellCenterToWorld(cell, wallHeight * 0.5f);

                    ApplyMaterial(go, material);
                }
            }
        }

        /// <summary>
        /// Fills the one-cell ring outside <see cref="GeneratorSettings.gridSize"/> with walls so walkable cells
        /// on the map edge are still enclosed (interior walls only cover non-floor cells inside the grid).
        /// </summary>
        private void CreateBorderWallObjects()
        {
            int width = Mathf.Max(0, settings.gridSize.x);
            int height = Mathf.Max(0, settings.gridSize.y);
            Material material = ResolveMaterial(wallMaterial, fallbackWallColor, ref _generatedWallMaterial, "Generated_DungeonWallMaterial");

            for (int x = -1; x <= width; x++)
            {
                for (int y = -1; y <= height; y++)
                {
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        continue;
                    }

                    var cell = new Vector2Int(x, y);
                    var go = CreateTileObject(wallPrefab, "WallBorder", cell);
                    go.transform.localScale = new Vector3(cellSize, wallHeight, cellSize);
                    go.transform.position = CellCenterToWorld(cell, wallHeight * 0.5f);

                    ApplyMaterial(go, material);
                }
            }
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            if (target == null || material == null)
                return;

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
                renderer.sharedMaterial = material;
        }

        private static Material ResolveMaterial(Material assigned, Color fallbackColor, ref Material generated, string generatedName)
        {
            if (assigned != null && assigned.shader != null && assigned.shader.isSupported)
                return assigned;

            if (generated != null)
                return generated;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                return null;

            generated = new Material(shader)
            {
                name = generatedName
            };

            if (generated.HasProperty("_BaseColor"))
                generated.SetColor("_BaseColor", fallbackColor);
            if (generated.HasProperty("_Color"))
                generated.SetColor("_Color", fallbackColor);

            return generated;
        }

        private GameObject CreateTileObject(GameObject prefab, string fallbackName, Vector2Int cell)
        {
            GameObject instance;

            if (prefab != null)
            {
                instance = Instantiate(prefab, _generatedRoot);
                instance.name = fallbackName + "_" + cell.x + "_" + cell.y;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = fallbackName + "_" + cell.x + "_" + cell.y;
                instance.transform.SetParent(_generatedRoot, false);
            }

            return instance;
        }

        private GameObject CreateObject(GameObject prefab, string name)
        {
            GameObject instance;

            if (prefab != null)
            {
                instance = Instantiate(prefab, _generatedRoot);
                instance.name = name;
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = name;
                instance.transform.SetParent(_generatedRoot, false);
            }

            return instance;
        }

        private Vector3 GetNodeCenterWorld(DungeonNode node, float y)
        {
            return transform.position + worldOffset + new Vector3(
                (node.GridPosition.x + node.Size.x * 0.5f) * cellSize,
                y,
                (node.GridPosition.y + node.Size.y * 0.5f) * cellSize
            );
        }

        public Vector3 CellCenterToWorld(Vector2Int cell, float y)
        {
            return transform.position + worldOffset + new Vector3((cell.x + 0.5f) * cellSize, y, (cell.y + 0.5f) * cellSize);
        }

        private static bool TryGetStartCellFromGraph(DungeonGraph graph, out Vector2Int startCell)
        {
            startCell = default;

            DungeonNode fallback = null;
            foreach (var node in graph.Nodes)
            {
                if (fallback == null)
                    fallback = node;

                if (node.NodeType == DungeonNodeType.Start)
                {
                    startCell = GetNodeCenterCell(node);
                    return true;
                }
            }

            if (fallback != null)
            {
                startCell = GetNodeCenterCell(fallback);
                return true;
            }

            return false;
        }

        private void RestoreWalkableCache()
        {
            _walkableCells.Clear();

            for (int i = 0; i < _serializedWalkableCells.Count; i++)
                _walkableCells.Add(_serializedWalkableCells[i]);
        }

        [ContextMenu("Debug: Builder State")]
        public void DebugBuilderState()
        {
            Debug.Log("=== DungeonBasic3DBuilder State ===");
            Debug.Log($"Builder transform position: {transform.position}");
            Debug.Log($"Builder world offset: {worldOffset}");
            Debug.Log($"Cell size: {cellSize}");
            Debug.Log($"Walkable cells count: {_walkableCells.Count}");
            Debug.Log($"Has start cell: {_hasStartCell}");
            if (_hasStartCell)
                Debug.Log($"Start cell: {_lastStartCell}");

            if (_walkableCells.Count > 0 && _walkableCells.Count <= 30)
            {
                Debug.Log($"All walkable cells: {string.Join(", ", _walkableCells)}");
            }
            else if (_walkableCells.Count > 30)
            {
                Debug.Log($"First 15 walkable cells: {string.Join(", ", System.Linq.Enumerable.Take(_walkableCells, 15))}");
            }
            Debug.Log("=== End Builder State ===");
        }
    }
}
