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
        public float connectionWidth = 0.35f;
        public Vector3 worldOffset = Vector3.zero;

        [Header("Prefabs / Materials")]
        public GameObject floorPrefab;
        public GameObject wallPrefab;
        public Material floorMaterial;
        public Material wallMaterial;

        [Header("Fallback Colors")]
        public Color fallbackFloorColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        public Color fallbackCorridorColor = new Color(0.5f, 0.7f, 0.55f, 1f);
        public Color fallbackWallColor = new Color(0.12f, 0.12f, 0.14f, 1f);
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
            CreateWallObjects(floorCells);
        }

        public void ClearDungeon()
        {
            _walkableCells.Clear();
            _serializedWalkableCells.Clear();
            _hasStartCell = false;

            if (_generatedRoot == null)
                return;

            while (_generatedRoot.childCount > 0)
            {
                var child = _generatedRoot.GetChild(0).gameObject;

                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
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

                    AddCorridorCellsLShaped(floorCells, from, to);
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

        private static void AddCorridorCellsLShaped(HashSet<Vector2Int> cells, Vector2Int from, Vector2Int to)
        {
            int stepX = from.x <= to.x ? 1 : -1;
            int stepY = from.y <= to.y ? 1 : -1;

            for (int x = from.x; x != to.x; x += stepX)
                cells.Add(new Vector2Int(x, from.y));
            cells.Add(new Vector2Int(to.x, from.y));

            for (int y = from.y; y != to.y; y += stepY)
                cells.Add(new Vector2Int(to.x, y));
            cells.Add(to);
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
