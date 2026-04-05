namespace DungeonGenerator
{
    /// <summary>
    /// While queued, <see cref="AbilityQueueComponent"/> adds <see cref="DamageBonusPerAttack"/> per stack tick when
    /// <see cref="AbilityQueueComponent.NotifyStackDamageFromPlayerHit"/> runs (after player-sourced damage to enemies).
    /// </summary>
    public interface IAbilityQueueAttackStackContributor
    {
        float DamageBonusPerAttack { get; }

        /// <summary>Total damage when evoked (base + accumulated stack from <paramref name="attackStackDamage"/>).</summary>
        float GetEvokeDamageTotal(float attackStackDamage);
    }
}
