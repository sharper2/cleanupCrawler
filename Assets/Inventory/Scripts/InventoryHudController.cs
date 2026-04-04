using System.Collections.Generic;
using DungeonGenerator;
using UnityEngine;

public class InventoryHudController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 panelPosition = new(24f, 24f);
    [SerializeField] private Vector2Int gridSize = new(10, 10);
    [SerializeField] private float cellPixelSize = 36f;

    [Header("Behavior")]
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private KeyCode rotateKey = KeyCode.R;
    [SerializeField] private List<InventoryItemDefinition> startingItems = new();

    [Header("Progress")]
    [SerializeField] private int levelQuota = 100;

    [Header("Debug Visual")]
    [SerializeField] private Color cellColor = new(0.2f, 0.2f, 0.2f, 0.7f);
    [SerializeField] private Color cellBorderColor = new(0f, 0f, 0f, 0.9f);
    [SerializeField] private Color itemColor = new(0.2f, 0.55f, 0.95f, 0.75f);
    [SerializeField] private Color equippedItemColor = new(0.95f, 0.8f, 0.2f, 0.85f);
    [SerializeField] private Color validDropColor = new(0.2f, 0.9f, 0.3f, 0.5f);
    [SerializeField] private Color invalidDropColor = new(0.95f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Vector2 deleteZoneSize = new(160f, 42f);
    [SerializeField] private float deleteZoneTopMargin = 10f;
    [SerializeField] private Color deleteZoneColor = new(0.45f, 0.12f, 0.12f, 0.8f);
    [SerializeField] private Color deleteZoneHoverColor = new(0.8f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Vector2 tooltipOffset = new(18f, 18f);
    [SerializeField] private Color tooltipBackgroundColor = new(0f, 0f, 0f, 0.9f);
    [SerializeField] private Vector2 equipmentSlotSize = new(170f, 52f);
    [SerializeField] private float equipmentSlotMargin = 16f;
    [SerializeField] private float equipmentSlotGap = 8f;
    [SerializeField] private Color equipmentSlotColor = new(0.12f, 0.12f, 0.12f, 0.85f);
    [SerializeField] private Color equipmentSlotBorderColor = new(0.45f, 0.45f, 0.45f, 0.9f);

    [Header("Scene Gizmo")]
    [SerializeField] private bool drawSceneGizmo = true;
    [SerializeField] private float gizmoCellSize = 0.25f;

    private InventoryGridData _grid;
    private bool _isVisible = true;

    private InventoryItemInstance _draggedItem;
    private Vector2Int _draggedFromOrigin;
    private int _draggedFromRotationSteps;
    private bool _dragHasPreviousOrigin;
    private Vector2Int _dragMouseToOriginCellOffset;
    private Vector2 _dragMouseToOriginPixelOffset;
    private bool _cursorWasVisibleBeforeDrag;

    private bool _isHoveringGrid;
    private Vector2Int _hoveredOrigin;
    private bool _canDropAtHover;
    private InventoryItemInstance _lastClickedItem;
    private InventoryItemInstance _equippedWeaponItem;
    private InventoryItemInstance _equippedMagicItem;
    private InventoryItemInstance _equippedMobilityItem;
    private float _lastClickTime = -999f;

    private const float DoubleClickThresholdSeconds = 0.3f;

    private Rect GridRect => new(panelPosition.x, panelPosition.y, gridSize.x * cellPixelSize, gridSize.y * cellPixelSize);
    private Rect DeleteZoneRect => new(
        GridRect.xMax - deleteZoneSize.x,
        GridRect.yMax + deleteZoneTopMargin,
        deleteZoneSize.x,
        deleteZoneSize.y);

    private int _score;

    public int Score => _score;
    public int LevelQuota => Mathf.Max(0, levelQuota);
    public bool CanCompleteLevel => Score >= LevelQuota;

    public void SetLevelQuota(int quota)
    {
        levelQuota = Mathf.Max(0, quota);
    }

    public void ResetScore()
    {
        _score = 0;
    }

    public void ClearAllItems()
    {
        EnsureGridInitialized();

        TryClearEquippedItem(ref _equippedWeaponItem);
        TryClearEquippedItem(ref _equippedMagicItem);
        TryClearEquippedItem(ref _equippedMobilityItem);

        var snapshot = new List<InventoryItemInstance>(_grid.Items);
        for (var i = 0; i < snapshot.Count; i++)
        {
            _grid.RemoveItem(snapshot[i]);
        }
    }

    private void Awake()
    {
        EnsureGridInitialized();

        for (var i = 0; i < startingItems.Count; i++)
        {
            TryAddItem(startingItems[i]);
        }
    }

    public bool TryAddItem(InventoryItemDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        EnsureGridInitialized();

        var instance = new InventoryItemInstance(definition);
        if (!_grid.TryFindFirstFit(instance, out var origin))
        {
            return false;
        }

        return _grid.TryPlaceItem(instance, origin);
    }

    public bool TryConsumeItem(InventoryItemDefinition definition, int amount = 1)
    {
        if (definition == null || amount <= 0)
        {
            return false;
        }

        EnsureGridInitialized();

        var matchingItems = new List<InventoryItemInstance>(amount);
        var items = _grid.Items;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item != null && ReferenceEquals(item.Definition, definition))
            {
                matchingItems.Add(item);
                if (matchingItems.Count >= amount)
                {
                    break;
                }
            }
        }

        if (matchingItems.Count < amount)
        {
            return false;
        }

        for (var i = 0; i < amount; i++)
        {
            TryClearMatchingEquippedItem(matchingItems[i]);

            _grid.RemoveItem(matchingItems[i]);
        }

        return true;
    }

    private void EnsureGridInitialized()
    {
        if (_grid != null)
        {
            return;
        }

        var width = Mathf.Max(1, gridSize.x);
        var height = Mathf.Max(1, gridSize.y);
        _grid = new InventoryGridData(width, height);
    }

    private void Update()
    {
        if (GetToggleKeyDown())
        {
            _isVisible = !_isVisible;

            if (!_isVisible && _draggedItem != null)
            {
                CancelDragToPreviousPosition();
            }
        }

        if (!_isVisible)
        {
            return;
        }

        if (_draggedItem != null && GetRotateKeyDown())
        {
            _draggedItem.RotateClockwise();
            UpdateHoveredPlacement();
        }

        if (GetPrimaryMouseDown())
        {
            if (_draggedItem == null && TryToggleEquipByDoubleClick())
            {
                return;
            }

            BeginDrag();
        }

        if (_draggedItem != null)
        {
            UpdateHoveredPlacement();

            if (GetPrimaryMouseUp())
            {
                CompleteDrag();
            }
        }
    }

    private void OnGUI()
    {
        if (!_isVisible || _grid == null)
        {
            return;
        }

        DrawGrid();
        DrawPlacedItems();
        DrawDeleteZone();
        DrawEquipmentSlots();
        DrawHoveredItemTooltip();

        if (_draggedItem != null)
        {
            DrawDragPreview();
        }
    }

    private void DrawHoveredItemTooltip()
    {
        if (_draggedItem != null)
        {
            return;
        }

        var mousePosition = GetGuiMousePosition();
        if (!GridRect.Contains(mousePosition))
        {
            return;
        }

        var hoveredItem = _grid.GetItemAt(GuiToCell(mousePosition));
        var definition = hoveredItem != null ? hoveredItem.Definition : null;
        if (definition == null)
        {
            return;
        }

        var lines = BuildTooltipLines(definition);
        if (lines.Count == 0)
        {
            return;
        }

        var style = GUI.skin.label;
        var width = 120f;
        var lineHeight = style.lineHeight > 0f ? style.lineHeight : 16f;

        for (var i = 0; i < lines.Count; i++)
        {
            var size = style.CalcSize(new GUIContent(lines[i]));
            if (size.x > width)
            {
                width = size.x;
            }
        }

        var padding = 8f;
        var tooltipRect = new Rect(
            mousePosition.x + tooltipOffset.x,
            mousePosition.y + tooltipOffset.y,
            width + padding * 2f,
            lineHeight * lines.Count + padding * 2f);

        if (tooltipRect.xMax > Screen.width)
        {
            tooltipRect.x = mousePosition.x - tooltipRect.width - tooltipOffset.x;
        }

        if (tooltipRect.yMax > Screen.height)
        {
            tooltipRect.y = mousePosition.y - tooltipRect.height - tooltipOffset.y;
        }

        DrawRect(tooltipRect, tooltipBackgroundColor);
        DrawBorder(tooltipRect, cellBorderColor, 1f);

        var lineRect = new Rect(tooltipRect.x + padding, tooltipRect.y + padding, tooltipRect.width - padding * 2f, lineHeight);
        for (var i = 0; i < lines.Count; i++)
        {
            GUI.Label(lineRect, lines[i]);
            lineRect.y += lineHeight;
        }
    }

    private static List<string> BuildTooltipLines(InventoryItemDefinition definition)
    {
        var lines = new List<string>();
        lines.Add(definition.DisplayName);
        lines.Add($"Value: {definition.Value}");

        if (definition.TryGetDamage(out var damage))
        {
            lines.Add($"Damage: {damage:0.##}");
        }

        var attributes = definition.GetAttributeDescriptions();
        if (attributes != null && attributes.Count > 0)
        {
            lines.Add("Attributes:");
            for (var i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (string.IsNullOrWhiteSpace(attribute))
                {
                    continue;
                }

                lines.Add($"- {attribute}");
            }
        }

        return lines;
    }

    private void BeginDrag()
    {
        var mousePosition = GetGuiMousePosition();
        if (!GridRect.Contains(mousePosition))
        {
            return;
        }

        var cell = GuiToCell(mousePosition);
        var selectedItem = _grid.GetItemAt(cell);
        if (selectedItem == null)
        {
            return;
        }

        _draggedItem = selectedItem;
        _dragHasPreviousOrigin = _grid.TryGetOrigin(selectedItem, out _draggedFromOrigin);
        _draggedFromRotationSteps = selectedItem.RotationSteps;

        var itemOriginCell = _dragHasPreviousOrigin ? _draggedFromOrigin : cell;
        _dragMouseToOriginCellOffset = cell - itemOriginCell;
        _dragMouseToOriginPixelOffset = CellToPixelPosition(itemOriginCell) - mousePosition;

        _cursorWasVisibleBeforeDrag = Cursor.visible;
        Cursor.visible = false;

        _grid.RemoveItem(selectedItem);

        UpdateHoveredPlacement();
    }

    private void CompleteDrag()
    {
        if (_draggedItem == null)
        {
            return;
        }

        var mousePosition = GetGuiMousePosition();
        var deleted = IsHoveringDeleteZone(mousePosition) && TryDeleteDraggedItem();
        var placed = _isHoveringGrid && _canDropAtHover && _grid.TryPlaceItem(_draggedItem, _hoveredOrigin);

        if (!deleted && !placed && _dragHasPreviousOrigin)
        {
            RestoreDraggedItemToPreviousPosition();
        }

        Cursor.visible = _cursorWasVisibleBeforeDrag;
        _draggedItem = null;
        _dragHasPreviousOrigin = false;
        _isHoveringGrid = false;
        _canDropAtHover = false;
    }

    private bool TryToggleEquipByDoubleClick()
    {
        var mousePosition = GetGuiMousePosition();
        if (!GridRect.Contains(mousePosition))
        {
            return false;
        }

        var cell = GuiToCell(mousePosition);
        var clickedItem = _grid.GetItemAt(cell);
        var clickedDefinition = clickedItem != null ? clickedItem.Definition : null;
        if (clickedDefinition == null || !clickedDefinition.TryGetEquipmentSlot(out var slot))
        {
            return false;
        }

        var isDoubleClick = ReferenceEquals(clickedItem, _lastClickedItem)
            && Time.unscaledTime - _lastClickTime <= DoubleClickThresholdSeconds;

        _lastClickedItem = clickedItem;
        _lastClickTime = Time.unscaledTime;

        if (!isDoubleClick)
        {
            return false;
        }

        ref var equippedSlotItem = ref GetEquippedSlotItemRef(slot);
        if (ReferenceEquals(clickedItem, equippedSlotItem))
        {
            if (!TryUnequipAsset(clickedDefinition))
            {
                return false;
            }

            equippedSlotItem = null;
            return true;
        }

        if (!TryEquipAsset(clickedDefinition))
        {
            return false;
        }

        equippedSlotItem = clickedItem;
        return true;
    }

    private static bool TryEquipAsset(Object itemAsset)
    {
        if (itemAsset == null)
        {
            return false;
        }

        var receivers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < receivers.Length; i++)
        {
            if (receivers[i] is IEquipmentReceiver receiver && receiver.TryEquipObject(itemAsset))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryUnequipAsset(Object itemAsset)
    {
        if (itemAsset == null)
        {
            return false;
        }

        var receivers = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < receivers.Length; i++)
        {
            if (receivers[i] is IEquipmentReceiver receiver && receiver.TryUnequipObject(itemAsset))
            {
                return true;
            }
        }

        return false;
    }

    private void CancelDragToPreviousPosition()
    {
        if (_draggedItem == null)
        {
            return;
        }

        if (_dragHasPreviousOrigin)
        {
            RestoreDraggedItemToPreviousPosition();
        }

        Cursor.visible = _cursorWasVisibleBeforeDrag;
        _draggedItem = null;
        _dragHasPreviousOrigin = false;
        _isHoveringGrid = false;
        _canDropAtHover = false;
    }

    private void RestoreDraggedItemToPreviousPosition()
    {
        if (_draggedItem == null)
        {
            return;
        }

        _draggedItem.RotationSteps = _draggedFromRotationSteps;
        _grid.TryPlaceItem(_draggedItem, _draggedFromOrigin);
    }

    private bool TryDeleteDraggedItem()
    {
        if (_draggedItem == null || _draggedItem.Definition == null)
        {
            return false;
        }

        TryClearMatchingEquippedItem(_draggedItem);

        _score += _draggedItem.Definition.Value;
        return true;
    }

    private bool IsHoveringDeleteZone(Vector2 guiMousePosition)
    {
        return DeleteZoneRect.Contains(guiMousePosition);
    }

    private void UpdateHoveredPlacement()
    {
        var mousePosition = GetGuiMousePosition();
        _isHoveringGrid = GridRect.Contains(mousePosition);
        if (!_isHoveringGrid)
        {
            _canDropAtHover = false;
            return;
        }

        _hoveredOrigin = GuiToCell(mousePosition) - _dragMouseToOriginCellOffset;
        _canDropAtHover = _grid.CanPlaceItem(_draggedItem, _hoveredOrigin);
    }

    private void DrawDeleteZone()
    {
        var hoveringDeleteZone = _draggedItem != null && IsHoveringDeleteZone(GetGuiMousePosition());
        var zoneColor = hoveringDeleteZone ? deleteZoneHoverColor : deleteZoneColor;

        DrawRect(DeleteZoneRect, zoneColor);
        DrawBorder(DeleteZoneRect, cellBorderColor, 1f);

        GUI.Label(DeleteZoneRect, "DELETE");

        var scoreRect = new Rect(GridRect.x, DeleteZoneRect.yMax + 4f, GridRect.width, 24f);
        GUI.Label(scoreRect, $"Score: {Score} / Quota: {LevelQuota} {(CanCompleteLevel ? "(Ready)" : string.Empty)}");
    }

    private void DrawEquipmentSlots()
    {
        DrawEquipmentSlot(EquipmentSlotType.Magic, GetLeftSlotRect(0), "Magic", "Shift");
        DrawEquipmentSlot(EquipmentSlotType.Mobility, GetLeftSlotRect(1), "Mobility", "Ctrl");
        DrawEquipmentSlot(EquipmentSlotType.Weapon, GetRightSlotRect(), "Weapon", "Space");
    }

    private void DrawEquipmentSlot(EquipmentSlotType slot, Rect rect, string label, string keyLabel)
    {
        DrawRect(rect, equipmentSlotColor);
        DrawBorder(rect, equipmentSlotBorderColor, 1f);

        var equippedItem = GetEquippedSlotItem(slot);
        var definition = equippedItem != null ? equippedItem.Definition : null;
        var itemName = definition != null ? definition.DisplayName : "(empty)";

        var iconSize = rect.height - 12f;
        var iconRect = new Rect(rect.x + 6f, rect.y + 6f, iconSize, iconSize);
        var textX = iconRect.xMax + 6f;
        var topRect = new Rect(textX, rect.y + 4f, rect.xMax - textX - 6f, 20f);
        var bottomRect = new Rect(textX, rect.y + 24f, rect.xMax - textX - 6f, 20f);

        if (definition != null && definition.Icon != null)
        {
            GUI.DrawTexture(iconRect, definition.Icon.texture, ScaleMode.ScaleToFit, true);
        }
        else
        {
            DrawRect(iconRect, new Color(0f, 0f, 0f, 0.35f));
            DrawBorder(iconRect, equipmentSlotBorderColor, 1f);
        }

        GUI.Label(topRect, $"{label} [{keyLabel}]");
        GUI.Label(bottomRect, itemName);
    }

    private Rect GetLeftSlotRect(int index)
    {
        var x = equipmentSlotMargin;
        var y = Screen.height - equipmentSlotMargin - equipmentSlotSize.y * (2 - index) - equipmentSlotGap * (1 - index);
        return new Rect(x, y, equipmentSlotSize.x, equipmentSlotSize.y);
    }

    private Rect GetRightSlotRect()
    {
        var x = Screen.width - equipmentSlotMargin - equipmentSlotSize.x;
        var y = Screen.height - equipmentSlotMargin - equipmentSlotSize.y;
        return new Rect(x, y, equipmentSlotSize.x, equipmentSlotSize.y);
    }

    private bool IsEquippedInstance(InventoryItemInstance item)
    {
        return ReferenceEquals(item, _equippedWeaponItem)
            || ReferenceEquals(item, _equippedMagicItem)
            || ReferenceEquals(item, _equippedMobilityItem);
    }

    private InventoryItemInstance GetEquippedSlotItem(EquipmentSlotType slot)
    {
        return slot switch
        {
            EquipmentSlotType.Weapon => _equippedWeaponItem,
            EquipmentSlotType.Magic => _equippedMagicItem,
            EquipmentSlotType.Mobility => _equippedMobilityItem,
            _ => null
        };
    }

    private ref InventoryItemInstance GetEquippedSlotItemRef(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Weapon:
                return ref _equippedWeaponItem;
            case EquipmentSlotType.Magic:
                return ref _equippedMagicItem;
            default:
                return ref _equippedMobilityItem;
        }
    }

    private void TryClearMatchingEquippedItem(InventoryItemInstance item)
    {
        if (ReferenceEquals(item, _equippedWeaponItem))
        {
            TryClearEquippedItem(ref _equippedWeaponItem);
        }

        if (ReferenceEquals(item, _equippedMagicItem))
        {
            TryClearEquippedItem(ref _equippedMagicItem);
        }

        if (ReferenceEquals(item, _equippedMobilityItem))
        {
            TryClearEquippedItem(ref _equippedMobilityItem);
        }
    }

    private static void TryClearEquippedItem(ref InventoryItemInstance item)
    {
        if (item == null || item.Definition == null)
        {
            item = null;
            return;
        }

        TryUnequipAsset(item.Definition);
        item = null;
    }

    private void DrawGrid()
    {
        var rect = GridRect;
        DrawRect(rect, new Color(0f, 0f, 0f, 0.45f));

        for (var y = 0; y < _grid.Height; y++)
        {
            for (var x = 0; x < _grid.Width; x++)
            {
                var cellRect = CellToRect(new Vector2Int(x, y));
                DrawRect(cellRect, cellColor);
                DrawBorder(cellRect, cellBorderColor, 1f);
            }
        }
    }

    private void DrawPlacedItems()
    {
        var items = _grid.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!_grid.TryGetOrigin(item, out var origin))
            {
                continue;
            }

            var color = IsEquippedInstance(item) ? equippedItemColor : itemColor;
            DrawItemCells(item, origin, color);
        }
    }

    private void DrawDragPreview()
    {
        if (_isHoveringGrid)
        {
            DrawItemCells(_draggedItem, _hoveredOrigin, _canDropAtHover ? validDropColor : invalidDropColor);
            return;
        }

        var mousePosition = GetGuiMousePosition();
        var itemOriginPixel = mousePosition + _dragMouseToOriginPixelOffset;
        DrawItemCellsAtPixel(_draggedItem, itemOriginPixel, invalidDropColor);
    }

    private void DrawItemCells(IInventoryItem item, Vector2Int origin, Color color)
    {
        var offsets = item.GetOccupiedOffsets();
        for (var i = 0; i < offsets.Count; i++)
        {
            var cell = origin + offsets[i];
            var cellRect = CellToRect(cell);
            DrawRect(cellRect, color);
            DrawBorder(cellRect, cellBorderColor, 1f);
        }
    }

    private Rect CellToRect(Vector2Int cell)
    {
        return new Rect(
            GridRect.x + cell.x * cellPixelSize,
            GridRect.y + cell.y * cellPixelSize,
            cellPixelSize,
            cellPixelSize);
    }

    private void DrawItemCellsAtPixel(IInventoryItem item, Vector2 itemOriginPixel, Color color)
    {
        var offsets = item.GetOccupiedOffsets();
        for (var i = 0; i < offsets.Count; i++)
        {
            var cellPixel = itemOriginPixel + (Vector2)offsets[i] * cellPixelSize;
            var cellRect = new Rect(cellPixel.x, cellPixel.y, cellPixelSize, cellPixelSize);
            DrawRect(cellRect, color);
            DrawBorder(cellRect, cellBorderColor, 1f);
        }
    }

    private Vector2 CellToPixelPosition(Vector2Int cell)
    {
        return new Vector2(
            GridRect.x + cell.x * cellPixelSize,
            GridRect.y + cell.y * cellPixelSize);
    }

    private Vector2Int GuiToCell(Vector2 guiPosition)
    {
        var localX = guiPosition.x - GridRect.x;
        var localY = guiPosition.y - GridRect.y;

        return new Vector2Int(
            Mathf.FloorToInt(localX / cellPixelSize),
            Mathf.FloorToInt(localY / cellPixelSize));
    }

    private static void DrawRect(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    private static Vector2 GetGuiMousePosition()
    {
        var screenPosition = GetMouseScreenPosition();
        return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
    }

    private static Vector2 GetMouseScreenPosition()
    {
        return Input.mousePosition;
    }

    private bool GetPrimaryMouseDown()
    {
        return Input.GetMouseButtonDown(0);
    }

    private bool GetPrimaryMouseUp()
    {
        return Input.GetMouseButtonUp(0);
    }

    private bool GetToggleKeyDown()
    {
        return Input.GetKeyDown(toggleKey);
    }

    private bool GetRotateKeyDown()
    {
        return Input.GetKeyDown(rotateKey);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSceneGizmo)
        {
            return;
        }

        var width = Mathf.Max(1, gridSize.x);
        var height = Mathf.Max(1, gridSize.y);

        Gizmos.color = Color.cyan;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var center = transform.position + new Vector3(x * gizmoCellSize, -y * gizmoCellSize, 0f);
                Gizmos.DrawWireCube(center, new Vector3(gizmoCellSize, gizmoCellSize, 0.01f));
            }
            }
        }

    private void OnDisable()
    {
        if (_draggedItem != null)
        {
            Cursor.visible = _cursorWasVisibleBeforeDrag;
        }
    }
}
