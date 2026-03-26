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

        private bool _isMoving;
        private bool _isTurning;
        private bool _hasAttemptedAutoBuild;
        private Vector2Int _currentCell;
        private Vector2Int _queuedMove;
        private int _queuedTurn;
        private Vector2Int _facing = Vector2Int.up;

        private void Awake()
        {
            if (dungeonBuilder == null)
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();
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

            if (!ResolveCurrentCell())
                return;

            Vector2Int targetCell = _currentCell + direction;
            if (!dungeonBuilder.IsCellWalkable(targetCell) || HasWallAtCell(targetCell))
                return;

            StartCoroutine(MoveToCell(targetCell));
        }

        [ContextMenu("Snap To Dungeon Start")]
        public void SnapToStart()
        {
            if (dungeonBuilder == null)
                return;

            EnsureDungeonReady(true);

            if (dungeonBuilder.TryGetStartCell(out _currentCell))
                transform.position = dungeonBuilder.CellCenterToWorld(_currentCell, yOffset);
            else
                ResolveCurrentCell();
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.KeyDown)
                return;

            switch (e.keyCode)
            {
                case KeyCode.W:
                case KeyCode.UpArrow:
                    _queuedMove = Vector2Int.up;
                    break;
                case KeyCode.S:
                case KeyCode.DownArrow:
                    _queuedMove = Vector2Int.down;
                    break;
                case KeyCode.A:
                    _queuedMove = Vector2Int.left;
                    break;
                case KeyCode.D:
                    _queuedMove = Vector2Int.right;
                    break;
                case KeyCode.Q:
                case KeyCode.LeftArrow:
                    _queuedTurn = -1;
                    break;
                case KeyCode.E:
                case KeyCode.RightArrow:
                    _queuedTurn = 1;
                    break;
            }
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

            transform.rotation = end;

            if (direction < 0)
                _facing = new Vector2Int(-_facing.y, _facing.x);
            else
                _facing = new Vector2Int(_facing.y, -_facing.x);

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

            return dungeonBuilder.TryWorldToCell(transform.position, out _currentCell);
        }

        private IEnumerator MoveToCell(Vector2Int targetCell)
        {
            _isMoving = true;

            Vector3 start = transform.position;
            Vector3 end = dungeonBuilder.CellCenterToWorld(targetCell, yOffset);
            float duration = Mathf.Max(0.01f, moveDuration);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            transform.position = end;
            _currentCell = targetCell;
            _isMoving = false;
        }
    }
}
