using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Spherical hitbox that damages enemies once each, ignores the owner, and despawns when its center
    /// leaves a walkable dungeon cell (so narrow corridors can be threaded if aimed well).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChargedSphereProjectile : MonoBehaviour
    {
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        private DungeonBasic3DBuilder _builder;
        private Vector3 _direction;
        private float _speed;
        private float _damage;
        private float _hitRadius;
        private Transform _owner;
        private readonly HashSet<IDamageable> _damaged = new HashSet<IDamageable>();

        public void Initialize(
            DungeonBasic3DBuilder builder,
            Vector3 worldPosition,
            Vector3 direction,
            float damage,
            float speed,
            float hitRadius,
            Transform owner)
        {
            _builder = builder;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _damage = damage;
            _speed = Mathf.Max(0.01f, speed);
            _hitRadius = Mathf.Max(0.01f, hitRadius);
            _owner = owner;

            transform.position = worldPosition;
            transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);

            // Default Unity sphere mesh has radius 0.5 in local space.
            transform.localScale = Vector3.one * (_hitRadius * 2f);

            if (_damage <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_builder == null)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 delta = _direction * (_speed * Time.deltaTime);
            Vector3 next = transform.position + delta;

            if (!IsCenterInWalkable(next))
            {
                Destroy(gameObject);
                return;
            }

            transform.position = next;
            ApplyDamageAtCenter();
        }

        private bool IsCenterInWalkable(Vector3 worldPosition)
        {
            if (!_builder.TryWorldToCell(worldPosition, out var cell))
            {
                return false;
            }

            return _builder.IsCellWalkable(cell);
        }

        private void ApplyDamageAtCenter()
        {
            Vector3 center = transform.position;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                _hitRadius,
                OverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var collider = OverlapBuffer[i];
                OverlapBuffer[i] = null;
                if (collider == null)
                {
                    continue;
                }

                if (_owner != null && collider.transform.IsChildOf(_owner))
                {
                    continue;
                }

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive || !_damaged.Add(damageable))
                {
                    continue;
                }

                damageable.TakeDamage(_damage);
                if (_owner != null)
                {
                    AbilityQueueComponent.NotifyStackDamageFromPlayerHit(_owner.gameObject);
                }
            }
        }
    }
}
