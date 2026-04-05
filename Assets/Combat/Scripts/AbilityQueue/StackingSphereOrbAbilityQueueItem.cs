using UnityEngine;

namespace DungeonGenerator
{
    [CreateAssetMenu(fileName = "StackingSphereOrb", menuName = "Combat/Stacking Sphere Orb Ability")]
    public class StackingSphereOrbAbilityQueueItem : AbilityQueueItemDefinition, IAbilityQueueAttackStackContributor
    {
        [Header("Stack (player weapon attacks while queued)")]
        [SerializeField, Min(0f)] private float damageBonusPerAttack = 3f;
        [SerializeField, Min(0f)] private float baseEvokeDamage = 8f;

        [Header("Projectile (evoke)")]
        [SerializeField, Min(0.01f)] private float baseHitRadius = 0.35f;
        [SerializeField, Min(0f)] private float hitRadiusPerTotalDamage = 0.02f;
        [SerializeField, Min(0.01f)] private float maxSpeed = 18f;
        [SerializeField, Min(0.01f)] private float minSpeed = 2f;
        [Tooltip("Higher values keep speed closer to maxSpeed at low total damage; speed falls as total damage grows.")]
        [SerializeField, Min(0.01f)] private float speedReferenceDamage = 40f;
        [SerializeField, Min(0f)] private float castHeight = 0.5f;
        [Tooltip("If set, must include ChargedSphereProjectile or it will be added at runtime. Default Unity sphere radius is 0.5 local units.")]
        [SerializeField] private GameObject projectilePrefab;

        public float DamageBonusPerAttack => damageBonusPerAttack;

        public float GetEvokeDamageTotal(float attackStackDamage)
        {
            return Mathf.Max(0f, baseEvokeDamage + Mathf.Max(0f, attackStackDamage));
        }

        public override AbilityQueuePassiveSchedule PassiveSchedule => AbilityQueuePassiveSchedule.None;

        public override void OnEvoked(AbilityQueueContext context)
        {
            if (context?.Player == null)
            {
                return;
            }

            var grid = context.Player.GetComponent<DungeonGridPlayerController>()
                       ?? context.Player.GetComponentInChildren<DungeonGridPlayerController>(true);
            var builder = grid != null ? grid.dungeonBuilder : Object.FindFirstObjectByType<DungeonBasic3DBuilder>();
            if (builder == null)
            {
                return;
            }

            float stack = Mathf.Max(0f, context.EvokeAttackStackDamage);
            float totalDamage = Mathf.Max(0f, baseEvokeDamage + stack);
            float speed = ComputeSpeed(totalDamage);
            float hitRadius = ComputeHitRadius(totalDamage);

            Vector3 origin = grid != null
                ? grid.transform.position + Vector3.up * castHeight
                : context.Player.transform.position + Vector3.up * castHeight;
            Vector3 dir = ComputeOrbFireDirection(context.Player, grid);

            GameObject go;
            if (projectilePrefab != null)
            {
                go = Object.Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir, Vector3.up));
                Object.Destroy(go.GetComponent<Collider>());
            }

            var projectile = go.GetComponent<ChargedSphereProjectile>();
            if (projectile == null)
            {
                projectile = go.AddComponent<ChargedSphereProjectile>();
            }

            projectile.Initialize(builder, origin, dir, totalDamage, speed, hitRadius, context.Player.transform);
        }

        private float ComputeSpeed(float totalDamage)
        {
            float t = Mathf.Max(0f, totalDamage);
            float raw = maxSpeed * speedReferenceDamage / (speedReferenceDamage + t);
            return Mathf.Clamp(raw, minSpeed, maxSpeed);
        }

        private float ComputeHitRadius(float totalDamage)
        {
            return Mathf.Max(0.01f, baseHitRadius + totalDamage * hitRadiusPerTotalDamage);
        }

        /// <summary>
        /// Same facing as thrust / sword: <see cref="CombatAttackController"/> forward, snapped to cardinals (matches melee).
        /// Falls back to camera, then grid facing.
        /// </summary>
        private static Vector3 ComputeOrbFireDirection(GameObject player, DungeonGridPlayerController grid)
        {
            var combat = player.GetComponent<CombatAttackController>()
                         ?? player.GetComponentInChildren<CombatAttackController>(true)
                         ?? player.GetComponentInParent<CombatAttackController>();
            if (combat != null)
            {
                return CombatAttackController.SnapToGridDirection(combat.transform.forward);
            }

            var cam = Camera.main;
            if (cam != null)
            {
                var f = cam.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.0001f)
                {
                    return CombatAttackController.SnapToGridDirection(f.normalized);
                }
            }

            if (grid != null)
            {
                return grid.GetFacingWorldDirection();
            }

            var flat = player.transform.forward;
            flat.y = 0f;
            return CombatAttackController.SnapToGridDirection(flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward);
        }

    }
}
