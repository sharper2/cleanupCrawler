using UnityEngine;

namespace DungeonGenerator
{
    public class HealthComponent : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath;

        private float _currentHealth;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public bool IsAlive => _currentHealth > 0f;

        private void Awake()
        {
            _currentHealth = MaxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive)
            {
                return;
            }

            if (amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);

            if (_currentHealth <= 0f)
            {
                OnDied();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
        }

        private void OnDied()
        {
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }
}
