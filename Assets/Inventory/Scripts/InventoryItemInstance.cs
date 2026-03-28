using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryItemInstance : IInventoryItem
{
    [SerializeField] private InventoryItemDefinition definition;
    [SerializeField, Range(0, 3)] private int rotationSteps;

    public InventoryItemInstance(InventoryItemDefinition definition)
    {
        this.definition = definition;
        rotationSteps = 0;
    }

    public InventoryItemDefinition Definition => definition;

    public int RotationSteps
    {
        get => rotationSteps;
        set => rotationSteps = NormalizeRotation(value);
    }

    public IReadOnlyList<Vector2Int> GetOccupiedOffsets()
    {
        return definition == null
            ? Array.Empty<Vector2Int>()
            : definition.GetOccupiedOffsets(rotationSteps);
    }

    public void RotateClockwise()
    {
        RotationSteps++;
    }

    private static int NormalizeRotation(int value)
    {
        var normalized = value % 4;
        return normalized < 0 ? normalized + 4 : normalized;
    }
}
