using UnityEngine;
using UnityEngine.UI;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] Slider healthBar;
    
    // State
    private float currentHealth;
    [SerializeField] float maxHealth;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        healthBar.maxValue = maxHealth;
        SetHealth(maxHealth);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        healthBar.value = currentHealth;

        if (currentHealth <= 0) Die();
    }

    public void SetHealth(float health)
    {
        currentHealth = health;
        healthBar.value = currentHealth;
    }

    private void Die()
    {
        Debug.Log("Died");
        Destroy(gameObject);
    }
}
