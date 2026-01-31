using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class EnemyMove : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    private CharacterController controller;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        
        // Safety check: Find player by tag if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
    }

    void Update()
    {
        if (player == null) return;

        MoveTowardsPlayer();
        ApplyGravity();
    }

    private void MoveTowardsPlayer()
    {
        // 1. Calculate direction (Target - Current)
        Vector3 direction = player.position - transform.position;

        // 2. Lock the Y axis
        direction.y = 0;

        // 3. Check distance so enemy doesn't stand inside the player
        if (direction.magnitude > stoppingDistance)
        {
            // Normalize to get a consistent speed
            Vector3 moveDir = direction.normalized;

            // 4. Move the enemy
            controller.Move(moveDir * (moveSpeed * Time.deltaTime));

            // 5. Rotate to face the player
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}