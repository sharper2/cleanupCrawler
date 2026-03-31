using UnityEngine;

namespace DungeonGenerator
{
    [RequireComponent(typeof(HealthComponent))]
    public class StaticEnemy : MonoBehaviour, IGridCellOccupant
    {
        [Header("Grid")]
        [SerializeField] private DungeonBasic3DBuilder dungeonBuilder;
        [SerializeField] private Transform gridAnchor;
        [SerializeField] private float yOffset = 0.5f;
        [SerializeField] private bool keepSnappedToGrid = true;

        [SerializeField] private Color gizmoColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private float gizmoRadius = 0.3f;

        private bool _hasCell;
        private Vector2Int _occupiedCell;

        public DungeonBasic3DBuilder DungeonBuilder => dungeonBuilder;
        public bool KeepSnappedToGrid
        {
            get => keepSnappedToGrid;
            set => keepSnappedToGrid = value;
        }

        private void Awake()
        {
            if (dungeonBuilder == null)
            {
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();
            }
        }

        private void OnEnable()
        {
            GridCellOccupantRegistry.Register(this);
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
            GridCellOccupantRegistry.Unregister(this);
        }

        public bool TryGetOccupiedCell(out Vector2Int cell)
        {
            UpdateOccupiedCellFromAnchor();
            cell = _occupiedCell;
            return _hasCell;
        }

        public bool TrySetOccupiedCell(Vector2Int cell)
        {
            if (dungeonBuilder == null || !dungeonBuilder.IsCellWalkable(cell))
            {
                return false;
            }

            Vector3 anchorPosition = GetAnchorPosition();
            Vector3 cellCenter = GetCellCenterWorld(cell);
            transform.position += cellCenter - anchorPosition;

            _occupiedCell = cell;
            _hasCell = true;
            return true;
        }

        public Vector3 GetAnchorWorldPosition()
        {
            return GetAnchorPosition();
        }

        public void SetAnchorWorldPosition(Vector3 anchorWorldPosition)
        {
            Vector3 anchorPosition = GetAnchorPosition();
            transform.position += anchorWorldPosition - anchorPosition;
        }

        public Vector3 GetCellCenterWorld(Vector2Int cell)
        {
            if (dungeonBuilder == null)
            {
                return transform.position;
            }

            return dungeonBuilder.CellCenterToWorld(cell, yOffset);
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
