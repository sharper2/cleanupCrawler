using System.Collections.Generic;
using DungeonGenerator;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    public class LevelController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DungeonBasic3DBuilder dungeonBuilder;
        [SerializeField] private DungeonGridPlayerController playerController;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private InventoryHudController playerInventory;
        [SerializeField] private LevelSetupProfile setupProfile;

        [Header("Extensibility")]
        [SerializeField] private List<MonoBehaviour> decorationPlacers = new();

        [Header("Lifecycle")]
        [SerializeField] private bool buildOnStart = true;

        [Header("Exit Behavior")]
        [SerializeField] private bool requireQuotaForExit = true;
        [SerializeField] private bool preserveInventoryOnExit = true;
        [SerializeField] private bool preserveHealthOnExit = true;

        [Header("Spawn Source")]
        [SerializeField] private bool disableBuilderFloorItems = true;

        [Header("Safety")]
        [SerializeField, Min(1)] private int maxSpawnsPerCategory = 256;
        [SerializeField] private bool logBuildTimings;

        private readonly List<GameObject> _spawnedRuntimeObjects = new();
        private bool _isBuildingLevel;

        public LevelBuildContext LastBuildContext { get; private set; }

        private void Awake()
        {
            Debug.LogWarning($"[LevelController] Awake on '{name}' (enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy})");

            if (dungeonBuilder == null)
            {
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<DungeonGridPlayerController>();
            }

            if (playerTransform == null && playerController != null)
            {
                playerTransform = playerController.transform;
            }

            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<InventoryHudController>();
            }
        }

        private void Start()
        {
            if (logBuildTimings)
            {
                Debug.Log($"[LevelController] Start on '{name}'. enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}, buildOnStart={buildOnStart}");
            }

            if (buildOnStart)
            {
                BuildLevel();
                return;
            }

            if (logBuildTimings)
            {
                Debug.Log("[LevelController] buildOnStart is disabled. BuildLevel was not called automatically.");
            }
        }

        private void OnEnable()
        {
            Debug.LogWarning($"[LevelController] OnEnable on '{name}'.");
        }

        [ContextMenu("Build Level")]
        public void BuildLevel()
        {
            BuildLevel(true);
        }

        public bool TryUseExit(GameObject enteringObject)
        {
            if (_isBuildingLevel || !IsPlayerObject(enteringObject))
            {
                return false;
            }

            if (requireQuotaForExit && playerInventory != null && !playerInventory.CanCompleteLevel)
            {
                return false;
            }

            var health = ResolvePlayerHealthComponent();
            var healthBefore = health != null ? health.CurrentHealth : 0f;

            BuildLevel(!preserveInventoryOnExit);

            if (health != null)
            {
                health.SetCurrentHealth(preserveHealthOnExit ? healthBefore : health.MaxHealth);
            }

            return true;
        }

        private void BuildLevel(bool applyPlayerInventorySetup)
        {
            if (logBuildTimings)
            {
                Debug.Log("[LevelController] BuildLevel requested.");
            }

            if (dungeonBuilder == null || setupProfile == null)
            {
                if (logBuildTimings)
                {
                    Debug.LogWarning($"[LevelController] BuildLevel aborted. Missing references: dungeonBuilder={(dungeonBuilder != null)}, setupProfile={(setupProfile != null)}");
                }
                return;
            }

            if (_isBuildingLevel)
            {
                return;
            }

            _isBuildingLevel = true;

            var startTime = Time.realtimeSinceStartup;
            var stepTime = startTime;

            try
            {
                ClearSpawnedRuntimeObjects();
                LogStep("Cleared previous runtime objects", ref stepTime);

                RandomizeSeedIfNeeded();
                if (disableBuilderFloorItems)
                {
                    dungeonBuilder.spawnFloorItems = false;
                }
                dungeonBuilder.BuildDungeon();
                LogStep("Generated dungeon", ref stepTime);

                var walkableCells = CollectWalkableCells();
                if (walkableCells.Count == 0)
                {
                    return;
                }

                LogStep($"Collected {walkableCells.Count} walkable cells", ref stepTime);

                var startCell = ResolveStartCell(walkableCells);
                var exitCell = ResolveExitCell(walkableCells, startCell);

                PlacePlayerAtStart(startCell, walkableCells);
                if (applyPlayerInventorySetup)
                {
                    ApplyPlayerInventorySetup();
                }
                LogStep("Placed player and applied inventory", ref stepTime);

                var reservedCells = new HashSet<Vector2Int> { startCell, exitCell };

                int pickupCount = SpawnPickups(walkableCells, reservedCells);
                LogStep($"Spawned pickups: {pickupCount}", ref stepTime);

                int enemyCount = SpawnEnemies(walkableCells, reservedCells, startCell);
                LogStep($"Spawned enemies: {enemyCount}", ref stepTime);

                bool hasCustomDecorationPlacers = HasCustomDecorationPlacers();
                int decorationCount = hasCustomDecorationPlacers ? 0 : SpawnDecorations(walkableCells, reservedCells);
                LogStep($"Spawned decorations: {decorationCount}", ref stepTime);

                PlaceExit(exitCell);
                LogStep("Placed exit", ref stepTime);

                int pickupCountForQuota = CountPickupsInScene();
                int quota = setupProfile.BaseQuota + pickupCountForQuota * setupProfile.QuotaPerPickup;
                if (playerInventory != null)
                {
                    playerInventory.SetLevelQuota(quota);
                    playerInventory.ResetScore();
                }

                var context = new LevelBuildContext
                {
                    DungeonBuilder = dungeonBuilder,
                    WalkableCells = walkableCells,
                    StartCell = startCell,
                    ExitCell = exitCell,
                    SpawnedPickupCount = pickupCountForQuota,
                    SpawnedEnemyCount = enemyCount,
                    SpawnedDecorationCount = decorationCount,
                    GeneratedQuota = quota
                };

                LastBuildContext = context;
                ApplyDecorationPlacers(context);
                LogStep("Applied decoration placers", ref stepTime);

                if (logBuildTimings)
                {
                    var totalMs = (Time.realtimeSinceStartup - startTime) * 1000f;
                    Debug.Log($"[LevelController] BuildLevel complete in {totalMs:0.0} ms");
                }
            }
            finally
            {
                _isBuildingLevel = false;
            }
        }

        private void RandomizeSeedIfNeeded()
        {
            if (!setupProfile.RandomizeSeedEachBuild || dungeonBuilder.settings == null)
            {
                return;
            }

            dungeonBuilder.settings.seed = Random.Range(int.MinValue, int.MaxValue);
            dungeonBuilder.settings.useRandomSeed = false;
        }

        private List<Vector2Int> CollectWalkableCells()
        {
            var cells = new List<Vector2Int>();
            var size = dungeonBuilder.settings != null ? dungeonBuilder.settings.gridSize : Vector2Int.zero;

            for (var y = 0; y < size.y; y++)
            {
                for (var x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (dungeonBuilder.IsCellWalkable(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        private Vector2Int ResolveStartCell(IReadOnlyList<Vector2Int> walkableCells)
        {
            if (dungeonBuilder.TryGetStartCell(out var startCell) && dungeonBuilder.IsCellWalkable(startCell))
            {
                return startCell;
            }

            return walkableCells[0];
        }

        private Vector2Int ResolveExitCell(IReadOnlyList<Vector2Int> walkableCells, Vector2Int startCell)
        {
            var bestCell = startCell;
            var bestDistance = -1f;

            for (var i = 0; i < walkableCells.Count; i++)
            {
                var candidate = walkableCells[i];
                var distance = (candidate - startCell).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }

            return bestCell;
        }

        private void PlacePlayerAtStart(Vector2Int startCell, IReadOnlyList<Vector2Int> walkableCells)
        {
            if (playerController != null)
            {
                playerController.dungeonBuilder = dungeonBuilder;

                if (playerController.TryTeleportToCell(startCell))
                {
                    return;
                }

                for (var i = 0; i < walkableCells.Count; i++)
                {
                    var c = walkableCells[i];
                    if (playerController.TryTeleportToCell(c))
                    {
                        return;
                    }
                }

                if (walkableCells.Count > 0)
                {
                    playerController.ForceSetGridCell(walkableCells[0]);
                }

                return;
            }

            if (playerTransform == null)
            {
                return;
            }

            var placeCell = startCell;
            if (walkableCells != null && walkableCells.Count > 0 && !dungeonBuilder.IsCellWalkable(placeCell))
            {
                placeCell = walkableCells[0];
            }

            playerTransform.position = dungeonBuilder.CellCenterToWorld(placeCell, 0.5f);
        }

        private void ApplyPlayerInventorySetup()
        {
            if (playerInventory == null)
            {
                return;
            }

            if (setupProfile.ClearInventoryOnBuild)
            {
                playerInventory.ClearAllItems();
            }

            var items = setupProfile.StartingInventoryItems;
            for (var i = 0; i < items.Count; i++)
            {
                playerInventory.TryAddItem(items[i]);
            }
        }

        private int SpawnPickups(IReadOnlyList<Vector2Int> walkableCells, HashSet<Vector2Int> reservedCells)
        {
            var pool = setupProfile.LootPool;
            if (pool == null || pool.Count == 0)
            {
                return 0;
            }

            var availableCells = BuildAvailableCells(walkableCells, reservedCells);
            if (availableCells.Count == 0)
            {
                return 0;
            }

            var targetCount = Random.Range(setupProfile.MinPickupCount, setupProfile.MaxPickupCount + 1);
            var spawnCount = Mathf.Min(targetCount, availableCells.Count, Mathf.Max(1, maxSpawnsPerCategory));

            var spawned = 0;
            for (var i = 0; i < spawnCount; i++)
            {
                var cell = TakeRandomCell(availableCells);
                var entry = SelectWeightedEntry(pool, e => e != null && e.HasValidLoot(), e => e.Weight);
                if (entry == null)
                {
                    continue;
                }

                var pickupObject = new GameObject($"Pickup_{entry.ResolvePickupDisplayName()}");
                pickupObject.transform.SetParent(transform, false);

                if (entry.Kind == LevelLootKind.InventoryItem)
                {
                    var pickup = pickupObject.AddComponent<FloorInventoryItemPickup>();
                    if (!pickup.Configure(dungeonBuilder, entry.Item, cell))
                    {
                        Destroy(pickupObject);
                        continue;
                    }
                }
                else if (entry.Kind == LevelLootKind.AbilityOrb)
                {
                    var pickup = pickupObject.AddComponent<FloorAbilityQueuePickup>();
                    if (!pickup.Configure(dungeonBuilder, entry.AbilityOrb, cell))
                    {
                        Destroy(pickupObject);
                        continue;
                    }
                }
                else
                {
                    Destroy(pickupObject);
                    continue;
                }

                _spawnedRuntimeObjects.Add(pickupObject);
                reservedCells.Add(cell);
                spawned++;
            }

            return spawned;
        }

        private int SpawnEnemies(IReadOnlyList<Vector2Int> walkableCells, HashSet<Vector2Int> reservedCells, Vector2Int startCell)
        {
            var pool = setupProfile.EnemyPool;
            if (pool == null || pool.Count == 0)
            {
                return 0;
            }

            var availableCells = BuildAvailableCells(walkableCells, reservedCells);
            availableCells.RemoveAll(cell => ManhattanDistance(cell, startCell) <= setupProfile.StartSafeRadiusCells);

            if (availableCells.Count == 0)
            {
                return 0;
            }

            var targetCount = Random.Range(setupProfile.MinEnemyCount, setupProfile.MaxEnemyCount + 1);
            var spawnCount = Mathf.Min(targetCount, availableCells.Count, Mathf.Max(1, maxSpawnsPerCategory));

            var spawned = 0;
            for (var i = 0; i < spawnCount; i++)
            {
                var cell = TakeRandomCell(availableCells);
                var entry = SelectWeightedEntry(pool, e => e != null && e.EnemyPrefab != null, e => e.Weight);
                if (entry == null)
                {
                    continue;
                }

                var enemy = Instantiate(entry.EnemyPrefab, transform);
                var staticEnemy = enemy.GetComponent<StaticEnemy>();
                if (staticEnemy != null)
                {
                    staticEnemy.TrySetOccupiedCell(cell);
                }
                else
                {
                    enemy.transform.position = dungeonBuilder.CellCenterToWorld(cell, 0.5f);
                }

                var stateController = enemy.GetComponent<EnemyStateController>();
                if (stateController != null && entry.Weapon != null)
                {
                    stateController.SetAttackType(entry.Weapon);
                }

                _spawnedRuntimeObjects.Add(enemy);
                reservedCells.Add(cell);
                spawned++;
            }

            return spawned;
        }

        private int SpawnDecorations(IReadOnlyList<Vector2Int> walkableCells, HashSet<Vector2Int> reservedCells)
        {
            var pool = setupProfile.DecorationPool;
            if (pool == null || pool.Count == 0)
            {
                return 0;
            }

            var availableCells = BuildAvailableCells(walkableCells, reservedCells);
            if (availableCells.Count == 0)
            {
                return 0;
            }

            var targetCount = Random.Range(setupProfile.MinDecorationCount, setupProfile.MaxDecorationCount + 1);
            var spawnCount = Mathf.Min(targetCount, availableCells.Count, Mathf.Max(1, maxSpawnsPerCategory));

            var spawned = 0;
            for (var i = 0; i < spawnCount; i++)
            {
                var cell = TakeRandomCell(availableCells);
                var entry = SelectWeightedEntry(pool, e => e != null && e.Prefab != null, e => e.Weight);
                if (entry == null)
                {
                    continue;
                }

                var basePosition = dungeonBuilder.CellCenterToWorld(cell, setupProfile.DecorationYOffset);
                var prefabPositionOffset = entry.ApplyPrefabTransformOffsets ? entry.Prefab.transform.localPosition : Vector3.zero;
                var prefabRotationOffset = entry.ApplyPrefabTransformOffsets ? entry.Prefab.transform.localRotation : Quaternion.identity;

                var finalRotation = prefabRotationOffset * Quaternion.Euler(entry.RotationOffsetEuler);
                var position = basePosition + prefabRotationOffset * prefabPositionOffset + finalRotation * entry.PositionOffset;

                var instance = Instantiate(entry.Prefab, position, finalRotation, transform);

                _spawnedRuntimeObjects.Add(instance);
                reservedCells.Add(cell);
                spawned++;
            }

            return spawned;
        }

        private void PlaceExit(Vector2Int exitCell)
        {
            if (setupProfile.ExitPrefab == null)
            {
                return;
            }

            var exit = Instantiate(setupProfile.ExitPrefab, transform);
            exit.transform.position = dungeonBuilder.CellCenterToWorld(exitCell, setupProfile.ExitYOffset);

            var exitCollider = exit.GetComponent<Collider>();
            if (exitCollider == null)
            {
                exitCollider = exit.AddComponent<BoxCollider>();
            }

            exitCollider.isTrigger = true;

            var exitBody = exit.GetComponent<Rigidbody>();
            if (exitBody == null)
            {
                exitBody = exit.AddComponent<Rigidbody>();
            }

            exitBody.isKinematic = true;
            exitBody.useGravity = false;

            var exitTrigger = exit.GetComponent<LevelExitTrigger>();
            if (exitTrigger == null)
            {
                exitTrigger = exit.AddComponent<LevelExitTrigger>();
            }

            exitTrigger.Initialize(this);
            _spawnedRuntimeObjects.Add(exit);
        }

        private bool IsPlayerObject(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            var candidateTransform = candidate.transform;
            if (playerTransform != null && (candidateTransform == playerTransform || candidateTransform.IsChildOf(playerTransform) || playerTransform.IsChildOf(candidateTransform)))
            {
                return true;
            }

            if (playerController == null)
            {
                return false;
            }

            var controllerTransform = playerController.transform;
            return candidateTransform == controllerTransform || candidateTransform.IsChildOf(controllerTransform) || controllerTransform.IsChildOf(candidateTransform);
        }

        private HealthComponent ResolvePlayerHealthComponent()
        {
            if (playerController != null)
            {
                var health = playerController.GetComponent<HealthComponent>()
                             ?? playerController.GetComponentInChildren<HealthComponent>(true)
                             ?? playerController.GetComponentInParent<HealthComponent>();
                if (health != null)
                {
                    return health;
                }
            }

            if (playerTransform == null)
            {
                return null;
            }

            return playerTransform.GetComponent<HealthComponent>()
                ?? playerTransform.GetComponentInChildren<HealthComponent>(true)
                ?? playerTransform.GetComponentInParent<HealthComponent>();
        }

        private void ApplyDecorationPlacers(LevelBuildContext context)
        {
            for (var i = 0; i < decorationPlacers.Count; i++)
            {
                if (decorationPlacers[i] is ILevelDecorationPlacer placer)
                {
                    placer.ApplyDecorations(context);
                }
            }
        }

        private bool HasCustomDecorationPlacers()
        {
            for (var i = 0; i < decorationPlacers.Count; i++)
            {
                if (decorationPlacers[i] is ILevelDecorationPlacer)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearSpawnedRuntimeObjects()
        {
            for (var i = 0; i < _spawnedRuntimeObjects.Count; i++)
            {
                if (_spawnedRuntimeObjects[i] != null)
                {
                    Destroy(_spawnedRuntimeObjects[i]);
                }
            }

            _spawnedRuntimeObjects.Clear();
        }

        private static List<Vector2Int> BuildAvailableCells(IReadOnlyList<Vector2Int> walkableCells, HashSet<Vector2Int> reservedCells)
        {
            var result = new List<Vector2Int>(walkableCells.Count);
            for (var i = 0; i < walkableCells.Count; i++)
            {
                var cell = walkableCells[i];
                if (!reservedCells.Contains(cell) && !GridCellOccupantRegistry.IsCellOccupied(cell) && !FloorItemRegistry.TryGetItemAtCell(cell, out _))
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        private static Vector2Int TakeRandomCell(List<Vector2Int> cells)
        {
            var index = Random.Range(0, cells.Count);
            var cell = cells[index];
            cells[index] = cells[cells.Count - 1];
            cells.RemoveAt(cells.Count - 1);
            return cell;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        private static int CountPickupsInScene()
        {
            var inventory = FindObjectsByType<FloorInventoryItemPickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            var orbs = FindObjectsByType<FloorAbilityQueuePickup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            return inventory + orbs;
        }

        private static T SelectWeightedEntry<T>(IReadOnlyList<T> entries, System.Func<T, bool> isValid, System.Func<T, int> getWeight)
        {
            var totalWeight = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (isValid(entry))
                {
                    totalWeight += Mathf.Max(1, getWeight(entry));
                }
            }

            if (totalWeight <= 0)
            {
                return default;
            }

            var roll = Random.Range(0, totalWeight);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!isValid(entry))
                {
                    continue;
                }

                roll -= Mathf.Max(1, getWeight(entry));
                if (roll < 0)
                {
                    return entry;
                }
            }

            return default;
        }

        private void LogStep(string label, ref float stepTime)
        {
            if (!logBuildTimings)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var stepMs = (now - stepTime) * 1000f;
            stepTime = now;
            Debug.Log($"[LevelController] {label} ({stepMs:0.0} ms)");
        }
    }
}
