using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerCombatController : MonoBehaviour
    {
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private CombatAttackController attackController;
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;

        private void Awake()
        {
            if (equipment == null)
            {
                equipment = GetComponent<PlayerEquipment>();
            }

            if (attackController == null)
            {
                attackController = GetComponent<CombatAttackController>();
            }
        }

        private void Update()
        {
            if (!Input.GetKeyDown(attackKey))
            {
                return;
            }

            var weapon = equipment != null ? equipment.EquippedWeapon : null;
            if (weapon == null)
            {
                return;
            }

            if (attackController == null)
            {
                return;
            }

            attackController.TryExecuteAttack(weapon);
        }
    }
}
