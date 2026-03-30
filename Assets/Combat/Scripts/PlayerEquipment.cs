using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerEquipment : MonoBehaviour, IEquipmentReceiver
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

        public bool TryEquipObject(Object itemAsset)
        {
            if (itemAsset is EquippableItemDefinition equippable)
            {
                Equip(equippable);
                return true;
            }

            return false;
        }

        public bool TryUnequipObject(Object itemAsset)
        {
            if (itemAsset is EquippableItemDefinition equippable && ReferenceEquals(equippedItem, equippable))
            {
                Unequip();
                return true;
            }

            return false;
        }

        public bool IsEquippedObject(Object itemAsset)
        {
            return itemAsset is EquippableItemDefinition equippable && ReferenceEquals(equippedItem, equippable);
        }
    }
}
