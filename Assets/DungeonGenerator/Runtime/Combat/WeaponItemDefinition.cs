using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "WeaponItem", menuName = "Combat/Weapon Item")]
    public class WeaponItemDefinition : EquippableItemDefinition
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private float range = 2f;
        [SerializeField] private float cooldown = 0.35f;

        public float Damage => Mathf.Max(0f, damage);
        public float Range => Mathf.Max(0.1f, range);
        public float Cooldown => Mathf.Max(0.01f, cooldown);
    }
}
