using System.Collections.Generic;
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

    [Header("Debug Visual")]
    [SerializeField] private Color cellColor = new(0.2f, 0.2f, 0.2f, 0.7f);
    [SerializeField] private Color cellBorderColor = new(0f, 0f, 0f, 0.9f);
    [SerializeField] private Color itemColor = new(0.2f, 0.55f, 0.95f, 0.75f);
    [SerializeField] private Color validDropColor = new(0.2f, 0.9f, 0.3f, 0.5f);
    [SerializeField] private Color invalidDropColor = new(0.95f, 0.2f, 0.2f, 0.5f);

    [Header("Scene Gizmo")]
    [SerializeField] private bool drawSceneGizmo = true;
    [SerializeField] private float gizmoCellSize = 0.25f;

    private InventoryGridData _grid;
    private bool _isVisible = true;

    private InventoryItemInstance _draggedItem;
    private Vector2Int _draggedFromOrigin;
    private bool _dragHasPreviousOrigin;
    private Vector2Int _dragMouseToOriginCellOffset;
    private Vector2 _dragMouseToOriginPixelOffset;
    private bool _cursorWasVisibleBeforeDrag;

    private bool _isHoveringGrid;
    private Vector2Int _hoveredOrigin;
    private bool _canDropAtHover;

    private Rect GridRect => new(panelPosition.x, panelPosition.y, gridSize.x * cellPixelSize, gridSize.y * cellPixelSize);

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

        if (_draggedItem != null)
        {
            DrawDragPreview();
        }
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

        var placed = _isHoveringGrid && _canDropAtHover && _grid.TryPlaceItem(_draggedItem, _hoveredOrigin);
        if (!placed && _dragHasPreviousOrigin)
        {
            _grid.TryPlaceItem(_draggedItem, _draggedFromOrigin);
        }

        Cursor.visible = _cursorWasVisibleBeforeDrag;
        _draggedItem = null;
        _dragHasPreviousOrigin = false;
        _isHoveringGrid = false;
        _canDropAtHover = false;
    }

    private void CancelDragToPreviousPosition()
    {
        if (_draggedItem == null)
        {
            return;
        }

        if (_dragHasPreviousOrigin)
        {
            _grid.TryPlaceItem(_draggedItem, _draggedFromOrigin);
        }

        Cursor.visible = _cursorWasVisibleBeforeDrag;
        _draggedItem = null;
        _dragHasPreviousOrigin = false;
        _isHoveringGrid = false;
        _canDropAtHover = false;
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

            DrawItemCells(item, origin, itemColor);
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
