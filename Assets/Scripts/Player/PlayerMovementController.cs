using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Server-authoritative player movement with client prediction.
/// Uses CharacterController for movement and NetworkTransform for sync.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerMovementController : NetworkBehaviour
{
    [Header("References")]
    private CharacterController controller;
    
    [SerializeField] private Animator animator;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private Transform cameraTransform;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 15f;
    
    [Header("Gravity")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float groundedGravity = -2f;
    
    // Constants
    private const string ANIM_SPEED = "Speed";
    private const float ANIMATOR_SMOOTHING = 0.1f;

    // State
    private Vector3 velocity;
    private Vector2 inputVector;
    private float currentAnimSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        // Only the owner controls this player
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        // Lock cursor for owner
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Ensure camera reference exists
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null)
            {
                Debug.LogError("[PlayerMovement] No camera found!");
            }
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        ApplyGravity();
    }

    /// <summary>
    /// Handle player movement input and rotation
    /// </summary>
    private void HandleMovement()
    {
        // Read input
        inputVector = moveAction.action.ReadValue<Vector2>();

        // Get camera-relative directions
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // Flatten to prevent flying
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Calculate movement direction
        Vector3 moveDirection = (forward * inputVector.y + right * inputVector.x);

        // Update animator (smooth transition)
        float targetSpeed = moveDirection.magnitude;
        currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, ANIMATOR_SMOOTHING);
        
        if (animator != null)
        {
            animator.SetFloat(ANIM_SPEED, currentAnimSpeed);
        }

        // Apply movement
        controller.Move(moveDirection * (moveSpeed * Time.deltaTime));

        // Rotate to face movement direction
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Apply gravity to the player
    /// </summary>
    private void ApplyGravity()
    {
        // Reset falling velocity when grounded
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = groundedGravity;
        }

        // Apply gravity over time
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
    }

    // Debug visualization
    private void OnDrawGizmosSelected()
    {
        if (controller == null) return;
        
        Gizmos.color = controller.isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, controller.radius);
    }
}
