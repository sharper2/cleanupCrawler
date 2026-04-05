using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "LightningOrb", menuName = "Combat/Lightning Orb Ability")]
    public class LightningOrbAbilityQueueItem : AbilityQueueItemDefinition
    {
        [Header("Passive (every grid move)")]
        [Tooltip("Chebyshev cell distance (square around player): 1 = adjacent cells including diagonals.")]
        [SerializeField, Min(0)] private int passiveRadiusCells = 1;
        [SerializeField, Min(0f)] private float passiveDamage = 6f;

        [Header("Evoke (Space)")]
        [SerializeField, Min(0)] private int evokeRadiusCells = 2;
        [SerializeField, Min(0f)] private float evokeDamage = 42f;

        [Header("Visual (placeholder until VFX)")]
        [SerializeField] private bool showPassiveRing;
        [SerializeField] private bool showEvokeRing = true;
        [SerializeField, Min(0.01f)] private float passiveRingDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float evokeRingDuration = 0.22f;
        [SerializeField] private Color passiveRingColor = new(0.45f, 0.75f, 1f, 0.9f);
        [SerializeField] private Color evokeRingColor = new(1f, 0.98f, 0.45f, 0.95f);

        public override AbilityQueuePassiveSchedule PassiveSchedule => AbilityQueuePassiveSchedule.EveryNPlayerMoves(1);

        public override void OnPassiveProcced(AbilityQueueContext context)
        {
            if (context?.Player == null)
            {
                return;
            }

            ApplyGridLightning(context, passiveRadiusCells, passiveDamage);
            TryPlayRing(context, passiveRadiusCells, showPassiveRing, passiveRingDuration, passiveRingColor);
        }

        public override void OnEvoked(AbilityQueueContext context)
        {
            if (context?.Player == null)
            {
                return;
            }

            ApplyGridLightning(context, evokeRadiusCells, evokeDamage);
            TryPlayRing(context, evokeRadiusCells, showEvokeRing, evokeRingDuration, evokeRingColor);
        }

        private static void ApplyGridLightning(AbilityQueueContext context, int radiusCells, float damage)
        {
            if (damage <= 0f || radiusCells < 0)
            {
                return;
            }

            if (!TryGetPlayerCellAndCellSize(context.Player, out var playerCell, out _))
            {
                return;
            }

            var dealt = new HashSet<IDamageable>();

            GridCellOccupantRegistry.ForEachOccupantInChebyshevRadius(playerCell, radiusCells, occupant =>
            {
                if (occupant is DungeonGridPlayerController)
                {
                    return;
                }

                if (occupant is not MonoBehaviour mb)
                {
                    return;
                }

                var damageable = mb.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                {
                    return;
                }

                if (!dealt.Add(damageable))
                {
                    return;
                }

                damageable.TakeDamage(damage);
                AbilityQueueComponent.NotifyStackDamageFromPlayerHit(context.Player);
            });
        }

        private void TryPlayRing(
            AbilityQueueContext context,
            int radiusCells,
            bool show,
            float duration,
            Color color)
        {
            if (!show || context?.Player == null || radiusCells < 0)
            {
                return;
            }

            if (!TryGetPlayerCellAndCellSize(context.Player, out _, out var cellSize))
            {
                return;
            }

            if (context.OrbVisuals == null)
            {
                return;
            }

            var worldRadius = Mathf.Max(0.1f, (radiusCells + 0.5f) * cellSize);
            context.OrbVisuals.PlayRing(worldRadius, duration, color);
        }

        private static bool TryGetPlayerCellAndCellSize(GameObject player, out Vector2Int cell, out float cellSize)
        {
            cell = default;
            cellSize = 1f;

            var grid = player.GetComponent<DungeonGridPlayerController>();
            if (grid == null || !grid.TryGetOccupiedCell(out cell))
            {
                return false;
            }

            var builder = grid.dungeonBuilder;
            if (builder != null && builder.cellSize > 0.0001f)
            {
                cellSize = builder.cellSize;
            }

            return true;
        }
    }
}
