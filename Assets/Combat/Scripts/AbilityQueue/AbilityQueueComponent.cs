using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Orb queue: back on the left (newest pickup), front on the right (next evoke, oldest).
    /// New orbs enqueue at the back (left). When full, discards the front (right) then adds at the back.
    /// </summary>
    public sealed class AbilityQueueComponent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject player;
        [SerializeField] private DungeonGridPlayerController gridPlayer;
        [Tooltip("Optional shared orb VFX (rings, etc.). If unset, resolved from the player hierarchy.")]
        [SerializeField] private OrbAbilityVisualFeedback orbVisuals;

        [Header("Queue")]
        [SerializeField, Min(1)] private int maxSlots = 5;

        [Header("Input")]
        [SerializeField] private KeyCode evokeKey = KeyCode.Space;

        private readonly List<QueuedEntry> _queue = new();
        private AbilityQueueContext _context;
        private Vector2Int _lastPlayerCell;
        private bool _hasLastPlayerCell;

        public event Action QueueChanged;
        /// <summary>Fired when the front orb was evoked (Space). Not fired for silent discard when picking up while full.</summary>
        public event Action<IAbilityQueueItem> FrontOrbEvoked;

        private static int _nextOrbInstanceId = 1;

        private sealed class QueuedEntry
        {
            public int InstanceId;
            public IAbilityQueueItem Item;
            public float TimeAccum;
            public int MoveAccumSinceProc;
        }

        public int Count => _queue.Count;
        public int MaxSlots => Mathf.Max(1, maxSlots);

        public IAbilityQueueItem GetSlot(int indexFromLeft)
        {
            if (indexFromLeft < 0 || indexFromLeft >= _queue.Count)
            {
                return null;
            }

            return _queue[indexFromLeft].Item;
        }

        /// <summary>Stable id for HUD animation (unique per orb instance in this run).</summary>
        public int GetSlotInstanceId(int indexFromLeft)
        {
            if (indexFromLeft < 0 || indexFromLeft >= _queue.Count)
            {
                return 0;
            }

            return _queue[indexFromLeft].InstanceId;
        }

        /// <summary>
        /// Passive countdown for HUD (time or moves). Use with <see cref="AbilityQueuePassiveHudState"/> helpers.
        /// </summary>
        public bool TryGetPassiveCountdownForInstanceId(int instanceId, out AbilityQueuePassiveHudState state)
        {
            state = default;
            for (var i = 0; i < _queue.Count; i++)
            {
                var e = _queue[i];
                if (e.InstanceId != instanceId)
                {
                    continue;
                }

                state = AbilityQueuePassiveHudState.Compute(e.Item.PassiveSchedule, e.TimeAccum, e.MoveAccumSinceProc);
                return true;
            }

            return false;
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

            var resolvedVisuals = orbVisuals;
            if (resolvedVisuals == null && player != null)
            {
                resolvedVisuals = player.GetComponentInChildren<OrbAbilityVisualFeedback>(true);
            }

            _context = new AbilityQueueContext(player, this, resolvedVisuals);
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
                ProcessMovementPassivesForEveryQueuedOrb();
            }

            _lastPlayerCell = cell;
            _hasLastPlayerCell = true;
        }

        /// <summary>
        /// Each queued orb runs its move-based passive independently (left to right in the queue), not only the front (evoke) orb.
        /// </summary>
        private void ProcessMovementPassivesForEveryQueuedOrb()
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

        /// <summary>
        /// Time-based passives tick for every queued orb, not only the front orb.
        /// </summary>
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

        /// <summary>
        /// Adds an orb at the back of the queue (left). If full, removes the front (right) first (no evoke).
        /// </summary>
        public bool TryEnqueue(IAbilityQueueItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (_queue.Count >= MaxSlots)
            {
                TryDiscardFrontSilently();
            }

            var id = _nextOrbInstanceId++;
            _queue.Insert(0, new QueuedEntry { InstanceId = id, Item = item });
            RaiseQueueChanged();
            return true;
        }

        /// <summary>Removes the front orb (rightmost) without firing <see cref="IAbilityQueueItem.OnEvoked"/>.</summary>
        public bool TryDiscardFrontSilently()
        {
            if (_queue.Count == 0)
            {
                return false;
            }

            _queue.RemoveAt(_queue.Count - 1);
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
            var item = entry.Item;
            FrontOrbEvoked?.Invoke(item);
            _queue.RemoveAt(idx);
            item.OnEvoked(_context);
            RaiseQueueChanged();
            return true;
        }

        private void RaiseQueueChanged()
        {
            QueueChanged?.Invoke();
        }
    }
}
