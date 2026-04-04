using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Floor pickup that enqueues an <see cref="AbilityQueueItemDefinition"/> when the player walks onto its cell
    /// Same collection path as inventory floor pickups (IFloorItem + FloorItemRegistry).
    /// </summary>
    public sealed class FloorAbilityQueuePickup : MonoBehaviour, IFloorItem
    {
        [SerializeField] private DungeonBasic3DBuilder dungeonBuilder;
        [SerializeField] private AbilityQueueItemDefinition abilityDefinition;
        [SerializeField] private Transform gridAnchor;
        [SerializeField] private Transform modelAnchor;
        [SerializeField] private float yOffset = 0.5f;
        [SerializeField] private bool keepSnappedToGrid = true;

        private bool _hasCell;
        private Vector2Int _cell;
        private GameObject _modelInstance;

        private void Awake()
        {
            if (dungeonBuilder == null)
            {
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();
            }
        }

        private void OnEnable()
        {
            FloorItemRegistry.Register(this);
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
            FloorItemRegistry.Unregister(this);

            if (_modelInstance != null)
            {
                Destroy(_modelInstance);
                _modelInstance = null;
            }
        }

        public bool TryGetCell(out Vector2Int cell)
        {
            UpdateCellFromAnchor();
            cell = _cell;
            return _hasCell;
        }

        public bool TryCollect(GameObject collector)
        {
            if (abilityDefinition == null)
            {
                return false;
            }

            var queue = collector != null
                ? collector.GetComponentInChildren<AbilityQueueComponent>(true)
                : null;
            if (queue == null)
            {
                queue = collector != null ? collector.GetComponentInParent<AbilityQueueComponent>() : null;
            }

            if (queue == null)
            {
                queue = FindFirstObjectByType<AbilityQueueComponent>();
            }

            if (queue == null || !queue.TryEnqueue(abilityDefinition))
            {
                return false;
            }

            Destroy(gameObject);
            return true;
        }

        public bool Configure(DungeonBasic3DBuilder builder, AbilityQueueItemDefinition definition, Vector2Int cell)
        {
            dungeonBuilder = builder;
            abilityDefinition = definition;

            if (!TryBuildModel())
            {
                return false;
            }

            return TrySetCell(cell);
        }

        private bool TryBuildModel()
        {
            if (_modelInstance != null)
            {
                Destroy(_modelInstance);
                _modelInstance = null;
            }

            var prefab = abilityDefinition != null ? abilityDefinition.FloorPickupModelPrefab : null;
            if (prefab == null)
            {
                return true;
            }

            var parent = modelAnchor != null ? modelAnchor : transform;
            _modelInstance = Instantiate(prefab, parent);
            _modelInstance.transform.localPosition = Vector3.zero;
            _modelInstance.transform.localRotation = Quaternion.identity;
            _modelInstance.transform.localScale = Vector3.one;
            return true;
        }

        public bool TrySetCell(Vector2Int cell)
        {
            if (dungeonBuilder == null || !dungeonBuilder.IsCellWalkable(cell))
            {
                return false;
            }

            _cell = cell;
            _hasCell = true;

            var anchorPosition = GetAnchorPosition();
            var cellCenter = dungeonBuilder.CellCenterToWorld(cell, yOffset);
            transform.position += cellCenter - anchorPosition;
            return true;
        }

        private void SnapToGridCell()
        {
            if (!UpdateCellFromAnchor())
            {
                return;
            }

            var anchorPosition = GetAnchorPosition();
            var cellCenter = dungeonBuilder.CellCenterToWorld(_cell, yOffset);
            transform.position += cellCenter - anchorPosition;
        }

        private bool UpdateCellFromAnchor()
        {
            if (dungeonBuilder == null)
            {
                _hasCell = false;
                return false;
            }

            _hasCell = dungeonBuilder.TryWorldToCell(GetAnchorPosition(), out _cell);
            return _hasCell;
        }

        private Vector3 GetAnchorPosition()
        {
            return gridAnchor != null ? gridAnchor.position : transform.position;
        }
    }
}
