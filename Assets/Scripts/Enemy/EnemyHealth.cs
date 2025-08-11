using UnityEngine;

namespace GameJam.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 10f;
        [SerializeField] private float currentHealth = 10f;
        [SerializeField] private bool destroyOnDeath = true;

        [Header("Optional")]
        [Tooltip("If true, ignores further damage while at or below 0 HP to avoid double-death.")]
        [SerializeField] private bool ignoreDamageWhenDead = true;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        public void SetMaxHealth(float value, bool fillCurrent = true)
        {
            maxHealth = Mathf.Max(1f, value);
            if (fillCurrent)
            {
                currentHealth = maxHealth;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            if (IsDead) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;
            if (ignoreDamageWhenDead && IsDead) return;
            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        private void Die()
        {
            if (!destroyOnDeath) return;
            Destroy(gameObject);
        }
    }
}


