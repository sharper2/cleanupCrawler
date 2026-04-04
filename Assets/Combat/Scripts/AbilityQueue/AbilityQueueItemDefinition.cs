using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "AbilityQueueItem", menuName = "Combat/Ability Queue Item")]
    public class AbilityQueueItemDefinition : ScriptableObject, IAbilityQueueItem
    {
        [SerializeField] private Sprite queueSprite;
        [SerializeField] private string displayName = "Orb";
        [SerializeField] private AbilityQueuePassiveSchedule passiveSchedule;

        public Sprite QueueSprite => queueSprite;
        public string DisplayName => displayName;
        public AbilityQueuePassiveSchedule PassiveSchedule => passiveSchedule;

        public virtual void OnPassiveProcced(AbilityQueueContext context)
        {
        }

        public virtual void OnEvoked(AbilityQueueContext context)
        {
        }
    }
}
