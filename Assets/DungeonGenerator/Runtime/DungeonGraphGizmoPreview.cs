using System.Collections.Generic;
using DungeonGenerator.Data;
using UnityEngine;

namespace DungeonGenerator
{
    [ExecuteAlways]
    public class DungeonGraphGizmoPreview : MonoBehaviour
    {
        [Header("Generation")]
        public GeneratorSettings settings = new GeneratorSettings();
        public bool autoRegenerateInEditor;

        [Header("Drawing")]
        public float cellSize = 1f;
        public Vector3 worldOffset = Vector3.zero;
        public bool drawGrid = true;
        public bool drawEdges = true;
        public bool drawNodeLabels = true;

        [Header("Colors")]
        public Color roomColor = new Color(0.65f, 0.65f, 0.65f, 1f);
        public Color startColor = new Color(0.2f, 0.85f, 0.2f, 1f);
        public Color exitColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        public Color edgeColor = new Color(0.5f, 0.8f, 1f, 1f);
        public Color gridColor = new Color(1f, 1f, 1f, 0.08f);

        [SerializeField] private DungeonGraph _previewGraph;

        public DungeonGraph PreviewGraph => _previewGraph;

        public void Regenerate()
        {
            if (settings == null)
                settings = new GeneratorSettings();

            _previewGraph = new RoomPlacementGenerator().Generate(settings);
        }

        private void OnEnable()
        {
            if (_previewGraph == null)
                Regenerate();
        }

        private void OnValidate()
        {
            if (autoRegenerateInEditor && !Application.isPlaying)
                Regenerate();
        }

        private void OnDrawGizmos()
        {
            if (_previewGraph == null)
                Regenerate();

            if (_previewGraph == null)
                return;

            if (drawGrid)
                DrawGrid();

            if (drawEdges)
                DrawEdges(_previewGraph);

            DrawNodes(_previewGraph);
        }

        private void DrawGrid()
        {
            if (settings == null)
                return;

            Gizmos.color = gridColor;

            int width = Mathf.Max(0, settings.gridSize.x);
            int height = Mathf.Max(0, settings.gridSize.y);

            for (int x = 0; x <= width; x++)
            {
                Vector3 from = GridToWorld(new Vector2Int(x, 0));
                Vector3 to = GridToWorld(new Vector2Int(x, height));
                Gizmos.DrawLine(from, to);
            }

            for (int y = 0; y <= height; y++)
            {
                Vector3 from = GridToWorld(new Vector2Int(0, y));
                Vector3 to = GridToWorld(new Vector2Int(width, y));
                Gizmos.DrawLine(from, to);
            }
        }

        private void DrawNodes(DungeonGraph graph)
        {
            foreach (var node in graph.Nodes)
            {
                Gizmos.color = GetNodeColor(node);

                Vector3 min = GridToWorld(node.GridPosition);
                Vector3 size = new Vector3(node.Size.x * cellSize, 0.05f, node.Size.y * cellSize);
                Vector3 center = min + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);

                Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
                if (drawNodeLabels)
                {
                    string label = $"{node.ID} ({node.NodeType}) d:{node.Depth}";
                    UnityEditor.Handles.Label(center + Vector3.up * 0.15f, label);
                }
#endif
            }
        }

        private void DrawEdges(DungeonGraph graph)
        {
            Gizmos.color = edgeColor;

            var drawnEdges = new HashSet<string>();

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

                    if (!drawnEdges.Add(key))
                        continue;

                    DungeonNode otherNode = graph.GetNodeByID(otherID);
                    if (otherNode == null)
                        continue;

                    Vector3 from = GetNodeCenterWorld(node);
                    Vector3 to = GetNodeCenterWorld(otherNode);
                    Gizmos.DrawLine(from, to);
                }
            }
        }

        private Color GetNodeColor(DungeonNode node)
        {
            switch (node.NodeType)
            {
                case DungeonNodeType.Start:
                    return startColor;
                case DungeonNodeType.Exit:
                    return exitColor;
                default:
                    return roomColor;
            }
        }

        private Vector3 GetNodeCenterWorld(DungeonNode node)
        {
            Vector3 basePos = GridToWorld(node.GridPosition);
            return basePos + new Vector3(node.Size.x * cellSize * 0.5f, 0f, node.Size.y * cellSize * 0.5f);
        }

        private Vector3 GridToWorld(Vector2Int grid)
        {
            return transform.position + worldOffset + new Vector3(grid.x * cellSize, 0f, grid.y * cellSize);
        }
    }
}
