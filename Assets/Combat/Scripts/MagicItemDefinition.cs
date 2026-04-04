using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "MagicItem", menuName = "Combat/Equippable Item/Magic")]
    public class MagicItemDefinition : EquippableItemDefinition
    {
        [SerializeField, Min(0)] private int manaCost;

        public override EquipmentSlotType SlotType => EquipmentSlotType.Magic;
        public int ManaCost => Mathf.Max(0, manaCost);

        public override bool TryActivate(GameObject owner)
        {
            if (owner == null)
            {
                return false;
            }

            if (ManaCost > 0)
            {
                var mana = owner.GetComponent<ManaComponent>()
                         ?? owner.GetComponentInParent<ManaComponent>()
                         ?? owner.GetComponentInChildren<ManaComponent>(true);

                if (mana == null || !mana.TrySpend(ManaCost))
                {
                    return false;
                }
            }

            return ExecuteMagic(owner);
        }

        public virtual bool ExecuteMagic(GameObject owner)
        {
            return false;
        }

        public override IReadOnlyList<string> GetAttributeDescriptions()
        {
            return new[] { $"Mana Cost: {ManaCost}" };
        }
    }
}
