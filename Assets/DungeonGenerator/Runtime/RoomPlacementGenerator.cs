using UnityEngine;
using System.Collections.Generic;
using DungeonGenerator.Data;

namespace DungeonGenerator
{
    public class RoomPlacementGenerator
    {
        private GeneratorSettings _settings;
        private System.Random _rng;

        public DungeonGraph Generate(GeneratorSettings settings)
        {
            _settings = settings;

            int seed = settings.useRandomSeed ? UnityEngine.Random.Range(0, int.MaxValue) : settings.seed;
            _rng = new System.Random(seed);

            NodeIDFactory.Reset();

            var graph = new DungeonGraph();

            PlaceRooms(graph);
            ConnectRooms(graph);
            AddLoops(graph);
            AssignDepths(graph);
            AssignSpecialNodes(graph);

            return graph;
        }

        private void PlaceRooms(DungeonGraph graph)
        {
            if (_settings.gridSize.x <= 0 || _settings.gridSize.y <= 0)
                return;

            int minRooms = Mathf.Max(1, Mathf.Min(_settings.minRooms, _settings.maxRooms));
            int maxRooms = Mathf.Max(minRooms, _settings.maxRooms);
            int targetRooms = _rng.Next(minRooms, maxRooms + 1);
            int maxAttempts = targetRooms * 10;
            int attempts = 0;

            int minRoomWidth = Mathf.Clamp(_settings.minRoomSize.x, 1, _settings.gridSize.x);
            int maxRoomWidth = Mathf.Clamp(_settings.maxRoomSize.x, minRoomWidth, _settings.gridSize.x);
            int minRoomHeight = Mathf.Clamp(_settings.minRoomSize.y, 1, _settings.gridSize.y);
            int maxRoomHeight = Mathf.Clamp(_settings.maxRoomSize.y, minRoomHeight, _settings.gridSize.y);

            while (graph.NodeCount < targetRooms && attempts < maxAttempts)
            {
                attempts++;

                Vector2Int size = new Vector2Int(
                    _rng.Next(minRoomWidth, maxRoomWidth + 1),
                    _rng.Next(minRoomHeight, maxRoomHeight + 1)
                );

                Vector2Int position = new Vector2Int(
                    _rng.Next(0, _settings.gridSize.x - size.x + 1),
                    _rng.Next(0, _settings.gridSize.y - size.y + 1)
                );

                if (!CanPlaceRoom(graph, position, size))
                    continue;

                var node = new DungeonNode(NodeIDFactory.Next(), position)
                {
                    NodeType = DungeonNodeType.Room,
                    Size = size
                };

                graph.AddNode(node);
                ReserveGridCells(graph, node);
            }
        }

        private bool CanPlaceRoom(DungeonGraph graph, Vector2Int position, Vector2Int size)
        {
            for (int x = position.x - 1; x < position.x + size.x + 1; x++)
            {
                for (int y = position.y - 1; y < position.y + size.y + 1; y++)
                {
                    if (graph.IsPositionOccupied(new Vector2Int(x, y)))
                        return false;
                }
            }

            return true;
        }

        private void ReserveGridCells(DungeonGraph graph, DungeonNode node)
        {
            for (int x = node.GridPosition.x; x < node.GridPosition.x + node.Size.x; x++)
            {
                for (int y = node.GridPosition.y; y < node.GridPosition.y + node.Size.y; y++)
                {
                    var cellPos = new Vector2Int(x, y);

                    if (cellPos != node.GridPosition)
                        graph.ReservePosition(cellPos, node.ID);
                }
            }
        }

        private void ConnectRooms(DungeonGraph graph)
        {
            var nodes = new List<DungeonNode>(graph.Nodes);
            if (nodes.Count < 2)
                return;

            var connected = new HashSet<string>();
            var unconnected = new HashSet<string>();

            foreach (var node in nodes)
                unconnected.Add(node.ID);

            string startID = nodes[0].ID;
            connected.Add(startID);
            unconnected.Remove(startID);

            while (unconnected.Count > 0)
            {
                string bestFrom = null;
                string bestTo = null;
                float bestDist = float.MaxValue;

                foreach (var fromID in connected)
                {
                    foreach (var toID in unconnected)
                    {
                        float dist = Vector2Int.Distance(graph.GetNodeByID(fromID).GridPosition, graph.GetNodeByID(toID).GridPosition);

                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestFrom = fromID;
                            bestTo = toID;
                        }
                    }
                }

                AddEdgeBetween(graph, bestFrom, bestTo);
                connected.Add(bestTo);
                unconnected.Remove(bestTo);
            }
        }

        private void AddLoops(DungeonGraph graph)
        {
            var nodes = new List<DungeonNode>(graph.Nodes);

            foreach (var node in nodes)
            {
                if (_rng.NextDouble() > _settings.loopProbability)
                    continue;

                DungeonNode nearest = null;
                float nearestDist = float.MaxValue;

                foreach (var other in nodes)
                {
                    if (other.ID == node.ID) continue;
                    if (AlreadyConnected(graph, node.ID, other.ID)) continue;

                    float dist = Vector2Int.Distance(node.GridPosition, other.GridPosition);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = other;
                    }
                }

                if (nearest != null)
                    AddEdgeBetween(graph, node.ID, nearest.ID);
            }
        }

        private void AssignDepths(DungeonGraph graph)
        {
            if (graph.NodeCount == 0) return;

            var nodes = new List<DungeonNode>(graph.Nodes);
            string startID = nodes[0].ID;

            var visited = new HashSet<string>();
            var queue = new Queue<(string id, int depth)>();

            queue.Enqueue((startID, 0));
            visited.Add(startID);

            while (queue.Count > 0)
            {
                var (currentID, depth) = queue.Dequeue();
                var currentNode = graph.GetNodeByID(currentID);
                currentNode.Depth = depth;
                foreach (var edge in graph.GetEdgesForNode(currentID))
                {
                    string neighborID = edge.GetOtherNodeID(currentID);

                    if (!visited.Contains(neighborID))
                    {
                        visited.Add(neighborID);
                        queue.Enqueue((neighborID, depth + 1));
                    }
                }
            }
        }

        private void AssignSpecialNodes(DungeonGraph graph)
        {
            DungeonNode startNode = null;
            DungeonNode exitNode = null;
            int maxDepth = -1;

            foreach (var node in graph.Nodes)
            {
                if (startNode == null || node.Depth < startNode.Depth)
                    startNode = node;

                if (node.Depth > maxDepth)
                {
                    maxDepth = node.Depth;
                    exitNode = node;
                }
            }

            if (startNode != null)
            {
                startNode.NodeType = DungeonNodeType.Start;
                graph.StartNodeID = startNode.ID;
            }

            if (exitNode != null && exitNode != startNode)
            {
                exitNode.NodeType = DungeonNodeType.Exit;
                graph.ExitNodeID = exitNode.ID;
            }
        }

        private void AddEdgeBetween(DungeonGraph graph, string fromID, string toID)
        {
            var fromNode = graph.GetNodeByID(fromID);
            var toNode = graph.GetNodeByID(toID);

            Vector2Int delta = toNode.GridPosition - fromNode.GridPosition;
            Vector2Int direction = new Vector2Int(
                Mathf.Clamp(delta.x, -1, 1),
                Mathf.Clamp(delta.y, -1, 1)
            );

            graph.AddEdge(new DungeonEdge(fromID, toID, direction));
        }

        private bool AlreadyConnected(DungeonGraph graph, string idA, string idB)
        {
            foreach (var edge in graph.GetEdgesForNode(idA))
            {
                if (edge.GetOtherNodeID(idA) == idB)
                    return true;
            }

            return false;
        }
    }
}