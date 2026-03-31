using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "SwordWeaponItem", menuName = "Combat/Weapon Item/Sword")]
    public class SwordWeaponItemDefinition : WeaponItemDefinition
    {
        [SerializeField] private float swipeWidth = 1.2f;

        public float SwipeWidth => Mathf.Max(0.1f, swipeWidth);

        public override bool ExecuteAttack(CombatAttackController attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            return attacker.PerformSwordSwipeAttack(this, SwipeWidth);
        }
    }
}
