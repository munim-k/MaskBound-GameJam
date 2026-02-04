using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using MaskBound.Combat;
using MaskBound.Player;
using MaskBound.Enemy;
using MaskBound.Core.Interfaces;
using MaskBound.Core.Data;

/// <summary>
/// Server-authoritative sword attack system.
/// Client predicts animation locally, server validates hits and applies damage.
/// </summary>
public class SwordAttack : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    
    [Header("Input Action")]
    [SerializeField] private InputActionReference attackAction;

    [Header("Attack Settings")]
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float attackCooldown = 0.2f;
    [SerializeField] private float attackDamage = 10f;
    
    [Header("Hit Detection")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.5f;
    [SerializeField] private LayerMask enemyLayers;

    // Constants
    private const float DAMAGE_DELAY_NORMALIZED = 11f / 12f;
    private const string ANIM_IS_ATTACKING = "isAttacking";
    private const string ANIM_ATTACK_SPEED = "AttackSpeed";

    // State
    private bool isAttacking = false;

    private void OnEnable()
    {
        attackAction?.action.Enable();
    }

    private void OnDisable()
    {
        attackAction?.action.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;
        
        if (attackAction.action.WasPressedThisFrame() && !isAttacking)
        {
            // Client: Predict animation locally for instant feedback
            StartCoroutine(PlayAttackAnimationLocal());
            
            // Server: Validate and apply damage
            RequestAttackServerRpc(attackPoint.position, transform.forward, attackRange);
        }
    }

    /// <summary>
    /// Client-side: Play attack animation locally for instant feedback
    /// </summary>
    private IEnumerator PlayAttackAnimationLocal()
    {
        isAttacking = true;
        animator.SetBool(ANIM_IS_ATTACKING, true);
        animator.SetFloat(ANIM_ATTACK_SPEED, attackSpeed);

        if(IsOwner)
            AudioManager.instance.PlayOneShot(FMODEvents.instance.swordSwing, transform.position);

        yield return new WaitForSeconds(attackDuration);
        animator.SetBool(ANIM_IS_ATTACKING, false);
    
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    /// <summary>
    /// Client requests server to validate and execute attack
    /// </summary>
    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void RequestAttackServerRpc(Vector3 attackPosition, Vector3 attackDirection, float range, RpcParams rpcParams = default)
    {
        // Server validates hit detection
        Collider[] hitColliders = Physics.OverlapSphere(attackPosition, range, enemyLayers);

        if (hitColliders.Length == 0)
        {
//            Debug.Log($"[SwordAttack] Client {OwnerClientId} attack missed (no targets in range)");
            return;
        }

        // Track unique hits (avoid hitting same enemy multiple times)
        HashSet<NetworkObject> hitEnemies = new HashSet<NetworkObject>();

        // Cache player components ONCE (performance optimization)
        var playerAffinity = GetComponent<PlayerAffinity>();
        var playerMask = GetComponent<PlayerMaskManager>();

        // Validate player components exist
        if (playerAffinity == null)
        {
            Debug.LogError("[SwordAttack] Player missing PlayerAffinity component!");
        }
        if (playerMask == null)
        {
            Debug.LogError("[SwordAttack] Player missing PlayerMaskManager component!");
        }

        foreach (Collider col in hitColliders)
        {
            // Get NetworkObject root to identify unique enemy
            NetworkObject enemyNetObj = col.GetComponentInParent<NetworkObject>();
            
            if (enemyNetObj == null)
            {
                Debug.LogWarning($"[SwordAttack] Hit object {col.name} has no NetworkObject");
                continue;
            }

            if (hitEnemies.Contains(enemyNetObj))
                continue; // Already damaged this enemy

            hitEnemies.Add(enemyNetObj);

            // Get enemy classification for GDD damage calculation
            var enemyType = enemyNetObj.GetComponent<EnemyClassification>();
            if (enemyType == null)
            {
                Debug.LogWarning($"[SwordAttack] {enemyNetObj.name} missing EnemyClassification component! Skipping damage calculation.");
                continue; // CRITICAL FIX: Skip this enemy instead of trying to use null reference
            }

            float finalDamage = attackDamage;

            bool wasStrong = false;

            // Calculate GDD-compliant damage if player components present
            if (playerAffinity != null && playerMask != null)
            {
                var damageResult = DamageCalculator.CalculateDamage(
                    attackDamage,
                    playerAffinity.AffinityTarget,
                    playerMask.CurrentMask,
                    enemyType.Family,
                    enemyType.Element
                );
                finalDamage = damageResult.FinalDamage;
                wasStrong = damageResult.ElementCounter || damageResult.AffinityBonus;
            }
            else
            {
                Debug.LogWarning("[SwordAttack] Missing player components - using base damage only");
            }

            // Apply damage via IDamageable interface (future-proof)
            var damageable = enemyNetObj.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage, new DamageSource 
                { 
                    AttackerClientId = OwnerClientId,
                    DamageType = DamageType.Melee,
                    HitPoint = attackPosition,
                    IsCritical = wasStrong
                });
                Debug.Log($"[SwordAttack] Client {OwnerClientId} hit {enemyNetObj.name} for {finalDamage} damage (base: {attackDamage})");
            }
            else
            {
                // Fallback: Try legacy EnemyHealth component
                var enemyHealth = enemyNetObj.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(finalDamage, wasStrong);
                    Debug.Log($"[SwordAttack] Client {OwnerClientId} hit {enemyNetObj.name} (legacy) for {finalDamage} damage");
                }
                else
                {
                    Debug.LogWarning($"[SwordAttack] Hit {enemyNetObj.name} has no damage component");
                }
            }
        }
    }

    // Gizmo for visualizing attack range in editor
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}

// TODO: Move to Core/Interfaces/IDamageable.cs
public interface IDamageable
{
    void TakeDamage(float amount, DamageSource source);
    float CurrentHealth { get; }
    float MaxHealth { get; }
}

// TODO: Move to Core/Data/DamageSource.cs
public struct DamageSource
{
    public ulong AttackerClientId;
    public DamageType DamageType;
    public Vector3 HitPoint;
    public bool IsCritical;
}

// TODO: Move to Core/Enums/DamageType.cs
public enum DamageType
{
    Melee,
    Ranged,
    Magic,
    Environmental
}