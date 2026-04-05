using UnityEngine;

namespace DungeonGenerator
{
    public sealed class AbilityQueueContext
    {
        public AbilityQueueContext(GameObject player, AbilityQueueComponent queue, OrbAbilityVisualFeedback orbVisuals)
        {
            Player = player;
            Queue = queue;
            OrbVisuals = orbVisuals;
        }

        public GameObject Player { get; }
        public AbilityQueueComponent Queue { get; }

        /// <summary>Optional shared VFX hook (one component on the player for all orb types).</summary>
        public OrbAbilityVisualFeedback OrbVisuals { get; }

        /// <summary>
        /// Bonus damage accumulated from player attacks while this orb was queued (only set during <see cref="IAbilityQueueItem.OnEvoked"/>).
        /// </summary>
        public float EvokeAttackStackDamage { get; internal set; }
    }
}
