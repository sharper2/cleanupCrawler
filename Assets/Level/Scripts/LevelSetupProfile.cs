using System.Collections.Generic;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    [CreateAssetMenu(fileName = "LevelSetupProfile", menuName = "Level/Setup Profile")]
    public class LevelSetupProfile : ScriptableObject
    {
        [Header("Dungeon")]
        [SerializeField] private bool randomizeSeedEachBuild = true;

        [Header("Dungeon layout")]
        [SerializeField] private Vector2Int dungeonGridSize = new Vector2Int(20, 20);
        [SerializeField, Min(1)] private int dungeonMinRooms = 5;
        [SerializeField, Min(1)] private int dungeonMaxRooms = 10;
        [SerializeField] private Vector2Int dungeonMinRoomSize = Vector2Int.one;
        [SerializeField] private Vector2Int dungeonMaxRoomSize = new Vector2Int(3, 3);
        [SerializeField, Range(0f, 1f)] private float dungeonLoopProbability = 0.15f;

        [Header("Dungeon look")]
        [SerializeField, Tooltip("If off, the dungeon builder keeps its scene fallback colors when this level builds. If the builder uses assigned floor/wall materials (not fallbacks), colors here may not show.")]
        private bool applyDungeonColorsFromProfile = true;
        [SerializeField] private Color dungeonFloorColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        [SerializeField] private Color dungeonCorridorColor = new Color(0.5f, 0.7f, 0.55f, 1f);
        [SerializeField] private Color dungeonWallColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        [SerializeField] private Color dungeonCeilingColor = new Color(0.2f, 0.2f, 0.24f, 1f);

        [Header("Pickups")]
        [SerializeField] private int minPickupCount = 4;
        [SerializeField] private int maxPickupCount = 10;
        [SerializeField, Tooltip("Weighted spawns. Each entry can be an inventory pickup or an ability-queue orb (see Kind).")]
        private List<LevelLootEntry> lootPool = new();

        [Header("Enemies")]
        [SerializeField] private int minEnemyCount = 2;
        [SerializeField] private int maxEnemyCount = 6;
        [SerializeField, Min(0)] private int startSafeRadiusCells = 2;
        [SerializeField] private List<LevelEnemyEntry> enemyPool = new();

        [Header("Decorations")]
        [SerializeField] private int minDecorationCount = 0;
        [SerializeField] private int maxDecorationCount = 10;
        [SerializeField] private float decorationYOffset = 0.5f;
        [SerializeField] private List<LevelDecorationEntry> decorationPool = new();

        [Header("Player")]
        [SerializeField] private bool clearInventoryOnBuild = false;
        [SerializeField] private List<InventoryItemDefinition> startingInventoryItems = new();

        [Header("Exit")]
        [SerializeField] private GameObject exitPrefab;
        [SerializeField] private float exitYOffset = 0.5f;

        [Header("Quota")]
        [SerializeField, Min(0)] private int baseQuota = 0;
        [SerializeField, Min(0)] private int quotaPerPickup = 10;

        public bool RandomizeSeedEachBuild => randomizeSeedEachBuild;

        public Vector2Int DungeonGridSize => new Vector2Int(
            Mathf.Max(1, dungeonGridSize.x),
            Mathf.Max(1, dungeonGridSize.y));

        public int DungeonMinRooms => Mathf.Max(1, dungeonMinRooms);

        public int DungeonMaxRooms => Mathf.Max(DungeonMinRooms, dungeonMaxRooms);

        public Vector2Int DungeonMinRoomSize => new Vector2Int(
            Mathf.Max(1, dungeonMinRoomSize.x),
            Mathf.Max(1, dungeonMinRoomSize.y));

        public Vector2Int DungeonMaxRoomSize
        {
            get
            {
                var min = DungeonMinRoomSize;
                var max = new Vector2Int(
                    Mathf.Max(min.x, dungeonMaxRoomSize.x),
                    Mathf.Max(min.y, dungeonMaxRoomSize.y));
                return max;
            }
        }

        public float DungeonLoopProbability => Mathf.Clamp01(dungeonLoopProbability);

        public bool ApplyDungeonColorsFromProfile => applyDungeonColorsFromProfile;

        public Color DungeonFloorColor => dungeonFloorColor;
        public Color DungeonCorridorColor => dungeonCorridorColor;
        public Color DungeonWallColor => dungeonWallColor;
        public Color DungeonCeilingColor => dungeonCeilingColor;

        public int MinPickupCount => Mathf.Max(0, minPickupCount);
        public int MaxPickupCount => Mathf.Max(MinPickupCount, maxPickupCount);
        public IReadOnlyList<LevelLootEntry> LootPool => lootPool;

        public int MinEnemyCount => Mathf.Max(0, minEnemyCount);
        public int MaxEnemyCount => Mathf.Max(MinEnemyCount, maxEnemyCount);
        public int StartSafeRadiusCells => Mathf.Max(0, startSafeRadiusCells);
        public IReadOnlyList<LevelEnemyEntry> EnemyPool => enemyPool;

        public int MinDecorationCount => Mathf.Max(0, minDecorationCount);
        public int MaxDecorationCount => Mathf.Max(MinDecorationCount, maxDecorationCount);
        public float DecorationYOffset => decorationYOffset;
        public IReadOnlyList<LevelDecorationEntry> DecorationPool => decorationPool;

        public bool ClearInventoryOnBuild => clearInventoryOnBuild;
        public IReadOnlyList<InventoryItemDefinition> StartingInventoryItems => startingInventoryItems;

        public GameObject ExitPrefab => exitPrefab;
        public float ExitYOffset => exitYOffset;

        public int BaseQuota => Mathf.Max(0, baseQuota);
        public int QuotaPerPickup => Mathf.Max(0, quotaPerPickup);
    }
}
