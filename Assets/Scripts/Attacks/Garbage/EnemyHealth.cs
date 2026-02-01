using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class EnemyHealth : NetworkBehaviour
{
    [SerializeField] private Slider healthBar;
    [SerializeField] private float maxHealth = 100f;
    
    [SerializeField] Animator animator;

    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // 1. Initialize UI values
        healthBar.maxValue = maxHealth;
        
        // 2. Force an immediate UI update for late-joiners
        updateHealthUI(0, currentHealth.Value);

        // 3. Subscribe to future changes
        currentHealth.OnValueChanged += updateHealthUI;

        // 4. Server-only: Set the starting health
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
    }

    public override void OnNetworkDespawn()
    {
        // Always unsubscribe to prevent memory leaks/errors
        currentHealth.OnValueChanged -= updateHealthUI;
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer)
        {
            updateHealthServerRpc(damage);
        }
        else
        {
            ApplyDamage(damage);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void updateHealthServerRpc(float damage)
    {
        ApplyDamage(damage);
    }

    private void ApplyDamage(float damage)
    {
        currentHealth.Value -= damage;
        if (currentHealth.Value <= 0)
        {
            Debug.Log("health now 0. triggering animation");
            // Use NetworkObject.Despawn for networked objects instead of Destroy
            animator.SetTrigger("death");
            AudioManager.instance.PlayOneShot(GetComponent<EnemyType>().GetDeathReference(), transform.position);
        }
    }

    // The delegate for OnValueChanged requires these parameters
    private void updateHealthUI(float previousValue, float newValue)
    {
        Debug.Log($"Updating UI: {newValue}");
        healthBar.value = newValue;
    }
}