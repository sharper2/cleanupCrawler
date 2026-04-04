using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "AbilityQueueItem", menuName = "Combat/Ability Queue Item")]
    public class AbilityQueueItemDefinition : ScriptableObject, IAbilityQueueItem
    {
        [SerializeField] private Sprite queueSprite;
        [SerializeField] private string displayName = "Orb";
        [SerializeField] private AbilityQueuePassiveSchedule passiveSchedule;
        [Tooltip("Optional world model for FloorAbilityQueuePickup; if unset, the pickup has no mesh.")]
        [SerializeField] private GameObject floorPickupModelPrefab;

        public Sprite QueueSprite => queueSprite;
        public string DisplayName => displayName;
        public virtual AbilityQueuePassiveSchedule PassiveSchedule => passiveSchedule;
        public GameObject FloorPickupModelPrefab => floorPickupModelPrefab;

        public virtual void OnPassiveProcced(AbilityQueueContext context)
        {
        }

        public virtual void OnEvoked(AbilityQueueContext context)
        {
        }
    }
}
