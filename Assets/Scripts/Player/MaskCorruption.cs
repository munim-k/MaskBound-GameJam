using UnityEngine;
using Unity.Netcode;

namespace MaskBound.Player
{
    /// <summary>
    /// Manages mask corruption system (GDD-specified)
    /// 
    /// GDD: "Wearing a mask continuously builds Corruption.
    /// After 30 seconds, Corruption begins dealing linear damage over time to the wearer.
    /// Corruption damage can knock a player.
    /// Mask timer resets when switching masks."
    /// 
    /// Feedback:
    /// - Mask visually cracks as corruption builds
    /// - Audio warning plays when 5 seconds remain before damage begins
    /// </summary>
    public class MaskCorruption : NetworkBehaviour
    {
        [Header("Corruption Timing (GDD)")]
        [Tooltip("Grace period before corruption damage starts (GDD: 30 seconds)")]
        [SerializeField] private float corruptionGracePeriod = 30f;
        
        [Tooltip("Time before damage when warning plays (GDD: 5 seconds)")]
        [SerializeField] private float warningThreshold = 5f;

        [Header("Corruption Damage")]
        [Tooltip("Damage per second after grace period ends")]
        [SerializeField] private float corruptionDamagePerSecond = 5f;
        
        [Tooltip("Can corruption damage knock the player? (GDD: Yes)")]
        [SerializeField] private bool canKnockPlayer = true;

        [Header("Audio")]
        [Tooltip("Audio clip to play when corruption warning triggers")]
        [SerializeField] private AudioClip warningSound;
        
        [Tooltip("Audio source for corruption sounds")]
        [SerializeField] private AudioSource audioSource;

        // Network-synchronized timer
        private NetworkVariable<float> corruptionTimer = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        private bool warningPlayed = false;
        private bool isDealingDamage = false;
        private PlayerMaskManager maskManager;

        public override void OnNetworkSpawn()
        {
            maskManager = GetComponent<PlayerMaskManager>();
            
            if (maskManager == null)
            {
                Debug.LogError("[MaskCorruption] No PlayerMaskManager found on this GameObject!");
            }

            // Only owner updates the timer
            if (IsOwner)
            {
                Debug.Log($"[MaskCorruption] Initialized for Client {OwnerClientId}");
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            
            // Only tick if player has a mask
            if (maskManager == null || !maskManager.HasMask())
            {
                return;
            }

            // Increment corruption timer
            corruptionTimer.Value += Time.deltaTime;
            
            float timeUntilDamage = corruptionGracePeriod - corruptionTimer.Value;
            
            // Play warning sound at threshold
            if (!warningPlayed && timeUntilDamage <= warningThreshold && timeUntilDamage > 0)
            {
                PlayWarning();
                warningPlayed = true;
                Debug.LogWarning($"[MaskCorruption] ⚠️ WARNING: Corruption damage in {timeUntilDamage:F1}s!");
            }

            // Start dealing corruption damage after grace period
            if (corruptionTimer.Value > corruptionGracePeriod)
            {
                if (!isDealingDamage)
                {
                    isDealingDamage = true;
                    Debug.LogError($"[MaskCorruption] 💀 CORRUPTION DAMAGE STARTED! Client {OwnerClientId}");
                }
                
                ApplyCorruptionDamage(Time.deltaTime);
            }
        }

        /// <summary>
        /// Apply corruption damage to the player
        /// </summary>
        private void ApplyCorruptionDamage(float deltaTime)
        {
            float damageThisFrame = corruptionDamagePerSecond * deltaTime;
            
            // TODO: Apply damage to player health component
            // For now, just log
            Debug.Log($"[MaskCorruption] Dealing {damageThisFrame:F2} corruption damage (total: {(corruptionTimer.Value - corruptionGracePeriod) * corruptionDamagePerSecond:F1})");
            
            // In full implementation:
            // var health = GetComponent<PlayerHealth>();
            // if (health != null)
            // {
            //     health.TakeDamage(damageThisFrame, DamageType.Corruption, canKnock: canKnockPlayer);
            // }
        }

        /// <summary>
        /// Play audio warning when corruption is about to start
        /// </summary>
        private void PlayWarning()
        {
            if (warningSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(warningSound);
                Debug.Log("[MaskCorruption] 🔊 Played corruption warning sound");
            }
            else
            {
                Debug.LogWarning("[MaskCorruption] Warning sound or AudioSource not assigned!");
            }
        }

        /// <summary>
        /// Reset corruption timer (called when mask is swapped)
        /// GDD: "Mask timer resets when switching masks"
        /// </summary>
        public void ResetCorruption()
        {
            float oldTimer = corruptionTimer.Value;
            corruptionTimer.Value = 0f;
            warningPlayed = false;
            isDealingDamage = false;
            
            Debug.Log($"[MaskCorruption] ✅ Corruption reset! (was at {oldTimer:F1}s)");
        }

        /// <summary>
        /// Get current corruption progress (0.0 to 1.0+)
        /// Used for visual feedback (cracking mask texture)
        /// </summary>
        public float GetCorruptionProgress()
        {
            return Mathf.Clamp01(corruptionTimer.Value / corruptionGracePeriod);
        }

        /// <summary>
        /// Get time remaining until corruption damage starts
        /// </summary>
        public float GetTimeUntilDamage()
        {
            return Mathf.Max(0f, corruptionGracePeriod - corruptionTimer.Value);
        }

        /// <summary>
        /// Is corruption currently dealing damage?
        /// </summary>
        public bool IsDealingDamage()
        {
            return isDealingDamage;
        }

        /// <summary>
        /// Get current corruption timer value (for debugging)
        /// </summary>
        public float GetCurrentTimer()
        {
            return corruptionTimer.Value;
        }
    }
}
