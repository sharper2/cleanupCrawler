using UnityEngine;

namespace DungeonGenerator
{
    public sealed class AbilityQueueContext
    {
        public AbilityQueueContext(GameObject player, AbilityQueueComponent queue)
        {
            Player = player;
            Queue = queue;
        }

        public GameObject Player { get; }
        public AbilityQueueComponent Queue { get; }
    }
}
