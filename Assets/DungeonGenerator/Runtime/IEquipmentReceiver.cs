using UnityEngine;

namespace DungeonGenerator
{
    public interface IEquipmentReceiver
    {
        bool TryEquipObject(Object itemAsset);
        bool TryUnequipObject(Object itemAsset);
        bool IsEquippedObject(Object itemAsset);
    }
}
