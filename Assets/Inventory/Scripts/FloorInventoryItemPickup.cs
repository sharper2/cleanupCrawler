using DungeonGenerator;
using UnityEngine;

public class FloorInventoryItemPickup : MonoBehaviour, IFloorItem
{
    [SerializeField] private DungeonBasic3DBuilder dungeonBuilder;
    [SerializeField] private InventoryItemDefinition itemDefinition;
    [SerializeField] private Transform gridAnchor;
    [SerializeField] private float yOffset = 0.5f;
    [SerializeField] private bool keepSnappedToGrid = true;

    private bool _hasCell;
    private Vector2Int _cell;

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
    }

    public bool TryGetCell(out Vector2Int cell)
    {
        UpdateCellFromAnchor();
        cell = _cell;
        return _hasCell;
    }

    public bool TryCollect(GameObject collector)
    {
        if (itemDefinition == null)
        {
            return false;
        }

        InventoryHudController inventory = null;
        if (collector != null)
        {
            inventory = collector.GetComponentInChildren<InventoryHudController>(true);
            if (inventory == null)
            {
                inventory = collector.GetComponentInParent<InventoryHudController>();
            }
        }

        if (inventory == null)
        {
            inventory = FindFirstObjectByType<InventoryHudController>();
        }

        if (inventory == null || !inventory.TryAddItem(itemDefinition))
        {
            return false;
        }

        Destroy(gameObject);
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
