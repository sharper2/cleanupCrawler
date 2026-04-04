using System;
using UnityEngine;

namespace DungeonGenerator
{
    [Serializable]
    public struct AbilityQueuePassiveSchedule
    {
        [SerializeField] private AbilityQueuePassiveKind kind;
        [SerializeField, Min(0.01f)] private float intervalSeconds;
        [SerializeField, Min(1)] private int playerMoves;

        public AbilityQueuePassiveKind Kind => kind;
        public float IntervalSeconds => Mathf.Max(0.01f, intervalSeconds);
        public int PlayerMoves => Mathf.Max(1, playerMoves);

        public static AbilityQueuePassiveSchedule None => default;

        public AbilityQueuePassiveSchedule(AbilityQueuePassiveKind passiveKind, float seconds, int moves)
        {
            kind = passiveKind;
            intervalSeconds = Mathf.Max(0.01f, seconds);
            playerMoves = Mathf.Max(1, moves);
        }

        public static AbilityQueuePassiveSchedule EverySeconds(float seconds)
        {
            return new AbilityQueuePassiveSchedule(AbilityQueuePassiveKind.IntervalSeconds, seconds, 1);
        }

        public static AbilityQueuePassiveSchedule EveryNPlayerMoves(int moves)
        {
            return new AbilityQueuePassiveSchedule(AbilityQueuePassiveKind.EveryNPlayerMoves, 0.01f, moves);
        }
    }
}
