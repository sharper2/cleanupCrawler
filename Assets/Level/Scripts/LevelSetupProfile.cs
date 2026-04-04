using System.Collections.Generic;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    [CreateAssetMenu(fileName = "LevelSetupProfile", menuName = "Level/Setup Profile")]
    public class LevelSetupProfile : ScriptableObject
    {
        [Header("Dungeon")]
        [SerializeField] private bool randomizeSeedEachBuild = true;

        [Header("Pickups")]
        [SerializeField] private int minPickupCount = 4;
        [SerializeField] private int maxPickupCount = 10;
        [SerializeField] private List<LevelLootEntry> lootPool = new();

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
