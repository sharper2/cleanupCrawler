using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerEquipment : MonoBehaviour
    {
        [SerializeField] private EquippableItemDefinition equippedItem;

        public EquippableItemDefinition EquippedItem => equippedItem;
        public WeaponItemDefinition EquippedWeapon => equippedItem as WeaponItemDefinition;

        public void Equip(EquippableItemDefinition item)
        {
            equippedItem = item;
        }

        public void Unequip()
        {
            equippedItem = null;
        }
    }
}
