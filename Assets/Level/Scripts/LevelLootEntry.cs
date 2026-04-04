using System;
using DungeonGenerator;
using UnityEngine;

namespace CleanupCrawler.Levels
{
    public enum LevelLootKind
    {
        InventoryItem = 0,
        AbilityOrb = 1
    }

    [Serializable]
    public class LevelLootEntry
    {
        [SerializeField] private LevelLootKind kind = LevelLootKind.InventoryItem;
        [SerializeField] private InventoryItemDefinition item;
        [SerializeField] private AbilityQueueItemDefinition abilityOrb;
        [SerializeField, Min(1)] private int weight = 1;

        public LevelLootKind Kind => kind;
        public InventoryItemDefinition Item => item;
        public AbilityQueueItemDefinition AbilityOrb => abilityOrb;
        public int Weight => Mathf.Max(1, weight);

        public bool HasValidLoot()
        {
            return kind switch
            {
                LevelLootKind.InventoryItem => item != null,
                LevelLootKind.AbilityOrb => abilityOrb != null,
                _ => false
            };
        }

        public string ResolvePickupDisplayName()
        {
            return kind switch
            {
                LevelLootKind.InventoryItem when item != null => item.DisplayName,
                LevelLootKind.AbilityOrb when abilityOrb != null => abilityOrb.DisplayName,
                _ => "Loot"
            };
        }
    }
}
