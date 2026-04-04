using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerCombatController : MonoBehaviour
    {
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private CombatAttackController attackController;
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        [SerializeField] private KeyCode magicActivationKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode mobilityActivationKey = KeyCode.LeftControl;

        private void Awake()
        {
            if (equipment == null)
            {
                equipment = GetComponent<PlayerEquipment>();
            }

            if (GetComponent<ManaComponent>() == null)
            {
                gameObject.AddComponent<ManaComponent>();
            }

            if (attackController == null)
            {
                attackController = GetComponent<CombatAttackController>();
            }
        }

        private void Update()
        {
            if (equipment != null)
            {
                if (Input.GetKeyDown(magicActivationKey))
                {
                    equipment.TryActivateSlot(EquipmentSlotType.Magic, gameObject);
                }

                if (Input.GetKeyDown(mobilityActivationKey))
                {
                    equipment.TryActivateSlot(EquipmentSlotType.Mobility, gameObject);
                }
            }

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
