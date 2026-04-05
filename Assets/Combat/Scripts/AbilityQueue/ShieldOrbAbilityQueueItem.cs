using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "ShieldOrb", menuName = "Combat/Shield Orb Ability")]
    public class ShieldOrbAbilityQueueItem : AbilityQueueItemDefinition, IAbilityQueueDamageReductionWhileQueued,
        IAbilityQueueEvokeLingerBehavior
    {
        [Header("While queued")]
        [SerializeField, Range(0f, 90f)] private float damageReductionPercentWhileQueued = 20f;

        [Header("Evoke")]
        [SerializeField, Min(0.01f)] private float evokeInvulnerabilitySeconds = 10f;

        public float DamageReductionPercentWhileQueued => damageReductionPercentWhileQueued;

        public float EvokeLingerDurationSeconds => evokeInvulnerabilitySeconds;

        public bool EvokeLingerStayAtEvokeSlot => false;

        public bool EvokeLingerStayRightOfHud => true;

        public override AbilityQueuePassiveSchedule PassiveSchedule => AbilityQueuePassiveSchedule.None;

        public override void OnEvoked(AbilityQueueContext context)
        {
            if (context.Queue == null)
            {
                return;
            }

            context.Queue.SetInvulnerableUntilUnscaled(Time.unscaledTime + evokeInvulnerabilitySeconds);
        }
    }
}
