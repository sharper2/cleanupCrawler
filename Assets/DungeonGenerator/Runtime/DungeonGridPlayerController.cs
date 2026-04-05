using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    public class DungeonGridPlayerController : MonoBehaviour, IGridCellOccupant
    {
        [Header("References")]
        public DungeonBasic3DBuilder dungeonBuilder;

        [Header("Movement")]
        public float moveDuration = 0.12f;
        public float stepPauseDuration = 0.06f;
        public float turnDuration = 0.08f;
        public float yOffset = 0.5f;
        public bool snapToStartOnPlay = true;
        public bool forceKinematicRigidbody = true;
        public Transform gridAnchor;

        private bool _isMoving;
        private bool _isTurning;
        private bool _hasAttemptedAutoBuild;
        private bool _hasCurrentCell;
        private Vector2Int _currentCell;
        private Vector2Int _queuedMove;
        private int _queuedTurn;
        private Vector2Int _facing = Vector2Int.up;
        private Rigidbody _rigidbody;
        private float _nextStepAllowedAt;

        private void Awake()
        {
            if (dungeonBuilder == null)
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null && forceKinematicRigidbody)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
            }
        }

        private void Start()
        {
            _facing = YawToFacing(transform.eulerAngles.y);
            ApplyFacingRotation();

            EnsureDungeonReady(true);

            if (snapToStartOnPlay)
                SnapToStart();
            else
                ResolveCurrentCell();
        }

        public bool TryTeleportToCell(Vector2Int cell)
        {
            if (dungeonBuilder == null || !dungeonBuilder.IsCellWalkable(cell))
            {
                return false;
            }

            CancelActiveGridMotion();

            _currentCell = cell;
            _hasCurrentCell = true;
            _nextStepAllowedAt = Time.time;

            SetAnchorWorldPosition(dungeonBuilder.CellCenterToWorld(cell, yOffset));
            return true;
        }

        /// <summary>
        /// Snaps the player to a grid cell without validating walkability (for level bootstrap when the walkable set must match the layout).
        /// Prefer <see cref="TryTeleportToCell"/> when possible.
        /// </summary>
        public void ForceSetGridCell(Vector2Int cell)
        {
            if (dungeonBuilder == null)
            {
                return;
            }

            CancelActiveGridMotion();

            _currentCell = cell;
            _hasCurrentCell = true;
            _nextStepAllowedAt = Time.time;

            SetAnchorWorldPosition(dungeonBuilder.CellCenterToWorld(cell, yOffset));
        }

        /// <summary>
        /// Stops move/turn coroutines and clears flags. Snaps yaw to the current logical facing so a turn interrupted
        /// mid-slerp cannot leave rotation out of sync with grid movement (which would block or confuse input).
        /// </summary>
        private void CancelActiveGridMotion()
        {
            StopAllCoroutines();
            _queuedMove = Vector2Int.zero;
            _queuedTurn = 0;
            _isMoving = false;
            _isTurning = false;
            ApplyFacingRotation();
        }

        /// <summary>
        /// Pushes the player up to <paramref name="cellCount"/> cardinal steps away from <paramref name="attackerCell"/>
        /// (e.g. melee knockback). Uses the same stepped motion as normal movement. Stops early if a wall,
        /// non-walkable cell, or other occupant blocks the path.
        /// </summary>
        public bool TryKnockbackFromSourceCell(Vector2Int attackerCell, int cellCount)
        {
            if (cellCount <= 0 || dungeonBuilder == null)
            {
                return false;
            }

            CancelActiveGridMotion();

            SnapAnchorToNearestWalkableCell();

            if (!_hasCurrentCell && !ResolveCurrentCell())
            {
                return false;
            }

            var dir = CardinalDirectionFromCells(attackerCell, _currentCell);
            if (dir == Vector2Int.zero)
            {
                return false;
            }

            var path = new List<Vector2Int>();
            var candidate = _currentCell;
            for (var step = 0; step < cellCount; step++)
            {
                var next = candidate + dir;
                if (!IsCellValidForKnockbackStep(next))
                {
                    break;
                }

                path.Add(next);
                candidate = next;
            }

            if (path.Count == 0)
            {
                return false;
            }

            StartCoroutine(KnockbackAlongPathRoutine(path));
            return true;
        }

        private IEnumerator KnockbackAlongPathRoutine(List<Vector2Int> path)
        {
            foreach (var next in path)
            {
                yield return MoveToCell(next);
            }
        }

        /// <summary>
        /// After interrupting movement, align logical cell and anchor to the grid cell under the anchor (or nearest resolve).
        /// </summary>
        private void SnapAnchorToNearestWalkableCell()
        {
            if (dungeonBuilder == null)
            {
                return;
            }

            if (dungeonBuilder.TryWorldToCell(GetAnchorWorldPosition(), out var cell))
            {
                _currentCell = cell;
                _hasCurrentCell = true;
                SetAnchorWorldPosition(dungeonBuilder.CellCenterToWorld(_currentCell, yOffset));
                _nextStepAllowedAt = Time.time;
            }
        }

        private bool IsCellValidForKnockbackStep(Vector2Int cell)
        {
            if (!dungeonBuilder.IsCellWalkable(cell))
            {
                return false;
            }

            if (HasWallAtCell(cell))
            {
                return false;
            }

            return !IsCellOccupied(cell);
        }

        private static Vector2Int CardinalDirectionFromCells(Vector2Int fromCell, Vector2Int toCell)
        {
            var d = toCell - fromCell;
            if (d.x != 0 && d.y != 0)
            {
                return Mathf.Abs(d.x) >= Mathf.Abs(d.y)
                    ? new Vector2Int((int)Mathf.Sign(d.x), 0)
                    : new Vector2Int(0, (int)Mathf.Sign(d.y));
            }

            if (d.x != 0)
            {
                return new Vector2Int((int)Mathf.Sign(d.x), 0);
            }

            if (d.y != 0)
            {
                return new Vector2Int(0, (int)Mathf.Sign(d.y));
            }

            return Vector2Int.zero;
        }

        private void OnEnable()
        {
            GridCellOccupantRegistry.Register(this);
        }

        private void OnDisable()
        {
            GridCellOccupantRegistry.Unregister(this);
        }

        public bool TryGetOccupiedCell(out Vector2Int cell)
        {
            if (!_hasCurrentCell && !ResolveCurrentCell())
            {
                cell = default;
                return false;
            }

            cell = _currentCell;
            return true;
        }

        private void Update()
        {
            if (GameplayPause.IsPaused)
            {
                return;
            }

            if (_isMoving || _isTurning || dungeonBuilder == null)
                return;

            if (!EnsureDungeonReady(false))
                return;

            if (!_hasCurrentCell && !ResolveCurrentCell())
                return;

            SnapToTrackedCell();
            TryCollectFloorItemAtCurrentCell();
            CaptureInput();

            if (_queuedTurn != 0)
            {
                StartCoroutine(TurnRoutine(_queuedTurn));
                _queuedTurn = 0;
                return;
            }

            if (_queuedMove == Vector2Int.zero)
                return;

            if (Time.time < _nextStepAllowedAt)
                return;

            Vector2Int localMove = _queuedMove;
            _queuedMove = Vector2Int.zero;

            Vector2Int direction = ToWorldDirection(localMove);

            Vector2Int targetCell = _currentCell + direction;
            if (!dungeonBuilder.IsCellWalkable(targetCell) || HasWallAtCell(targetCell) || IsCellOccupied(targetCell))
                return;

            StartCoroutine(MoveToCell(targetCell));
        }

        private void CaptureInput()
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                _queuedTurn = -1;
            }
            else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                _queuedTurn = 1;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                _queuedMove = Vector2Int.up;
            }
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                _queuedMove = Vector2Int.down;
            }
            else if (Input.GetKey(KeyCode.A))
            {
                _queuedMove = Vector2Int.left;
            }
            else if (Input.GetKey(KeyCode.D))
            {
                _queuedMove = Vector2Int.right;
            }
        }

        [ContextMenu("Snap To Dungeon Start")]
        public void SnapToStart()
        {
            if (dungeonBuilder == null)
                return;

            CancelActiveGridMotion();

            EnsureDungeonReady(true);

            if (dungeonBuilder.TryGetStartCell(out _currentCell))
            {
                SetAnchorWorldPosition(dungeonBuilder.CellCenterToWorld(_currentCell, yOffset));
                _hasCurrentCell = true;
            }
            else
                ResolveCurrentCell();
        }

        private void SnapToTrackedCell()
        {
            if (!_hasCurrentCell || dungeonBuilder == null)
                return;

            Vector3 snappedPosition = dungeonBuilder.CellCenterToWorld(_currentCell, yOffset);
            if ((GetAnchorWorldPosition() - snappedPosition).sqrMagnitude > 0.0001f)
                SetAnchorWorldPosition(snappedPosition);
        }

        private Vector2Int ToWorldDirection(Vector2Int localMove)
        {
            Vector2Int forward = _facing;
            Vector2Int right = new Vector2Int(forward.y, -forward.x);

            return (right * localMove.x) + (forward * localMove.y);
        }

        private IEnumerator TurnRoutine(int direction)
        {
            _isTurning = true;

            float turnAmount = direction < 0 ? -90f : 90f;
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(0f, turnAmount, 0f);
            float duration = Mathf.Max(0.01f, turnDuration);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                transform.rotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }

            if (direction < 0)
                _facing = new Vector2Int(-_facing.y, _facing.x);
            else
                _facing = new Vector2Int(_facing.y, -_facing.x);

            ApplyFacingRotation();

            _isTurning = false;
        }

        private bool HasWallAtCell(Vector2Int cell)
        {
            Transform generatedRoot = dungeonBuilder.transform.Find("GeneratedDungeon");
            if (generatedRoot == null)
                return false;

            string wallName = "Wall_" + cell.x + "_" + cell.y;
            return generatedRoot.Find(wallName) != null;
        }

        private static bool IsCellOccupied(Vector2Int cell)
        {
            return GridCellOccupantRegistry.IsCellOccupied(cell);
        }

        private void TryCollectFloorItemAtCurrentCell()
        {
            if (!_hasCurrentCell)
                return;

            while (FloorItemRegistry.TryGetItemAtCell(_currentCell, out IFloorItem floorItem))
            {
                if (floorItem == null || !floorItem.TryCollect(gameObject))
                    break;
            }
        }

        private void ApplyFacingRotation()
        {
            transform.rotation = Quaternion.Euler(0f, FacingToYaw(_facing), 0f);
        }

        /// <summary>
        /// World-space horizontal forward from grid facing (cardinal). Use for attacks/projectiles so direction matches Q/E turns.
        /// </summary>
        public Vector3 GetFacingWorldDirection()
        {
            return FacingToWorldVector(_facing);
        }

        private static Vector3 FacingToWorldVector(Vector2Int facing)
        {
            if (facing == Vector2Int.right)
            {
                return Vector3.right;
            }

            if (facing == Vector2Int.down)
            {
                return Vector3.back;
            }

            if (facing == Vector2Int.left)
            {
                return Vector3.left;
            }

            return Vector3.forward;
        }

        private static float FacingToYaw(Vector2Int facing)
        {
            if (facing == Vector2Int.right) return 90f;
            if (facing == Vector2Int.down) return 180f;
            if (facing == Vector2Int.left) return 270f;
            return 0f;
        }

        private static Vector2Int YawToFacing(float yaw)
        {
            float normalized = Mathf.Repeat(yaw, 360f);

            if (normalized >= 45f && normalized < 135f) return Vector2Int.right;
            if (normalized >= 135f && normalized < 225f) return Vector2Int.down;
            if (normalized >= 225f && normalized < 315f) return Vector2Int.left;
            return Vector2Int.up;
        }

        private bool EnsureDungeonReady(bool allowBuild)
        {
            if (dungeonBuilder == null)
                return false;

            if (dungeonBuilder.WalkableCellCount > 0)
                return true;

            if (!allowBuild || _hasAttemptedAutoBuild)
                return false;

            _hasAttemptedAutoBuild = true;
            dungeonBuilder.BuildDungeon();

            return dungeonBuilder.WalkableCellCount > 0;
        }

        private bool ResolveCurrentCell()
        {
            if (dungeonBuilder == null)
                return false;

            bool resolved = dungeonBuilder.TryWorldToCell(GetAnchorWorldPosition(), out _currentCell);
            _hasCurrentCell = resolved;
            return resolved;
        }

        private IEnumerator MoveToCell(Vector2Int targetCell)
        {
            _isMoving = true;

            Vector3 start = GetAnchorWorldPosition();
            Vector3 end = dungeonBuilder.CellCenterToWorld(targetCell, yOffset);
            float duration = Mathf.Max(0.01f, moveDuration);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetAnchorWorldPosition(Vector3.Lerp(start, end, t));
                yield return null;
            }

            SetAnchorWorldPosition(end);
            _currentCell = targetCell;
            _hasCurrentCell = true;
            _nextStepAllowedAt = Time.time + Mathf.Max(0f, stepPauseDuration);
            _isMoving = false;
        }

        private Vector3 GetAnchorWorldPosition()
        {
            return gridAnchor != null ? gridAnchor.position : GetWorldPosition();
        }

        private void SetAnchorWorldPosition(Vector3 anchorWorldPosition)
        {
            if (gridAnchor == null)
            {
                SetWorldPosition(anchorWorldPosition);
                return;
            }

            Vector3 delta = anchorWorldPosition - gridAnchor.position;
            SetWorldPosition(GetWorldPosition() + delta);
        }

        private Vector3 GetWorldPosition()
        {
            return _rigidbody != null ? _rigidbody.position : transform.position;
        }

        private void SetWorldPosition(Vector3 position)
        {
            if (_rigidbody != null)
                _rigidbody.position = position;
            else
                transform.position = position;
        }
    }
}
