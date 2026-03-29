using System.Collections;
using UnityEngine;

namespace DungeonGenerator
{
    public class DungeonGridPlayerController : MonoBehaviour
    {
        [Header("References")]
        public DungeonBasic3DBuilder dungeonBuilder;

        [Header("Movement")]
        public float moveDuration = 0.12f;
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

        private void Update()
        {
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

            Vector2Int localMove = _queuedMove;
            _queuedMove = Vector2Int.zero;

            Vector2Int direction = ToWorldDirection(localMove);

            Vector2Int targetCell = _currentCell + direction;
            if (!dungeonBuilder.IsCellWalkable(targetCell) || HasWallAtCell(targetCell) || HasEnemyAtCell(targetCell))
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

        private static bool HasEnemyAtCell(Vector2Int cell)
        {
            return StaticEnemy.IsCellOccupied(cell);
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
