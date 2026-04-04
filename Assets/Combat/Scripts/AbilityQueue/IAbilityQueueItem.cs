using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// A slot in the ability queue (Defect-style). Left = back, right = front (next evoke).
    /// </summary>
    public interface IAbilityQueueItem
    {
        Sprite QueueSprite { get; }
        string DisplayName { get; }
        AbilityQueuePassiveSchedule PassiveSchedule { get; }

        /// <summary>Called when this item's passive triggers (timer or move count).</summary>
        void OnPassiveProcced(AbilityQueueContext context);

        /// <summary>Called when the player evokes while this item is at the front (rightmost).</summary>
        void OnEvoked(AbilityQueueContext context);
    }
}
