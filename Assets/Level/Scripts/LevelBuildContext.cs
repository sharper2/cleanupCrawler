using System.Collections.Generic;
using DungeonGenerator;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    public class LevelBuildContext
    {
        public DungeonBasic3DBuilder DungeonBuilder { get; set; }
        public IReadOnlyList<Vector2Int> WalkableCells { get; set; }
        public Vector2Int StartCell { get; set; }
        public Vector2Int ExitCell { get; set; }
        public int SpawnedPickupCount { get; set; }
        public int SpawnedEnemyCount { get; set; }
        public int SpawnedDecorationCount { get; set; }
        public int GeneratedQuota { get; set; }
    }
}
