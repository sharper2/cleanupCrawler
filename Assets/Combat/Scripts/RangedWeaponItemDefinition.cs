using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "RangedWeaponItem", menuName = "Combat/Weapon Item/Ranged")]
    public class RangedWeaponItemDefinition : WeaponItemDefinition
    {
        [SerializeField] private CombatProjectile projectilePrefab;
        [SerializeField] private float projectileSpeed = 14f;
        [SerializeField] private int ammoPerShot = 1;
        [SerializeField] private InventoryItemDefinition requiredAmmo;

        public CombatProjectile ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public int AmmoPerShot => Mathf.Max(1, ammoPerShot);
        public InventoryItemDefinition RequiredAmmo => requiredAmmo;

        public override bool ExecuteAttack(CombatAttackController attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            return attacker.PerformProjectileAttack(this);
        }
    }
}
