using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "MobilityItem", menuName = "Combat/Equippable Item/Mobility")]
    public class MobilityItemDefinition : EquippableItemDefinition
    {
        public override EquipmentSlotType SlotType => EquipmentSlotType.Mobility;

        public override bool TryActivate(GameObject owner)
        {
            return ExecuteMobility(owner);
        }

        public virtual bool ExecuteMobility(GameObject owner)
        {
            return false;
        }

        public override IReadOnlyList<string> GetAttributeDescriptions()
        {
            return Array.Empty<string>();
        }
    }
}
