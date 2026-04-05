using UnityEngine;

namespace DungeonGenerator
{
    public class CombatProjectile : MonoBehaviour
    {
        [SerializeField] private float defaultSpeed = 12f;
        [SerializeField] private float defaultDamage = 10f;
        [SerializeField] private float defaultLifetime = 2f;

        private Vector3 _direction = Vector3.forward;
        private Vector3 _origin;
        private float _travelDistance;
        private float _speed;
        private float _damage;
        private float _expiresAt;
        private Transform _owner;

        public void Initialize(Vector3 direction, float speed, float damage, float lifetime, Transform owner)
        {
            transform.SetParent(null, true);
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            _origin = transform.position;
            _travelDistance = 0f;
            _speed = Mathf.Max(0.1f, speed);
            _damage = Mathf.Max(0f, damage);
            _expiresAt = Time.time + Mathf.Max(0.01f, lifetime);
            _owner = owner;
            transform.forward = _direction;
        }

        private void Awake()
        {
            _origin = transform.position;
            _travelDistance = 0f;
            _speed = Mathf.Max(0.1f, defaultSpeed);
            _damage = Mathf.Max(0f, defaultDamage);
            _expiresAt = Time.time + Mathf.Max(0.01f, defaultLifetime);
        }

        private void Update()
        {
            if (Time.time >= _expiresAt)
            {
                Destroy(gameObject);
                return;
            }

            var start = _origin + (_direction * _travelDistance);
            var stepDistance = _speed * Time.deltaTime;
            var end = start + (_direction * stepDistance);

            if (Physics.Raycast(start, _direction, out var hit, stepDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                HandleImpact(hit.collider);
                return;
            }

            _travelDistance += stepDistance;
            transform.position = end;
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleImpact(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision != null)
            {
                HandleImpact(collision.collider);
            }
        }

        private void HandleImpact(Collider collider)
        {
            if (collider == null)
            {
                return;
            }

            if (_owner != null && collider.transform.IsChildOf(_owner))
            {
                return;
            }

            var health = collider.GetComponentInParent<HealthComponent>();
            if (health != null)
            {
                health.TakeDamage(_damage);
                if (_owner != null)
                {
                    AbilityQueueComponent.NotifyStackDamageFromPlayerHit(_owner.gameObject);
                }
            }

            Destroy(gameObject);
        }
    }
}
