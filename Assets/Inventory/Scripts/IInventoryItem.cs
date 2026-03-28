using System.Collections.Generic;
using UnityEngine;

public interface IInventoryItem
{
    InventoryItemDefinition Definition { get; }
    int RotationSteps { get; set; }
    IReadOnlyList<Vector2Int> GetOccupiedOffsets();
}
