using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "TestManaOrb", menuName = "Combat/Test Mana Orb Ability")]
    public class TestManaOrbAbilityQueueItem : AbilityQueueItemDefinition
    {
        [SerializeField, Min(0f)] private float passiveManaRestore = 2f;
        [SerializeField, Min(0f)] private float evokeManaRestore = 25f;

        public override void OnPassiveProcced(AbilityQueueContext context)
        {
            if (context?.Player == null)
            {
                return;
            }

            var mana = context.Player.GetComponent<ManaComponent>();
            if (mana != null && passiveManaRestore > 0f)
            {
                mana.Restore(passiveManaRestore);
            }

            Debug.Log($"[TestManaOrb] Passive (+{passiveManaRestore} mana)", context.Player);
        }

        public override void OnEvoked(AbilityQueueContext context)
        {
            if (context?.Player == null)
            {
                return;
            }

            var mana = context.Player.GetComponent<ManaComponent>();
            if (mana != null && evokeManaRestore > 0f)
            {
                mana.Restore(evokeManaRestore);
            }

            Debug.Log($"[TestManaOrb] Evoked (+{evokeManaRestore} mana)", context.Player);
        }
    }
}
