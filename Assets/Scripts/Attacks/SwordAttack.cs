using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Netcode;
using System.Collections;

public class SwordAttack : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    
    [Header("Input Action")]
    // Drag your Input Action Reference here (e.g., 'Fire' or 'Attack')
    [SerializeField] private InputActionReference attackAction;

    [Header("Attack Settings")]
    [SerializeField] private float attackSpeed = 1f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float attackCooldown = 0.2f;

    [SerializeField] private float attackDamage = 2f;
    [SerializeField] LayerMask hitLayerMask;
    
    [Header("Hit Detection")]
    [SerializeField] private Transform attackPoint; // Create an empty GameObject at the sword's tip
    [SerializeField] private float attackRange = 0.5f;
    [SerializeField] private LayerMask enemyLayers;

    // State
    private bool isAttacking = false;

    private void OnEnable()
    {
        // Enable the input action when the object is active
        attackAction.action.Enable();
    }

    private void OnDisable()
    {
        // Disable it when inactive to prevent errors
        attackAction.action.Disable();
    }

    void Update()
    {
        if (!IsOwner) return;
        // Check if the button was just performed (clicked)
        if (attackAction.action.WasPressedThisFrame() && !isAttacking)
        {
            StartCoroutine(PerformAttack());
        }
    }

    IEnumerator PerformAttack()
    {
        var damageDelay = 11 / 12f;
        
        isAttacking = true;
        animator.SetBool("isAttacking", true);
        animator.SetFloat("AttackSpeed", attackSpeed);

        // Create a list to track who we've already damaged this swing
        List<GameObject> alreadyHit = new List<GameObject>();

        // Wait for the 'impact' moment of the animation
        yield return new WaitForSeconds(attackDuration * damageDelay); 

        // Detect everything in the sphere
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint.position, attackRange, enemyLayers);

        foreach (Collider col in hitColliders)
        {
            // Get the root object (to avoid hitting multiple colliders on the same enemy)
            GameObject enemyRoot = col.transform.root.gameObject;

            if (!alreadyHit.Contains(enemyRoot))
            {
                // Apply damage logic here
                enemyRoot.GetComponent<EnemyHealth>().TakeDamage(attackDamage);

                // Add to the list so we don't hit them again in this specific foreach loop
                alreadyHit.Add(enemyRoot);
            }
        }

        yield return new WaitForSeconds(attackDuration * (1-damageDelay));
        animator.SetBool("isAttacking", false);
    
        yield return new WaitForSeconds(attackCooldown);
        isAttacking = false;
    }

    // This lets you see the hit circle in the Scene view (very helpful!)
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
    }
}