using DungeonGenerator;
using DungeonGenerator.Data;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator.Tests
{
    public class DungeonGraphTests
    {
        private static Dictionary<string, int> ComputeShortestDepths(DungeonGraph graph, string startID)
        {
            var depths = new Dictionary<string, int>();
            var queue = new Queue<string>();

            depths[startID] = 0;
            queue.Enqueue(startID);

            while (queue.Count > 0)
            {
                string currentID = queue.Dequeue();
                int nextDepth = depths[currentID] + 1;

                foreach (var edge in graph.GetEdgesForNode(currentID))
                {
                    string neighborID = edge.GetOtherNodeID(currentID);
                    if (neighborID == null || depths.ContainsKey(neighborID))
                        continue;

                    depths[neighborID] = nextDepth;
                    queue.Enqueue(neighborID);
                }
            }

            return depths;
        }

        private static int GetUndirectedEdgeCount(DungeonGraph graph)
        {
            var uniqueEdges = new HashSet<string>();

            foreach (var node in graph.Nodes)
            {
                foreach (var edge in graph.GetEdgesForNode(node.ID))
                {
                    string otherId = edge.GetOtherNodeID(node.ID);
                    if (otherId == null)
                        continue;

                    string a = string.CompareOrdinal(node.ID, otherId) <= 0 ? node.ID : otherId;
                    string b = string.CompareOrdinal(node.ID, otherId) <= 0 ? otherId : node.ID;
                    uniqueEdges.Add(a + "|" + b);
                }
            }

            return uniqueEdges.Count;
        }

        private static IEnumerable<Vector2Int> GetOccupiedCells(DungeonNode node)
        {
            for (int x = node.GridPosition.x; x < node.GridPosition.x + node.Size.x; x++)
            {
                for (int y = node.GridPosition.y; y < node.GridPosition.y + node.Size.y; y++)
                    yield return new Vector2Int(x, y);
            }
        }

        private GeneratorSettings MakeSettings(int seed = 42)
        {
            return new GeneratorSettings
            {
                useRandomSeed = false,
                seed = seed,
                gridSize = new Vector2Int(20, 20),
                minRooms = 5,
                maxRooms = 10,
                loopProbability = 0.15f
            };
        }

        [Test]
        public void Generate_StartAndExitAssigned()
        {
            var graph = new RoomPlacementGenerator().Generate(MakeSettings());
            Assert.IsNotNull(graph.StartNodeID);
            Assert.IsNotNull(graph.ExitNodeID);
        }

        [Test]
        public void Generate_GraphIsConnected()
        {
            var graph = new RoomPlacementGenerator().Generate(MakeSettings());
            Assert.IsTrue(GetUndirectedEdgeCount(graph) >= graph.NodeCount - 1);
        }

        [Test]
        public void Generate_SameSeedProducesSameGraph()
        {
            var g1 = new RoomPlacementGenerator().Generate(MakeSettings(42));
            var g2 = new RoomPlacementGenerator().Generate(MakeSettings(42));
            Assert.AreEqual(g1.NodeCount, g2.NodeCount);
            Assert.AreEqual(GetUndirectedEdgeCount(g1), GetUndirectedEdgeCount(g2));
        }

        [Test]
        public void Generate_NoOverlappingRooms()
        {
            var graph = new RoomPlacementGenerator().Generate(MakeSettings());
            var occupied = new HashSet<Vector2Int>();

            foreach (var node in graph.Nodes)
            {
                foreach (var cell in GetOccupiedCells(node))
                {
                    Assert.IsTrue(occupied.Add(cell), $"Overlapping room cell found at {cell}");
                }
            }
        }

        [Test]
        public void Generate_AllNodesReachableAndDepthsMatchShortestPath()
        {
            var graph = new RoomPlacementGenerator().Generate(MakeSettings());
            Assert.IsNotNull(graph.StartNodeID);

            var shortestDepths = ComputeShortestDepths(graph, graph.StartNodeID);

            foreach (var node in graph.Nodes)
            {
                Assert.IsTrue(shortestDepths.ContainsKey(node.ID), $"Node {node.ID} is not reachable from start");
                Assert.AreEqual(shortestDepths[node.ID], node.Depth, $"Depth mismatch for node {node.ID}");
            }
        }

        [Test]
        public void Generate_MultipleSeeds_AlwaysProducesValidGraph()
        {
            var generator = new RoomPlacementGenerator();

            for (int seed = 1; seed <= 100; seed++)
            {
                var graph = generator.Generate(MakeSettings(seed));

                Assert.Greater(graph.NodeCount, 0, $"No rooms generated for seed {seed}");
                Assert.LessOrEqual(graph.NodeCount, MakeSettings(seed).maxRooms, $"Too many rooms for seed {seed}");
                Assert.IsNotNull(graph.StartNodeID, $"Start node missing for seed {seed}");

                var shortestDepths = ComputeShortestDepths(graph, graph.StartNodeID);
                Assert.AreEqual(graph.NodeCount, shortestDepths.Count, $"Graph is disconnected for seed {seed}");
            }
        }
    }
}