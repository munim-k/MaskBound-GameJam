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

    [Header("Layers & Refs")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Animator _animator; // Drag Child Model here manually
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

        float dist = Vector3.Distance(transform.position, target.position);
    
        if (dist <= attackRange)
        {
            StartCoroutine(MeleeAttackRoutine());
        }
    }
    
    [Header("Detection & References")]
    [SerializeField] private Transform attackPoint; // <--- NEW FIELD
    [SerializeField] private Animator _animator;
    private EnemyMove _movement;

    private IEnumerator MeleeAttackRoutine()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        // 1. Stop Moving
        if (_movement != null) _movement.enabled = false;

        // 2. Play Animation on ALL clients
        PlayAttackAnimClientRpc();

        // 3. Wait for the "Impact" moment (Manually tuned)
        yield return new WaitForSeconds(impactDelay);

        // 4. Deal Damage (Server Only)
        CheckHit();

        // 5. Wait for animation to finish
        // We subtract impactDelay so the total wait equals animationDuration
        yield return new WaitForSeconds(animationDuration - impactDelay);

        // 6. Resume
        if (_movement != null) _movement.enabled = true;
        _isAttacking = false;
    }

    private void CheckHit()
    {
        // Sphere slightly in front and up
        Vector3 hitCenter = transform.position + transform.forward + (Vector3.up * 1f);
        Collider[] hitPlayers = Physics.OverlapSphere(hitCenter, attackRange, playerLayer);

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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward + (Vector3.up * 1f), attackRange);
    }
}