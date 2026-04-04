using UnityEngine;

namespace DungeonGenerator
{
    public class EquippableItemDefinition : InventoryItemDefinition
    {
        [SerializeField] private EquipmentSlotType slotType = EquipmentSlotType.Weapon;
        [SerializeField, Min(0f)] private float activationCooldown = 0.25f;

        public virtual EquipmentSlotType SlotType => slotType;
        public float ActivationCooldown => Mathf.Max(0f, activationCooldown);

        public override bool TryGetEquipmentSlot(out EquipmentSlotType slot)
        {
            slot = SlotType;
            return true;
        }

        public virtual bool TryActivate(GameObject owner)
        {
            return false;
        }
    }
}
