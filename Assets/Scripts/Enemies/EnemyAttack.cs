using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class EnemyAttack : NetworkBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float attackRange = 2f;
    
    [Header("Timing")]
    [Tooltip("How long to wait after starting anim before dealing damage?")]
    [SerializeField] private float impactDelay = 0.5f; 
    [Tooltip("Total duration of the attack animation")]
    [SerializeField] private float animationDuration = 1.5f;

    [Header("Detection & References")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform attackPoint; // <--- The new Detection Point
    [SerializeField] private Animator _animator;
    
    // Internal State
    private EnemyMove _movement;
    private float _lastAttackTime;
    private bool _isAttacking;

    void Awake()
    {
        _movement = GetComponent<EnemyMove>();
        
        // Fail-safe if you forgot to drag it in
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // AI logic runs on Server only
        if (!IsServer) return;

        TryAttack();
    }

    private void TryAttack()
    {
        if (Time.time - _lastAttackTime < attackCooldown || _isAttacking) return;

        Transform target = _movement.GetPlayerTransform();
        if (target == null) return;

        // Note: We check distance from the Enemy Root, not the attack point, 
        // to decide when to START the attack.
        float dist = Vector3.Distance(transform.position, target.position);
    
        if (dist <= attackRange)
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }

    private IEnumerator MeleeAttackRoutine()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        // 1. Stop Moving
        if (_movement != null) _movement.enabled = false;

        // 2. Play Animation on ALL clients
        PlayAttackAnimClientRpc();
        
        // 3. Wait for the "Impact" moment
        yield return new WaitForSeconds(impactDelay);

        // 4. Deal Damage (Server Only)
        CheckHit();

        // 5. Wait for animation to finish
        yield return new WaitForSeconds(animationDuration - impactDelay);

        // 6. Resume
        if (_movement != null) _movement.enabled = true;
        _isAttacking = false;
    }

    private void CheckHit()
    {
        // USE THE NEW ATTACK POINT
        // If attackPoint is null (you forgot to assign it), fall back to the old math.
        Vector3 point = attackPoint != null ? attackPoint.position : (transform.position + transform.forward);

        Collider[] hitPlayers = Physics.OverlapSphere(point, attackRange, playerLayer);

        foreach (Collider obj in hitPlayers)
        {
            if (obj.TryGetComponent(out PlayerHealth health))
            {
                health.TakeDamage(damage);
            }
        }
    }

    public void AE_Die()
    {
        GetComponent<NetworkObject>().Despawn();
    }

    [ClientRpc]
    private void PlayAttackAnimClientRpc()
    {
        if (_animator != null) _animator.SetTrigger("meleeAttack");
    }

    private void OnDrawGizmosSelected()
    {
        // Update Gizmo to show the Attack Point
        Vector3 point = attackPoint != null ? attackPoint.position : (transform.position + transform.forward);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(point, attackRange);
    }
}