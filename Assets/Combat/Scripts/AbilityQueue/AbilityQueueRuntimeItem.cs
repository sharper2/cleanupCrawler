using System;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Non-asset <see cref="IAbilityQueueItem"/> for code-driven or pooled queue entries (passive/evoke as delegates).
    /// </summary>
    public sealed class AbilityQueueRuntimeItem : IAbilityQueueItem
    {
        private readonly Sprite _sprite;
        private readonly string _displayName;
        private readonly AbilityQueuePassiveSchedule _schedule;
        private readonly Action<AbilityQueueContext> _onPassive;
        private readonly Action<AbilityQueueContext> _onEvoke;

        public AbilityQueueRuntimeItem(
            Sprite queueSprite,
            string displayName,
            AbilityQueuePassiveSchedule passiveSchedule,
            Action<AbilityQueueContext> onPassiveProcced = null,
            Action<AbilityQueueContext> onEvoked = null)
        {
            _sprite = queueSprite;
            _displayName = displayName ?? string.Empty;
            _schedule = passiveSchedule;
            _onPassive = onPassiveProcced;
            _onEvoke = onEvoked;
        }

        public Sprite QueueSprite => _sprite;
        public string DisplayName => _displayName;
        public AbilityQueuePassiveSchedule PassiveSchedule => _schedule;

        public void OnPassiveProcced(AbilityQueueContext context)
        {
            _onPassive?.Invoke(context);
        }

        public void OnEvoked(AbilityQueueContext context)
        {
            _onEvoke?.Invoke(context);
        }
    }
}
