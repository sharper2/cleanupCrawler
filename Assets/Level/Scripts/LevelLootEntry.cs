using System;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    [Serializable]
    public class LevelLootEntry
    {
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField, Min(1)] private int weight = 1;

        public InventoryItemDefinition Item => item;
        public int Weight => Mathf.Max(1, weight);
    }
}
