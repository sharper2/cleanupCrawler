using System;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    [Serializable]
    public class LevelDecorationEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int weight = 1;
        [SerializeField] private bool applyPrefabTransformOffsets = true;
        [SerializeField] private Vector3 positionOffset;
        [SerializeField] private Vector3 rotationOffsetEuler;

        public GameObject Prefab => prefab;
        public int Weight => Mathf.Max(1, weight);
        public bool ApplyPrefabTransformOffsets => applyPrefabTransformOffsets;
        public Vector3 PositionOffset => positionOffset;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
    }
}
