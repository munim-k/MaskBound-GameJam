using UnityEngine;

public class EnemyAnimationRelay : MonoBehaviour
{
    // Reference to the main script on the parent
    private EnemyAttack _parentCombat;

    void Awake()
    {
        // Find the EnemyAttack script on the PARENT object
        _parentCombat = GetComponentInParent<EnemyAttack>();
    }

    // These match the function names in your Animation Event
    public void AE_Die()
    {
        Debug.Log("death AE relay");
        if (_parentCombat != null) _parentCombat.AE_Die();
    }
}
