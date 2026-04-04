using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    [RequireComponent(typeof(StaticEnemy))]
    [RequireComponent(typeof(CombatAttackController))]
    public class EnemyStateController : MonoBehaviour
    {
        public enum EnemyState
        {
            Roaming,
            CloseChasing,
            CloseAttacking,
            RangedChasing,
            RangedAttacking
        }

        [Header("Roaming")]
        [SerializeField] private float roamingMoveInterval = 0.5f;
        [SerializeField] private float roamingMoveDuration = 0.2f;
        [SerializeField] private int wallBufferCells = 1;
        [SerializeField] private int minStraightSteps = 3;
        [SerializeField] private int maxStraightSteps = 5;

        [Header("Chasing")]
        [SerializeField] private float chasingMoveInterval = 0.25f;
        [SerializeField] private float chasingMoveDuration = 0.12f;

        [Header("Threat Detection")]
        [SerializeField] private float threatScanRadius = 10f;
        [SerializeField] private float loseThreatDistance = 20f;
        [SerializeField] private float threatScanInterval = 0.1f;
        [SerializeField] private LayerMask threatLayerMask = ~0;

        [Header("Combat")]
        [SerializeField] private WeaponItemDefinition attackType;
        [SerializeField] private float closeAttackRange = 2f;

        private readonly Collider[] _threatBuffer = new Collider[32];
        private readonly RaycastHit[] _lineOfSightBuffer = new RaycastHit[16];
        private readonly Dictionary<Collider, IThreat> _threatByColliderCache = new Dictionary<Collider, IThreat>();
        private readonly HashSet<Transform> _threatScanVisited = new HashSet<Transform>();
        private readonly HashSet<Vector2Int> _roamRegionCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _roamCells = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _adjacentCandidates = new List<Vector2Int>(4);

        private StaticEnemy _enemy;
        private CombatAttackController _attackController;
        private EnemyState _state = EnemyState.Roaming;
        private float _nextMoveAt;
        private bool _isMoving;
        private Coroutine _moveRoutine;
        private bool _moveKeepSnappedBefore;
        private Vector2Int _currentDirection;
        private int _remainingStraightSteps;
        private Vector2Int _homeCell;
        private bool _hasHomeCell;
        private int _stuckMoveAttempts;
        private float _nextThreatScanAt;

        private const int MaxStuckMoveAttempts = 3;
        private const int MaxPathSearchNodes = 512;

        public EnemyState State => _state;
        public IThreat SpottedThreat { get; private set; }

        public void SetAttackType(WeaponItemDefinition weapon)
        {
            attackType = weapon;
        }

        private void Awake()
        {
            _enemy = GetComponent<StaticEnemy>();
            _attackController = GetComponent<CombatAttackController>();
        }

        private void Start()
        {
            RebuildRoamArea();
            if (_enemy != null && _enemy.TryGetOccupiedCell(out var spawnCell))
            {
                _homeCell = spawnCell;
                _hasHomeCell = true;
            }
            _nextMoveAt = Time.time + Mathf.Max(0.05f, roamingMoveInterval);
        }

        private void Update()
        {
            if (SpottedThreat != null && ShouldLoseThreat(SpottedThreat))
            {
                ReturnToRoaming();
            }

            if (_state == EnemyState.Roaming)
            {
                UpdateRoaming();
                return;
            }

            if (_state == EnemyState.CloseChasing)
            {
                UpdateCloseChasing();
                return;
            }

            if (_state == EnemyState.CloseAttacking)
            {
                UpdateCloseAttacking();
                return;
            }

            if (_state == EnemyState.RangedChasing)
            {
                UpdateRangedChasing();
                return;
            }

            UpdateRangedAttacking();
        }

        private void UpdateRoaming()
        {
            if (TrySpotThreat(out var threat))
            {
                SpottedThreat = threat;
                _stuckMoveAttempts = 0;
                _state = attackType is RangedWeaponItemDefinition ? EnemyState.RangedChasing : EnemyState.CloseChasing;
                _nextMoveAt = Time.time + Mathf.Max(0.05f, chasingMoveInterval);
                return;
            }

            if (Time.time < _nextMoveAt)
            {
                return;
            }

            _nextMoveAt = Time.time + Mathf.Max(0.05f, roamingMoveInterval);
            TryMoveToAdjacentRoamCell();
        }

        private void UpdateCloseChasing()
        {
            if (SpottedThreat == null || SpottedThreat.ThreatTransform == null)
            {
                ReturnToRoaming();
                return;
            }

            FaceThreat(SpottedThreat.ThreatTransform.position);

            float range = Mathf.Max(0.1f, closeAttackRange);
            if (IsThreatInRange(SpottedThreat.ThreatTransform.position, range))
            {
                _state = EnemyState.CloseAttacking;
                return;
            }

            if (Time.time < _nextMoveAt)
                return;

            _nextMoveAt = Time.time + Mathf.Max(0.05f, chasingMoveInterval);
            if (TryMoveTowardThreat(SpottedThreat.ThreatTransform.position))
            {
                _stuckMoveAttempts = 0;
                return;
            }

            _stuckMoveAttempts++;
            if (_stuckMoveAttempts >= MaxStuckMoveAttempts)
            {
                TryMoveTowardHomeCell();
                _stuckMoveAttempts = 0;
            }
        }

        private void UpdateCloseAttacking()
        {
            if (SpottedThreat == null || SpottedThreat.ThreatTransform == null)
            {
                ReturnToRoaming();
                return;
            }

            FaceThreat(SpottedThreat.ThreatTransform.position);

            float range = Mathf.Max(0.1f, closeAttackRange);
            if (!IsThreatInRange(SpottedThreat.ThreatTransform.position, range))
            {
                _state = EnemyState.CloseChasing;
                _nextMoveAt = Time.time + Mathf.Max(0.05f, chasingMoveInterval);
                return;
            }

            TryAttackCurrentThreat();
        }

        private void UpdateRangedChasing()
        {
            if (SpottedThreat == null || SpottedThreat.ThreatTransform == null)
            {
                ReturnToRoaming();
                return;
            }

            Vector3 threatPosition = SpottedThreat.ThreatTransform.position;
            float range = Mathf.Max(0.1f, attackType != null ? attackType.Range : closeAttackRange);
            bool canAttack = TryEvaluateRangedAttack(threatPosition, SpottedThreat.ThreatTransform, range, out var shotDirection);
            if (canAttack)
            {
                CancelMovement();
                FaceDirection(shotDirection);
                _stuckMoveAttempts = 0;
                _state = EnemyState.RangedAttacking;
                return;
            }

            FaceThreat(threatPosition);

            if (Time.time < _nextMoveAt)
                return;

            _nextMoveAt = Time.time + Mathf.Max(0.05f, chasingMoveInterval);
            if (TryMoveForRangedLineOfSight(threatPosition, range, SpottedThreat.ThreatTransform))
            {
                _stuckMoveAttempts = 0;
                return;
            }

            _stuckMoveAttempts++;
            if (_stuckMoveAttempts >= MaxStuckMoveAttempts)
            {
                TryMoveTowardHomeCell();
                _stuckMoveAttempts = 0;
            }
        }

        private void UpdateRangedAttacking()
        {
            if (SpottedThreat == null || SpottedThreat.ThreatTransform == null)
            {
                ReturnToRoaming();
                return;
            }

            Vector3 threatPosition = SpottedThreat.ThreatTransform.position;
            float range = Mathf.Max(0.1f, attackType != null ? attackType.Range : closeAttackRange);
            if (!TryEvaluateRangedAttack(threatPosition, SpottedThreat.ThreatTransform, range, out var shotDirection))
            {
                _state = EnemyState.RangedChasing;
                _nextMoveAt = Time.time + Mathf.Max(0.05f, chasingMoveInterval);
                return;
            }

            FaceDirection(shotDirection);
            TryAttackCurrentThreat();
        }

        private void ReturnToRoaming()
        {
            SpottedThreat = null;
            _state = EnemyState.Roaming;
            _currentDirection = Vector2Int.zero;
            _remainingStraightSteps = 0;
            _stuckMoveAttempts = 0;
        }

        private bool ShouldLoseThreat(IThreat threat)
        {
            if (threat == null || threat.ThreatTransform == null)
                return true;

            float maxDistance = Mathf.Max(threatScanRadius, loseThreatDistance);
            Vector3 delta = threat.ThreatTransform.position - GetEnemyReferencePosition();
            delta.y = 0f;
            return delta.sqrMagnitude > maxDistance * maxDistance;
        }

        private void FaceThreat(Vector3 threatPosition)
        {
            var threatOffset = threatPosition - GetEnemyReferencePosition();
            threatOffset.y = 0f;
            if (threatOffset.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(threatOffset.normalized, Vector3.up);
            }
        }

        private bool IsThreatInRange(Vector3 threatPosition, float range)
        {
            Vector3 delta = threatPosition - GetEnemyReferencePosition();
            delta.y = 0f;
            return delta.sqrMagnitude <= range * range;
        }

        private void TryAttackCurrentThreat()
        {
            if (_attackController != null && attackType != null)
            {
                _attackController.TryExecuteAttack(attackType);
            }
        }

        private void RebuildRoamArea()
        {
            _roamCells.Clear();
            _roamRegionCells.Clear();

            if (_enemy == null || !_enemy.TryGetOccupiedCell(out var startCell))
            {
                return;
            }

            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(startCell);
            _roamRegionCells.Add(startCell);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                if (IsRoamCell(current))
                {
                    _roamCells.Add(current);
                }

                for (var i = 0; i < 4; i++)
                {
                    var next = current + GetDirection(i);
                    if (_roamRegionCells.Contains(next))
                    {
                        continue;
                    }

                    if (!IsRoamRegionCell(next))
                    {
                        continue;
                    }

                    _roamRegionCells.Add(next);
                    frontier.Enqueue(next);
                }
            }

            if (_roamCells.Count == 0)
            {
                _roamCells.Add(startCell);
            }

            _homeCell = startCell;
            _hasHomeCell = true;
        }

        private bool IsRoamRegionCell(Vector2Int cell)
        {
            return _enemy != null && _enemy.DungeonBuilder != null && _enemy.DungeonBuilder.IsCellWalkable(cell);
        }

        private bool IsRoamCell(Vector2Int cell)
        {
            if (!IsRoamRegionCell(cell))
            {
                return false;
            }

            var buffer = Mathf.Max(0, wallBufferCells);
            for (var dx = -buffer; dx <= buffer; dx++)
            {
                for (var dy = -buffer; dy <= buffer; dy++)
                {
                    var nearby = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (!IsRoamRegionCell(nearby))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void TryMoveToAdjacentRoamCell()
        {
            if (_isMoving || _enemy == null || !_enemy.TryGetOccupiedCell(out var currentCell))
            {
                return;
            }

            Vector2Int nextCell;
            if (TryGetNextDirectionalCell(currentCell, out nextCell))
            {
                StartMoveToCell(nextCell, roamingMoveDuration);
            }
        }

        private bool TryGetNextDirectionalCell(Vector2Int currentCell, out Vector2Int nextCell)
        {
            nextCell = currentCell;

            if (_currentDirection != Vector2Int.zero && _remainingStraightSteps > 0)
            {
                var forwardCell = currentCell + _currentDirection;
                if (CanMoveTo(forwardCell))
                {
                    _remainingStraightSteps--;
                    nextCell = forwardCell;
                    return true;
                }

                _remainingStraightSteps = 0;
                _currentDirection = Vector2Int.zero;
            }

            _adjacentCandidates.Clear();
            var reverse = new Vector2Int(-_currentDirection.x, -_currentDirection.y);

            for (var i = 0; i < 4; i++)
            {
                var direction = GetDirection(i);
                var candidate = currentCell + direction;
                if (!CanMoveTo(candidate))
                {
                    continue;
                }

                if (_currentDirection != Vector2Int.zero && direction == reverse)
                {
                    continue;
                }

                _adjacentCandidates.Add(direction);
            }

            if (_adjacentCandidates.Count == 0)
            {
                for (var i = 0; i < 4; i++)
                {
                    var direction = GetDirection(i);
                    if (CanMoveTo(currentCell + direction))
                    {
                        _adjacentCandidates.Add(direction);
                    }
                }
            }

            if (_adjacentCandidates.Count == 0)
            {
                return false;
            }

            _currentDirection = _adjacentCandidates[Random.Range(0, _adjacentCandidates.Count)];
            _remainingStraightSteps = Mathf.Max(0, Random.Range(Mathf.Max(1, minStraightSteps), Mathf.Max(Mathf.Max(1, minStraightSteps), maxStraightSteps) + 1) - 1);
            nextCell = currentCell + _currentDirection;
            return true;
        }

        private bool CanMoveTo(Vector2Int cell)
        {
            if (!_roamCells.Contains(cell))
            {
                return false;
            }

            return !GridCellOccupantRegistry.IsCellOccupied(cell);
        }

        private bool CanMoveToChaseCell(Vector2Int cell)
        {
            if (!IsRoamRegionCell(cell))
            {
                return false;
            }

            return !GridCellOccupantRegistry.IsCellOccupied(cell);
        }

        private bool TryMoveTowardThreat(Vector3 threatPosition)
        {
            if (_isMoving || _enemy == null || !_enemy.TryGetOccupiedCell(out var currentCell))
            {
                return false;
            }

            if (_enemy.DungeonBuilder != null && _enemy.DungeonBuilder.TryWorldToCell(threatPosition, out var threatCell) && TryGetNextChasePathStep(currentCell, threatCell, out var nextPathCell))
            {
                StartMoveToCell(nextPathCell, chasingMoveDuration);
                return true;
            }

            bool hasCandidate = false;
            Vector2Int bestCell = currentCell;
            float bestDistance = float.MaxValue;

            for (var i = 0; i < 4; i++)
            {
                var candidate = currentCell + GetDirection(i);
                if (!CanMoveToChaseCell(candidate))
                {
                    continue;
                }

                float distance = DistanceSqToThreat(candidate, threatPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                    hasCandidate = true;
                }
            }

            if (hasCandidate)
            {
                StartMoveToCell(bestCell, chasingMoveDuration);
                return true;
            }

            return false;
        }

        private bool TryMoveForRangedLineOfSight(Vector3 threatPosition, float range, Transform threatTransform)
        {
            if (_isMoving || _enemy == null || !_enemy.TryGetOccupiedCell(out var currentCell))
            {
                return false;
            }

            bool hasCandidate = false;
            Vector2Int bestCell = currentCell;
            int bestEstimatedSteps = int.MaxValue;
            int bestPriority = int.MaxValue;
            float bestDistanceDelta = float.MaxValue;

            for (var i = 0; i < 4; i++)
            {
                var candidate = currentCell + GetDirection(i);
                if (!CanMoveToChaseCell(candidate))
                {
                    continue;
                }

                Vector3 candidateWorld = _enemy.GetCellCenterWorld(candidate);
                bool hasGridShotLane = TryGetGridShotLane(candidateWorld, threatPosition, out _, out var laneDistance);
                bool hasLos = hasGridShotLane && HasLineOfSightFrom(candidateWorld, threatPosition, threatTransform);
                bool inRange = hasGridShotLane && laneDistance <= range;
                int estimatedSteps = EstimateStepsUntilRangedInRange(candidate, threatPosition, range);

                int priority = hasLos && inRange ? 0 : hasLos ? 1 : inRange ? 2 : hasGridShotLane ? 3 : 4;
                float distanceDelta = hasGridShotLane ? Mathf.Abs(laneDistance - range) : float.MaxValue;

                if (!hasCandidate ||
                    estimatedSteps < bestEstimatedSteps ||
                    (estimatedSteps == bestEstimatedSteps && priority < bestPriority) ||
                    (estimatedSteps == bestEstimatedSteps && priority == bestPriority && distanceDelta < bestDistanceDelta))
                {
                    bestEstimatedSteps = estimatedSteps;
                    bestPriority = priority;
                    bestDistanceDelta = distanceDelta;
                    bestCell = candidate;
                    hasCandidate = true;
                }
            }

            if (hasCandidate)
            {
                StartMoveToCell(bestCell, chasingMoveDuration);
                return true;
            }

            return false;
        }

        private bool TryMoveTowardHomeCell()
        {
            if (!_hasHomeCell || _isMoving || _enemy == null || !_enemy.TryGetOccupiedCell(out var currentCell))
            {
                return false;
            }

            if (!TryGetNextChasePathStep(currentCell, _homeCell, out var nextCell))
            {
                return false;
            }

            StartMoveToCell(nextCell, chasingMoveDuration);
            return true;
        }

        private bool TryGetNextChasePathStep(Vector2Int startCell, Vector2Int targetCell, out Vector2Int nextCell)
        {
            nextCell = startCell;

            if (startCell == targetCell)
            {
                return false;
            }

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var firstStepByCell = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(startCell);
            visited.Add(startCell);
            firstStepByCell[startCell] = startCell;

            int bestDistance = Mathf.Abs(startCell.x - targetCell.x) + Mathf.Abs(startCell.y - targetCell.y);
            Vector2Int bestCell = startCell;
            int explored = 0;

            while (queue.Count > 0 && explored < MaxPathSearchNodes)
            {
                var current = queue.Dequeue();
                explored++;

                for (var i = 0; i < 4; i++)
                {
                    var neighbor = current + GetDirection(i);
                    if (visited.Contains(neighbor) || !CanMoveToChaseCell(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    firstStepByCell[neighbor] = current == startCell ? neighbor : firstStepByCell[current];

                    int distance = Mathf.Abs(neighbor.x - targetCell.x) + Mathf.Abs(neighbor.y - targetCell.y);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestCell = neighbor;
                    }

                    if (neighbor == targetCell)
                    {
                        nextCell = firstStepByCell[neighbor];
                        return true;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            if (bestCell != startCell)
            {
                nextCell = firstStepByCell[bestCell];
                return true;
            }

            return false;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private float DistanceSqToThreat(Vector2Int candidateCell, Vector3 threatPosition)
        {
            Vector3 world = _enemy.GetCellCenterWorld(candidateCell);
            float dx = world.x - threatPosition.x;
            float dz = world.z - threatPosition.z;
            return (dx * dx) + (dz * dz);
        }

        private bool HasLineOfSightFrom(Vector3 fromWorld, Vector3 toWorld, Transform targetTransform)
        {
            Vector3 from = fromWorld + Vector3.up * 0.5f;
            Vector3 to = toWorld + Vector3.up * 0.5f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
                return true;

            int hitCount = Physics.RaycastNonAlloc(from, delta / distance, _lineOfSightBuffer, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return true;

            RaycastHit nearestHit = default;
            float nearestDistance = float.MaxValue;
            bool hasNearest = false;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _lineOfSightBuffer[i];
                if (hit.collider == null)
                {
                    continue;
                }

                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<CombatProjectile>() != null)
                {
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                    hasNearest = true;
                }
            }

            if (!hasNearest)
            {
                return true;
            }

            return targetTransform != null && nearestHit.collider.transform.IsChildOf(targetTransform);
        }

        private bool TryEvaluateRangedAttack(Vector3 threatPosition, Transform threatTransform, float range, out Vector3 shotDirection)
        {
            return TryEvaluateRangedAttackFrom(GetEnemyReferencePosition(), threatPosition, threatTransform, range, out shotDirection);
        }

        private bool TryEvaluateRangedAttackFrom(Vector3 fromWorld, Vector3 threatPosition, Transform threatTransform, float range, out Vector3 shotDirection)
        {
            shotDirection = Vector3.zero;

            bool hasGridShotLane = TryGetGridShotLane(fromWorld, threatPosition, out var laneDirection, out var laneDistance);
            if (!hasGridShotLane)
            {
                return false;
            }

            bool inRange = laneDistance <= range;
            if (!inRange)
            {
                return false;
            }

            bool hasLineOfSight = HasLineOfSightFrom(fromWorld, threatPosition, threatTransform);
            if (!hasLineOfSight)
            {
                return false;
            }

            shotDirection = laneDirection;
            return true;
        }

        private int EstimateStepsUntilRangedInRange(Vector2Int fromCell, Vector3 threatPosition, float range)
        {
            if (_enemy == null || _enemy.DungeonBuilder == null || !_enemy.DungeonBuilder.TryWorldToCell(threatPosition, out var threatCell))
            {
                return int.MaxValue;
            }

            int rangeCells = Mathf.Max(1, Mathf.FloorToInt(range / Mathf.Max(0.1f, _enemy.DungeonBuilder.cellSize)));
            int minY = threatCell.y - rangeCells;
            int maxY = threatCell.y + rangeCells;
            int minX = threatCell.x - rangeCells;
            int maxX = threatCell.x + rangeCells;

            int stepsViaXAlignment = Mathf.Abs(fromCell.x - threatCell.x) + DistanceToInterval(fromCell.y, minY, maxY);
            int stepsViaYAlignment = Mathf.Abs(fromCell.y - threatCell.y) + DistanceToInterval(fromCell.x, minX, maxX);

            return Mathf.Min(stepsViaXAlignment, stepsViaYAlignment);
        }

        private static int DistanceToInterval(int value, int min, int max)
        {
            if (value < min)
            {
                return min - value;
            }

            if (value > max)
            {
                return value - max;
            }

            return 0;
        }

        private bool TryGetGridShotLane(Vector3 fromWorld, Vector3 toWorld, out Vector3 shotDirection, out float laneDistance)
        {
            shotDirection = Vector3.zero;
            laneDistance = 0f;

            if (_enemy == null || _enemy.DungeonBuilder == null)
            {
                return false;
            }

            if (!_enemy.DungeonBuilder.TryWorldToCell(fromWorld, out var fromCell) || !_enemy.DungeonBuilder.TryWorldToCell(toWorld, out var toCell))
            {
                return false;
            }

            var delta = toCell - fromCell;
            if (delta == Vector2Int.zero)
            {
                return false;
            }

            if (delta.x != 0 && delta.y != 0)
            {
                return false;
            }

            if (delta.x != 0)
            {
                shotDirection = delta.x > 0 ? Vector3.right : Vector3.left;
                laneDistance = Mathf.Abs(delta.x) * Mathf.Max(0.1f, _enemy.DungeonBuilder.cellSize);
                return true;
            }

            shotDirection = delta.y > 0 ? Vector3.forward : Vector3.back;
            laneDistance = Mathf.Abs(delta.y) * Mathf.Max(0.1f, _enemy.DungeonBuilder.cellSize);
            return true;
        }

        private void FaceDirection(Vector3 worldDirection)
        {
            var flatDirection = new Vector3(worldDirection.x, 0f, worldDirection.z);
            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }
        }

        private System.Collections.IEnumerator MoveToCellRoutine(Vector2Int targetCell, float durationSeconds)
        {
            _isMoving = true;

            _moveKeepSnappedBefore = _enemy.KeepSnappedToGrid;
            _enemy.KeepSnappedToGrid = false;

            try
            {
                var start = _enemy.GetAnchorWorldPosition();
                var end = _enemy.GetCellCenterWorld(targetCell);
                var duration = Mathf.Max(0.01f, durationSeconds);

                for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    var t = Mathf.Clamp01(elapsed / duration);
                    _enemy.SetAnchorWorldPosition(Vector3.Lerp(start, end, t));
                    yield return null;
                }

                _enemy.SetAnchorWorldPosition(end);
                _enemy.TrySetOccupiedCell(targetCell);
            }
            finally
            {
                _enemy.KeepSnappedToGrid = _moveKeepSnappedBefore;
                _isMoving = false;
                _moveRoutine = null;
            }
        }

        private void StartMoveToCell(Vector2Int targetCell, float durationSeconds)
        {
            if (_isMoving)
            {
                return;
            }

            _moveRoutine = StartCoroutine(MoveToCellRoutine(targetCell, durationSeconds));
        }

        private void CancelMovement()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
                _isMoving = false;
                if (_enemy != null)
                {
                    _enemy.KeepSnappedToGrid = _moveKeepSnappedBefore;
                }
            }
        }

        private bool TrySpotThreat(out IThreat threat)
        {
            threat = null;

            if (Time.time < _nextThreatScanAt)
            {
                return false;
            }

            _nextThreatScanAt = Time.time + Mathf.Max(0.01f, threatScanInterval);

            var hitCount = Physics.OverlapSphereNonAlloc(
                GetEnemyReferencePosition(),
                Mathf.Max(0.1f, threatScanRadius),
                _threatBuffer,
                threatLayerMask,
                QueryTriggerInteraction.Collide);

            float bestDistanceSq = float.MaxValue;
            Vector3 enemyPos = GetEnemyReferencePosition();
            _threatScanVisited.Clear();

            for (var i = 0; i < hitCount; i++)
            {
                var collider = _threatBuffer[i];
                _threatBuffer[i] = null;
                if (collider == null)
                {
                    continue;
                }

                if (!TryResolveThreat(collider, out var candidate) || candidate == null)
                {
                    continue;
                }

                var threatTransform = candidate.ThreatTransform;
                if (threatTransform == null)
                {
                    _threatByColliderCache.Remove(collider);
                    continue;
                }

                if (!_threatScanVisited.Add(threatTransform))
                {
                    continue;
                }

                if (!candidate.IsThreatTo(gameObject))
                {
                    continue;
                }

                Vector3 delta = threatTransform.position - enemyPos;
                delta.y = 0f;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                {
                    continue;
                }

                if (!HasLineOfSightFrom(enemyPos, threatTransform.position, threatTransform))
                {
                    continue;
                }

                threat = candidate;
                bestDistanceSq = distanceSq;
            }

            return threat != null;
        }

        private bool TryResolveThreat(Collider collider, out IThreat threat)
        {
            if (_threatByColliderCache.TryGetValue(collider, out threat))
            {
                return threat != null;
            }

            threat = collider.GetComponentInParent<IThreat>();
            _threatByColliderCache[collider] = threat;
            return threat != null;
        }

        private static Vector2Int GetDirection(int index)
        {
            return index switch
            {
                0 => Vector2Int.up,
                1 => Vector2Int.right,
                2 => Vector2Int.down,
                _ => Vector2Int.left
            };
        }

        private Vector3 GetEnemyReferencePosition()
        {
            return _enemy != null ? _enemy.GetAnchorWorldPosition() : transform.position;
        }
    }
}
