using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerEquipment : MonoBehaviour, IEquipmentReceiver
    {
        [SerializeField] private WeaponItemDefinition equippedWeapon;
        [SerializeField] private EquippableItemDefinition equippedMagicItem;
        [SerializeField] private EquippableItemDefinition equippedMobilityItem;
        [SerializeField] private Transform heldItemAnchor;

        private GameObject _heldItemInstance;
        private float _nextMagicActivationTime;
        private float _nextMobilityActivationTime;

        public WeaponItemDefinition EquippedWeapon => equippedWeapon;
        public EquippableItemDefinition EquippedMagicItem => equippedMagicItem;
        public EquippableItemDefinition EquippedMobilityItem => equippedMobilityItem;

        public void Equip(EquippableItemDefinition item)
        {
            if (item == null)
            {
                return;
            }

            switch (item.SlotType)
            {
                case EquipmentSlotType.Weapon:
                    equippedWeapon = item as WeaponItemDefinition;
                    RefreshHeldItemVisual();
                    break;
                case EquipmentSlotType.Magic:
                    equippedMagicItem = item;
                    break;
                case EquipmentSlotType.Mobility:
                    equippedMobilityItem = item;
                    break;
            }
        }

        public void Unequip(EquipmentSlotType slot)
        {
            switch (slot)
            {
                case EquipmentSlotType.Weapon:
                    equippedWeapon = null;
                    RefreshHeldItemVisual();
                    break;
                case EquipmentSlotType.Magic:
                    equippedMagicItem = null;
                    break;
                case EquipmentSlotType.Mobility:
                    equippedMobilityItem = null;
                    break;
            }
        }

        public bool TryActivateSlot(EquipmentSlotType slot, GameObject owner)
        {
            var item = GetItemInSlot(slot);
            if (item == null)
            {
                return false;
            }

            if (Time.time < GetNextActivationTime(slot))
            {
                return false;
            }

            if (!item.TryActivate(owner))
            {
                return false;
            }

            SetNextActivationTime(slot, Time.time + item.ActivationCooldown);
            return true;
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

        private float GetNextActivationTime(EquipmentSlotType slot)
        {
            return slot switch
            {
                EquipmentSlotType.Magic => _nextMagicActivationTime,
                EquipmentSlotType.Mobility => _nextMobilityActivationTime,
                _ => 0f
            };
        }

        private void SetNextActivationTime(EquipmentSlotType slot, float time)
        {
            switch (slot)
            {
                case EquipmentSlotType.Magic:
                    _nextMagicActivationTime = time;
                    break;
                case EquipmentSlotType.Mobility:
                    _nextMobilityActivationTime = time;
                    break;
            }
        }

        public bool TryUnequipObject(Object itemAsset)
        {
            return TryUnequipObjectFromSlot(itemAsset, EquipmentSlotType.Weapon)
                || TryUnequipObjectFromSlot(itemAsset, EquipmentSlotType.Magic)
                || TryUnequipObjectFromSlot(itemAsset, EquipmentSlotType.Mobility);
        }

        public bool IsEquippedObject(Object itemAsset)
        {
            return IsObjectInSlot(itemAsset, EquipmentSlotType.Weapon)
                || IsObjectInSlot(itemAsset, EquipmentSlotType.Magic)
                || IsObjectInSlot(itemAsset, EquipmentSlotType.Mobility);
        }

        private EquippableItemDefinition GetItemInSlot(EquipmentSlotType slot)
        {
            return slot switch
            {
                EquipmentSlotType.Weapon => equippedWeapon,
                EquipmentSlotType.Magic => equippedMagicItem,
                EquipmentSlotType.Mobility => equippedMobilityItem,
                _ => null
            };
        }

        private bool IsObjectInSlot(Object itemAsset, EquipmentSlotType slot)
        {
            return itemAsset is EquippableItemDefinition equippable && ReferenceEquals(GetItemInSlot(slot), equippable);
        }

        private bool TryUnequipObjectFromSlot(Object itemAsset, EquipmentSlotType slot)
        {
            if (!IsObjectInSlot(itemAsset, slot))
            {
                return false;
            }

            Unequip(slot);
            return true;
        }

        private void OnDisable()
        {
            if (_heldItemInstance != null)
            {
                Destroy(_heldItemInstance);
                _heldItemInstance = null;
            }
        }

        private void RefreshHeldItemVisual()
        {
            if (_heldItemInstance != null)
            {
                Destroy(_heldItemInstance);
                _heldItemInstance = null;
            }

            var heldPrefab = equippedWeapon != null ? equippedWeapon.HeldModelPrefab : null;
            if (heldPrefab == null)
            {
                return;
            }

            var parent = heldItemAnchor != null ? heldItemAnchor : transform;
            _heldItemInstance = Instantiate(heldPrefab, parent);
            _heldItemInstance.transform.localPosition = Vector3.zero;
            _heldItemInstance.transform.localRotation = Quaternion.identity;
            _heldItemInstance.transform.localScale = Vector3.one;
        }
    }
}
