using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class EnemyAttack : NetworkBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask playerLayer;

    private float _lastAttackTime;
    private bool _isAttacking;

    // References
    private EnemyMove _movement;
    private Animator _anim;

    void Awake()
    {
        _movement = GetComponent<EnemyMove>();
        _anim = GetComponent<Animator>();
    }

    void Update()
    {
        // Only the server manages AI logic
        if (!IsServer) return;

        TryAttack();
    }

    private void TryAttack()
    {
        if (Time.time - _lastAttackTime < attackCooldown || _isAttacking) return;

        Transform target = _movement.GetPlayerTransform(); 

        if (target == null) 
        {
            // This will tell us if the enemy lost track of the players
            Debug.LogWarning("EnemyAttack: Target is null!");
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);

        // This log will tell us exactly why the 'if' statement is failing
        // Debug.Log($"Distance to player: {dist}. Required Range: {attackRange}");

        if (dist <= attackRange)
        {
            Debug.Log("Range Check Passed! Starting Attack Sequence.");
            StartCoroutine(PerformAttackSequence());
        }
    }

    private IEnumerator PerformAttackSequence()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;

        // 1. Tell all clients to play the animation
        PlayAttackAnimationClientRpc();

        // 2. Disable movement during attack
        _movement.enabled = false;

        // 3. Wait for the 'impact' frame (adjust based on your animation)
        yield return new WaitForSeconds(0.5f);

        // 4. Server-side hit detection
        CheckHit();

        // 5. Wait for animation to finish
        yield return new WaitForSeconds(0.5f);

        _movement.enabled = true;
        _isAttacking = false;
    }

    private void CheckHit()
    {
        // Use a small sphere at the front of the enemy to detect player
        Collider[] hitPlayers = Physics.OverlapSphere(transform.position + transform.forward, attackRange, playerLayer);

        foreach (Collider obj in hitPlayers)
        {
            if (obj.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(damage);
            }
        }
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        if (_anim != null) _anim.SetTrigger("Attack");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw the attack sphere in front of the enemy
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward, attackRange);
    }
}