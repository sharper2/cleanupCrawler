using UnityEngine;

namespace DungeonGenerator
{
    public class ManaComponent : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxMana = 100f;
        [SerializeField, Min(0f)] private float startingMana = 100f;
        [SerializeField] private bool regenerateMana = true;
        [SerializeField, Min(0f)] private float manaRegenPerSecond = 5f;

        private float _currentMana;

        public float MaxMana => Mathf.Max(1f, maxMana);
        public float CurrentMana => Mathf.Clamp(_currentMana, 0f, MaxMana);

        private void Awake()
        {
            _currentMana = Mathf.Clamp(startingMana, 0f, MaxMana);
        }

        private void Update()
        {
            if (!regenerateMana || manaRegenPerSecond <= 0f || CurrentMana >= MaxMana)
            {
                return;
            }

            _currentMana = Mathf.Min(MaxMana, _currentMana + manaRegenPerSecond * Time.deltaTime);
        }

        public bool TrySpend(float amount)
        {
            var spendAmount = Mathf.Max(0f, amount);
            if (spendAmount <= 0f)
            {
                return true;
            }

            if (CurrentMana < spendAmount)
            {
                return false;
            }

            _currentMana = Mathf.Max(0f, _currentMana - spendAmount);
            return true;
        }

        public void Restore(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentMana = Mathf.Min(MaxMana, _currentMana + amount);
        }
    }
}
