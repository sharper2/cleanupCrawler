using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Snapshot of passive timing for HUD (time or move-based). Built from queue state via <see cref="Compute"/>.
    /// </summary>
    public readonly struct AbilityQueuePassiveHudState
    {
        public AbilityQueuePassiveKind Kind { get; }
        /// <summary>Seconds until next passive tick (interval passives).</summary>
        public float SecondsRemaining { get; }
        /// <summary>Grid moves until next passive tick (move passives).</summary>
        public int MovesRemaining { get; }

        private AbilityQueuePassiveHudState(AbilityQueuePassiveKind kind, float secondsRemaining, int movesRemaining)
        {
            Kind = kind;
            SecondsRemaining = secondsRemaining;
            MovesRemaining = movesRemaining;
        }

        public bool HasCountdown =>
            Kind == AbilityQueuePassiveKind.IntervalSeconds || Kind == AbilityQueuePassiveKind.EveryNPlayerMoves;

        /// <summary>
        /// Computes display state from queue entry fields (same values the queue uses when ticking passives).
        /// </summary>
        public static AbilityQueuePassiveHudState Compute(
            AbilityQueuePassiveSchedule schedule,
            float timeAccum,
            int moveAccumSinceProc)
        {
            switch (schedule.Kind)
            {
                case AbilityQueuePassiveKind.None:
                    return default;
                case AbilityQueuePassiveKind.IntervalSeconds:
                {
                    var interval = schedule.IntervalSeconds;
                    var rem = Mathf.Max(0f, interval - timeAccum);
                    return new AbilityQueuePassiveHudState(schedule.Kind, rem, 0);
                }
                case AbilityQueuePassiveKind.EveryNPlayerMoves:
                {
                    var moves = Mathf.Max(0, schedule.PlayerMoves - moveAccumSinceProc);
                    return new AbilityQueuePassiveHudState(schedule.Kind, 0f, moves);
                }
                default:
                    return default;
            }
        }

        /// <summary>Short label for the passive countdown badge (seconds with "s"; moves with "m" = moves remaining).</summary>
        public string GetCountdownLabelText()
        {
            if (!HasCountdown)
            {
                return "";
            }

            if (Kind == AbilityQueuePassiveKind.IntervalSeconds)
            {
                var s = SecondsRemaining;
                if (s >= 10f)
                {
                    return $"{Mathf.CeilToInt(s)}s";
                }

                return $"{s:0.0}s";
            }

            return $"{MovesRemaining}m";
        }
    }
}
