using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "WeaponItem", menuName = "Combat/Weapon Item")]
    public class WeaponItemDefinition : EquippableItemDefinition
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private float range = 2f;
        [SerializeField] private float attackActiveDuration = 0.12f;
        [SerializeField] private float cooldown = 0.35f;

        public float Damage => Mathf.Max(0f, damage);
        public float Range => Mathf.Max(0.1f, range);
        public float AttackActiveDuration => Mathf.Max(0.01f, attackActiveDuration);
        public float Cooldown => Mathf.Max(0.01f, cooldown);
        public override EquipmentSlotType SlotType => EquipmentSlotType.Weapon;

        public override bool TryGetDamage(out float value)
        {
            value = Damage;
            return true;
        }

        public virtual bool ExecuteAttack(CombatAttackController attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            return attacker.PerformThrustAttack(this);
        }
    }
}
