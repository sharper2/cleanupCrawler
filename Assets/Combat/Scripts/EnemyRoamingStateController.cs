using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    [RequireComponent(typeof(StaticEnemy))]
    public class EnemyRoamingStateController : MonoBehaviour
    {
        public enum EnemyState
        {
            Roaming,
            ThreatSpotted
        }

        [Header("Roaming")]
        [SerializeField] private float moveInterval = 0.5f;
        [SerializeField] private float moveDuration = 0.2f;
        [SerializeField] private int wallBufferCells = 1;
        [SerializeField] private int minStraightSteps = 2;
        [SerializeField] private int maxStraightSteps = 4;

        [Header("Threat Detection")]
        [SerializeField] private float threatScanRadius = 6f;
        [SerializeField] private LayerMask threatLayerMask = ~0;

        private readonly Collider[] _threatBuffer = new Collider[32];
        private readonly HashSet<Vector2Int> _roamRegionCells = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _roamCells = new HashSet<Vector2Int>();
        private readonly List<Vector2Int> _adjacentCandidates = new List<Vector2Int>(4);

        private StaticEnemy _enemy;
        private EnemyState _state = EnemyState.Roaming;
        private float _nextMoveAt;
        private bool _isMoving;
        private Vector2Int _currentDirection;
        private int _remainingStraightSteps;

        public EnemyState State => _state;
        public IThreat SpottedThreat { get; private set; }

        private void Awake()
        {
            _enemy = GetComponent<StaticEnemy>();
        }

        private void Start()
        {
            RebuildRoamArea();
            _nextMoveAt = Time.time + Mathf.Max(0.05f, moveInterval);
        }

        private void Update()
        {
            if (_state != EnemyState.Roaming)
            {
                return;
            }

            if (TrySpotThreat(out var threat))
            {
                SpottedThreat = threat;
                _state = EnemyState.ThreatSpotted;
                return;
            }

            if (Time.time < _nextMoveAt)
            {
                return;
            }

            _nextMoveAt = Time.time + Mathf.Max(0.05f, moveInterval);
            TryMoveToAdjacentRoamCell();
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
                StartCoroutine(MoveToCellRoutine(nextCell));
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

        private System.Collections.IEnumerator MoveToCellRoutine(Vector2Int targetCell)
        {
            _isMoving = true;

            var keepSnappedBeforeMove = _enemy.KeepSnappedToGrid;
            _enemy.KeepSnappedToGrid = false;

            var start = _enemy.GetAnchorWorldPosition();
            var end = _enemy.GetCellCenterWorld(targetCell);
            var duration = Mathf.Max(0.01f, moveDuration);

            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                _enemy.SetAnchorWorldPosition(Vector3.Lerp(start, end, t));
                yield return null;
            }

            _enemy.SetAnchorWorldPosition(end);
            _enemy.TrySetOccupiedCell(targetCell);
            _enemy.KeepSnappedToGrid = keepSnappedBeforeMove;

            _isMoving = false;
        }

        private bool TrySpotThreat(out IThreat threat)
        {
            threat = null;

            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                Mathf.Max(0.1f, threatScanRadius),
                _threatBuffer,
                threatLayerMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < hitCount; i++)
            {
                var collider = _threatBuffer[i];
                _threatBuffer[i] = null;
                if (collider == null)
                {
                    continue;
                }

                var candidate = collider.GetComponentInParent<IThreat>();
                if (candidate == null || !candidate.IsThreatTo(gameObject))
                {
                    continue;
                }

                threat = candidate;
                return true;
            }

            return false;
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
    }
}
