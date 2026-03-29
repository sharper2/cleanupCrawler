using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    [RequireComponent(typeof(HealthComponent))]
    public class StaticEnemy : MonoBehaviour
    {
        private static readonly HashSet<StaticEnemy> ActiveEnemies = new();

        [Header("Grid")]
        [SerializeField] private DungeonBasic3DBuilder dungeonBuilder;
        [SerializeField] private Transform gridAnchor;
        [SerializeField] private float yOffset = 0.5f;
        [SerializeField] private bool keepSnappedToGrid = true;

        [SerializeField] private Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private float gizmoRadius = 0.3f;

        private bool _hasCell;
        private Vector2Int _occupiedCell;

        private void Awake()
        {
            if (dungeonBuilder == null)
            {
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();
            }
        }

        private void OnEnable()
        {
            ActiveEnemies.Add(this);
        }

        private void Start()
        {
            SnapToGridCell();
        }

        private void LateUpdate()
        {
            if (keepSnappedToGrid)
            {
                SnapToGridCell();
            }
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
        }

        public static bool IsCellOccupied(Vector2Int cell)
        {
            foreach (var enemy in ActiveEnemies)
            {
                if (enemy.TryGetOccupiedCell(out var occupiedCell) && occupiedCell == cell)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetOccupiedCell(out Vector2Int cell)
        {
            UpdateOccupiedCellFromAnchor();
            cell = _occupiedCell;
            return _hasCell;
        }

        private void SnapToGridCell()
        {
            if (!UpdateOccupiedCellFromAnchor())
            {
                return;
            }

            Vector3 anchorPosition = GetAnchorPosition();
            Vector3 cellCenter = dungeonBuilder.CellCenterToWorld(_occupiedCell, yOffset);
            transform.position += cellCenter - anchorPosition;
        }

        private bool UpdateOccupiedCellFromAnchor()
        {
            if (dungeonBuilder == null)
            {
                _hasCell = false;
                return false;
            }

            _hasCell = dungeonBuilder.TryWorldToCell(GetAnchorPosition(), out _occupiedCell);
            return _hasCell;
        }

        private Vector3 GetAnchorPosition()
        {
            return gridAnchor != null ? gridAnchor.position : transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, Mathf.Max(0.05f, gizmoRadius));
        }
    }
}
