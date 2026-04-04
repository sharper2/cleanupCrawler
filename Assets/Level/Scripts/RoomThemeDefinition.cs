using System;
using System.Collections.Generic;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    [CreateAssetMenu(fileName = "RoomTheme", menuName = "Level/Room Theme")]
    public class RoomThemeDefinition : ScriptableObject
    {
        [SerializeField] private List<ThemeDecorationEntry> lowWallDecorations = new();
        [SerializeField] private List<ThemeDecorationEntry> highWallDecorations = new();
        [SerializeField] private List<ThemeDecorationEntry> ceilingDecorations = new();
        [SerializeField] private List<ThemeDecorationEntry> floorDecorations = new();

        public IReadOnlyList<ThemeDecorationEntry> LowWallDecorations => lowWallDecorations;
        public IReadOnlyList<ThemeDecorationEntry> HighWallDecorations => highWallDecorations;
        public IReadOnlyList<ThemeDecorationEntry> CeilingDecorations => ceilingDecorations;
        public IReadOnlyList<ThemeDecorationEntry> FloorDecorations => floorDecorations;
    }

    [Serializable]
    public class ThemeDecorationEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int minPerRoom = 1;
        [SerializeField, Min(0)] private int maxPerRoom = 1;
        [Tooltip("When > 0, floor/ceiling spawn counts scale with room size: count ≈ min/max × (room cells ÷ this value). When 0, min/max are used as a flat range per room.")]
        [SerializeField, Min(0)] private int referenceFloorCellCount = 0;
        [Tooltip("When > 0, wall spawn counts scale with available outer-wall anchors: count ≈ min/max × (wall anchors ÷ this value). When 0, uses reference floor cell count if set; otherwise flat min/max.")]
        [SerializeField, Min(0)] private int referenceWallAnchorCount = 0;
        [SerializeField] private bool applyPrefabTransformOffsets = true;
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffsetEuler;

        public GameObject Prefab => prefab;
        public int MinPerRoom => Mathf.Max(0, minPerRoom);
        public int MaxPerRoom => Mathf.Max(MinPerRoom, maxPerRoom);
        public int ReferenceFloorCellCount => Mathf.Max(0, referenceFloorCellCount);
        public int ReferenceWallAnchorCount => Mathf.Max(0, referenceWallAnchorCount);
        public bool ApplyPrefabTransformOffsets => applyPrefabTransformOffsets;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
    }

    [Serializable]
    public class WeightedRoomTheme
    {
        [SerializeField] private RoomThemeDefinition theme;
        [SerializeField, Min(1)] private int weight = 1;

        public RoomThemeDefinition Theme => theme;
        public int Weight => Mathf.Max(1, weight);
    }
}
