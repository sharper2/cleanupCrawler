using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Orb queue: back on the left (newest pickup), front on the right (next evoke, oldest).
    /// New orbs enqueue at the back (left). When full, evokes the front (right) like Space, then adds the new orb at the back.
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
        /// <summary>Fired when the front orb was evoked (Space or forced out by a pickup while the queue was full).</summary>
        public event Action<IAbilityQueueItem> FrontOrbEvoked;

        private static int _nextOrbInstanceId = 1;

        private sealed class QueuedEntry
        {
            public int InstanceId;
            public IAbilityQueueItem Item;
            public float TimeAccum;
            public int MoveAccumSinceProc;
            public float AttackStackDamage;
        }

        public int Count => _queue.Count;
        public int MaxSlots => Mathf.Max(1, maxSlots);

        /// <summary>True while evoke invulnerability is active (e.g. shield orb).</summary>
        public bool IsInvulnerable => Time.unscaledTime < _invulnerableUntilUnscaled;

        private float _invulnerableUntilUnscaled;

        /// <summary>
        /// Incoming damage multiplier from queued <see cref="IAbilityQueueDamageReductionWhileQueued"/> orbs (1 = none).
        /// </summary>
        public float GetQueuedDamageReductionMultiplier()
        {
            var totalPercent = 0f;
            for (var i = 0; i < _queue.Count; i++)
            {
                var item = _queue[i].Item;
                if (item is IAbilityQueueDamageReductionWhileQueued dr)
                {
                    totalPercent += dr.DamageReductionPercentWhileQueued;
                }
            }

            totalPercent = Mathf.Clamp(totalPercent, 0f, 90f);
            return (100f - totalPercent) / 100f;
        }

        /// <summary>Player ignores positive damage until this unscaled time (exclusive of past).</summary>
        public void SetInvulnerableUntilUnscaled(float unscaledTime)
        {
            _invulnerableUntilUnscaled = Mathf.Max(_invulnerableUntilUnscaled, unscaledTime);
        }

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

        /// <summary>Attack-stack bonus accumulated while queued (for <see cref="IAbilityQueueAttackStackContributor"/> HUD).</summary>
        public bool TryGetAttackStackDamageForInstanceId(int instanceId, out float attackStackDamage)
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                var e = _queue[i];
                if (e.InstanceId != instanceId)
                {
                    continue;
                }

                attackStackDamage = e.AttackStackDamage;
                return true;
            }

            attackStackDamage = 0f;
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
                player = gameObject;
            }

            var resolvedVisuals = orbVisuals;
            if (resolvedVisuals == null && player != null)
            {
                resolvedVisuals = player.GetComponentInChildren<OrbAbilityVisualFeedback>(true);
            }

            _context = new AbilityQueueContext(player, this, resolvedVisuals);
        }

        /// <summary>
        /// Increments stack damage for <see cref="IAbilityQueueAttackStackContributor"/> orbs.
        /// Use <see cref="NotifyStackDamageFromPlayerHit"/> after player-sourced damage to enemies, or call from custom gameplay.
        /// </summary>
        public void NotifyPlayerAttackExecuted()
        {
            var any = false;
            for (var i = 0; i < _queue.Count; i++)
            {
                var entry = _queue[i];
                if (entry.Item is IAbilityQueueAttackStackContributor contributor)
                {
                    entry.AttackStackDamage += contributor.DamageBonusPerAttack;
                    any = true;
                }
            }

            if (any)
            {
                RaiseQueueChanged();
            }
        }

        /// <summary>
        /// Call once per enemy hit after the player deals damage (melee, lightning orb, charged sphere, ranged projectile, etc.).
        /// Resolves the player's <see cref="AbilityQueueComponent"/> from <paramref name="playerDamageSource"/> (weapon root, camera child, projectile owner, etc.).
        /// </summary>
        public static void NotifyStackDamageFromPlayerHit(GameObject playerDamageSource)
        {
            if (playerDamageSource == null)
            {
                return;
            }

            if (playerDamageSource.GetComponentInParent<StaticEnemy>() != null)
            {
                return;
            }

            var grid = playerDamageSource.GetComponentInParent<DungeonGridPlayerController>()
                       ?? playerDamageSource.GetComponent<DungeonGridPlayerController>();
            if (grid == null)
            {
                return;
            }

            var queue = grid.GetComponent<AbilityQueueComponent>()
                        ?? grid.GetComponentInChildren<AbilityQueueComponent>(true);
            queue?.NotifyPlayerAttackExecuted();
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
        /// Adds an orb at the back of the queue (left). If full, evokes the front (right) first, then enqueues.
        /// </summary>
        public bool TryEnqueue(IAbilityQueueItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (_queue.Count >= MaxSlots)
            {
                TryEvokeFront();
            }

            var id = _nextOrbInstanceId++;
            _queue.Insert(0, new QueuedEntry { InstanceId = id, Item = item, AttackStackDamage = 0f });
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
            _context.EvokeAttackStackDamage = entry.AttackStackDamage;
            item.OnEvoked(_context);
            _context.EvokeAttackStackDamage = 0f;
            RaiseQueueChanged();
            return true;
        }

        private void RaiseQueueChanged()
        {
            QueueChanged?.Invoke();
        }
    }
}
