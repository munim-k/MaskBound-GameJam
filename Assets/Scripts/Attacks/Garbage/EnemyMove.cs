using UnityEngine;
using Unity.Netcode;
using FMOD.Studio;

[RequireComponent(typeof(CharacterController))]
public class EnemyMove : NetworkBehaviour
{
    [Header("References")]
    private Transform player;
    private CharacterController controller;

    [Header("Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 1.5f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float targetUpdateRate = 0.5f; // How often to scan for players

    private Vector3 velocity;
    private float nextTargetUpdateTime;
    
    [Header("Animation")]
    [SerializeField] private Animator animator;
    private EventInstance walkInstance;

    public override void OnNetworkSpawn()
    {
        controller = GetComponent<CharacterController>();

        // Initialize FMOD walk event
        walkInstance = AudioManager.instance.CreateInstance(GetComponent<EnemyType>().GetWalkReference());

        if (GetComponent<EnemyType>().enemyType == EnemyType.Type.Gargoyle)
        {
            walkInstance.start();
        }
    }

    // This is the public function your EnemyAttack script was looking for
    public Transform GetPlayerTransform() => player;


    void Update()
    {
        if (!IsServer) return;

        // Periodically find the closest player
        if (Time.time >= nextTargetUpdateTime)
        {
            FindClosestPlayer();
            nextTargetUpdateTime = Time.time + targetUpdateRate;
        }

        if (player == null) return;

        MoveTowardsPlayer();
        ApplyGravity();
    }

    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (GameObject p in players)
        {
            // Optional: Only target players who are alive
            // if (p.GetComponent<PlayerHealth>().currentHealth.Value <= 0) continue;

            float distance = Vector3.Distance(transform.position, p.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = p.transform;
            }
        }

        player = bestTarget;
    }

    private void MoveTowardsPlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;

        if (direction.magnitude > stoppingDistance)
        {
            Vector3 moveDir = direction.normalized;
            controller.Move(moveDir * (moveSpeed * Time.deltaTime));

            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            animator.SetBool("isWalking", true);

            PLAYBACK_STATE playbackState;
            walkInstance.getPlaybackState(out playbackState);
            if (playbackState != PLAYBACK_STATE.PLAYING)
            {
                walkInstance.start();
            }
        }
        else
        {
            animator.SetBool("isWalking", false);
            PLAYBACK_STATE playbackState;
            walkInstance.getPlaybackState(out playbackState);
            if (playbackState == PLAYBACK_STATE.PLAYING && GetComponent<EnemyType>().enemyType != EnemyType.Type.Gargoyle)
            {
                walkInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
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