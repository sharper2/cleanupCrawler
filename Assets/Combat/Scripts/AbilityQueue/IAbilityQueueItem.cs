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

    /// <summary>
    /// While this orb is in the queue, <see cref="AbilityQueueComponent"/> applies damage reduction to the player.
    /// </summary>
    public interface IAbilityQueueDamageReductionWhileQueued
    {
        /// <summary>0-100: percent of incoming damage ignored (additive across queued orbs, capped).</summary>
        float DamageReductionPercentWhileQueued { get; }
    }

    /// <summary>
    /// Optional per-orb settings for evoke linger on <see cref="AbilityQueueHud"/>.
    /// </summary>
    public interface IAbilityQueueEvokeLingerBehavior
    {
        /// <summary>How long the evoked orb stays on the HUD (unscaled seconds).</summary>
        float EvokeLingerDurationSeconds { get; }

        /// <summary>If true, the linger orb stays at the evoke column instead of sliding off to the right.</summary>
        bool EvokeLingerStayAtEvokeSlot { get; }

        /// <summary>
        /// If true, the linger orb stays in the column immediately to the right of the HUD bar (visual index <c>maxSlots</c>),
        /// so it does not overlap queued orbs. Takes precedence over <see cref="EvokeLingerStayAtEvokeSlot"/>.
        /// </summary>
        bool EvokeLingerStayRightOfHud { get; }
    }
}
