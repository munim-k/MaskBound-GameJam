using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using MaskBound.Core.Interfaces;
using MaskBound.Core.Data;
using MaskBound.Core.Enums;

namespace MaskBound.Enemy
{
    /// <summary>
    /// Server-authoritative enemy health component.
    /// Implements IDamageable interface for damage system integration.
    /// </summary>
    public class EnemyHealth : NetworkBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        
        [Header("UI")]
        [SerializeField] private Slider healthBar;

        // Server-authoritative health
        public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // IDamageable implementation
        public float CurrentHealth => currentHealth.Value;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth.Value > 0;

        public override void OnNetworkSpawn()
        {
            // Initialize UI
            if (healthBar != null)
            {
                healthBar.maxValue = maxHealth;
                healthBar.value = currentHealth.Value;
            }

            // Subscribe to health changes
            currentHealth.OnValueChanged += HandleHealthChanged;

            // Server: Set starting health
            if (IsServer)
            {
                currentHealth.Value = maxHealth;
            }
        }

        public override void OnNetworkDespawn()
        {
            currentHealth.OnValueChanged -= HandleHealthChanged;
        }

        /// <summary>
        /// Legacy damage method - redirects to interface method
        /// </summary>
        public void TakeDamage(float damage)
        {
            TakeDamage(damage, new DamageSource 
            { 
                DamageType = DamageType.Environmental 
            });
        }

        /// <summary>
        /// IDamageable implementation - server validates and applies damage
        /// </summary>
        public void TakeDamage(float amount, DamageSource source)
        {
            if (!IsServer)
            {
                Debug.LogWarning($"[EnemyHealth] TakeDamage called on client - damage ignored");
                return;
            }

            if (!IsAlive)
            {
                Debug.LogWarning($"[EnemyHealth] Damage attempted on dead enemy");
                return;
            }

            // TODO: Apply affinity/element damage multipliers here
            float finalDamage = amount;

            // Apply damage
            currentHealth.Value = Mathf.Max(0, currentHealth.Value - finalDamage);

            Debug.Log($"[EnemyHealth] {gameObject.name} took {finalDamage} damage from client {source.AttackerClientId} ({currentHealth.Value}/{maxHealth})");

            // Check for death
            if (currentHealth.Value <= 0)
            {
                // Die(source);
                killEnemyServerRpc();
            }
        }

        /// <summary>
        /// Handle enemy death
        /// </summary>
        private void Die()
        {

            // Debug.Log($"[EnemyHealth] {gameObject.name} died (killed by client {source.AttackerClientId})");

            // TODO: Play death animation
            // TODO: Drop loot/rewards
            // TODO: Trigger arena spawn logic

            // Despawn instead of Destroy for networked objects
            GetComponent<NetworkObject>().Despawn();
        }

        [ServerRpc(RequireOwnership = false)]
        private void killEnemyServerRpc(ServerRpcParams rpcParams = default)
        {
            Die();
        }

        /// <summary>
        /// Called when health NetworkVariable changes
        /// </summary>
        private void HandleHealthChanged(float previousValue, float newValue)
        {
            // Update UI
            if (healthBar != null)
            {
                healthBar.value = newValue;
            }

            // TODO: Show damage feedback VFX
            if (newValue < previousValue)
            {
                float damageAmount = previousValue - newValue;
                ShowDamageFeedback(damageAmount);
            }
        }

        /// <summary>
        /// Visual feedback for damage
        /// </summary>
        private void ShowDamageFeedback(float damageAmount)
        {
            // TODO: Implement damage numbers, flash effect, hit sound
            Debug.Log($"[EnemyHealth] Showing {damageAmount} damage feedback");
        }

        // Debug visualization
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            
            Gizmos.color = IsAlive ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.3f);
        }
    }
}
