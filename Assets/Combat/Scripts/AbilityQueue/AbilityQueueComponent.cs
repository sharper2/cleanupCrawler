using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Defect-style orb queue: back on the left, front on the right. Evoke removes the front with <see cref="evokeKey"/>.
    /// </summary>
    public sealed class AbilityQueueComponent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject player;
        [SerializeField] private DungeonGridPlayerController gridPlayer;

        [Header("Queue")]
        [SerializeField, Min(1)] private int maxSlots = 5;

        [Header("Input")]
        [SerializeField] private KeyCode evokeKey = KeyCode.Space;

        private readonly List<QueuedEntry> _queue = new();
        private AbilityQueueContext _context;
        private Vector2Int _lastPlayerCell;
        private bool _hasLastPlayerCell;

        private sealed class QueuedEntry
        {
            public IAbilityQueueItem Item;
            public float TimeAccum;
            public int MoveAccumSinceProc;
        }

        public int Count => _queue.Count;
        public int MaxSlots => Mathf.Max(1, maxSlots);
        public bool CanEnqueue => _queue.Count < MaxSlots;

        public IAbilityQueueItem GetSlot(int indexFromLeft)
        {
            if (indexFromLeft < 0 || indexFromLeft >= _queue.Count)
            {
                return null;
            }

            return _queue[indexFromLeft].Item;
        }

        private void Awake()
        {
            if (gridPlayer == null)
            {
                gridPlayer = FindFirstObjectByType<DungeonGridPlayerController>();
            }

            if (player == null && gridPlayer != null)
            {
                player = gridPlayer.gameObject;
            }

            if (player == null)
            {
                var combat = FindFirstObjectByType<PlayerCombatController>();
                if (combat != null)
                {
                    player = combat.gameObject;
                }
            }

            if (player == null)
            {
                player = gameObject;
            }

            _context = new AbilityQueueContext(player, this);
        }

        private void Update()
        {
            if (Input.GetKeyDown(evokeKey))
            {
                TryEvokeFront();
            }

            TrackPlayerMoves();
            TickPassiveTimers();
        }

        private void TrackPlayerMoves()
        {
            if (gridPlayer == null || !gridPlayer.TryGetOccupiedCell(out var cell))
            {
                return;
            }

            if (_hasLastPlayerCell && cell != _lastPlayerCell)
            {
                for (var i = 0; i < _queue.Count; i++)
                {
                    var entry = _queue[i];
                    var schedule = entry.Item.PassiveSchedule;
                    if (schedule.Kind != AbilityQueuePassiveKind.EveryNPlayerMoves)
                    {
                        continue;
                    }

                    entry.MoveAccumSinceProc++;
                    while (entry.MoveAccumSinceProc >= schedule.PlayerMoves)
                    {
                        entry.MoveAccumSinceProc -= schedule.PlayerMoves;
                        entry.Item.OnPassiveProcced(_context);
                    }
                }
            }

            _lastPlayerCell = cell;
            _hasLastPlayerCell = true;
        }

        private void TickPassiveTimers()
        {
            var dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            for (var i = 0; i < _queue.Count; i++)
            {
                var entry = _queue[i];
                var schedule = entry.Item.PassiveSchedule;
                if (schedule.Kind != AbilityQueuePassiveKind.IntervalSeconds)
                {
                    continue;
                }

                entry.TimeAccum += dt;
                var interval = schedule.IntervalSeconds;
                while (entry.TimeAccum >= interval)
                {
                    entry.TimeAccum -= interval;
                    entry.Item.OnPassiveProcced(_context);
                }
            }
        }

        /// <summary>Adds to the back (left). Fails if full.</summary>
        public bool TryEnqueue(IAbilityQueueItem item)
        {
            if (item == null || !CanEnqueue)
            {
                return false;
            }

            _queue.Insert(0, new QueuedEntry { Item = item });
            return true;
        }

        /// <summary>Evokes the front item (rightmost slot).</summary>
        public bool TryEvokeFront()
        {
            if (_queue.Count == 0)
            {
                return false;
            }

            var idx = _queue.Count - 1;
            var entry = _queue[idx];
            _queue.RemoveAt(idx);
            entry.Item.OnEvoked(_context);
            return true;
        }
    }
}
