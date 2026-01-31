using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class BasicPlayerMovement : MonoBehaviour
{
    [Header("References")]
    private CharacterController controller;

    [SerializeField] private Animator animator;
    [SerializeField] private InputActionReference moveAction;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -9.81f;
        
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform; // Drag 'Main Camera' here
    [SerializeField] private float rotationSpeed = 15f;
    
    private Vector3 velocity;
    private Vector2 inputVector;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // 1. Lock the cursor to the center of the screen
        Cursor.lockState = CursorLockMode.Locked;

        // 2. Hide the cursor so it doesn't stay on screen
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMovement();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // 1. Get Input
        inputVector = moveAction.action.ReadValue<Vector2>();

        // 2. Get Camera Directions
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // 3. Flatten Directions (Ignore Y axis so player doesn't fly)
        forward.y = 0f;
        right.y = 0f;

        // 4. Calculate Final Direction
        Vector3 move = (forward.normalized * inputVector.y + right.normalized * inputVector.x);

        if (move.magnitude > 0f)
        {
            animator.SetFloat("Speed", move.magnitude);
        }
        else
        {
            animator.SetFloat("Speed", 0f);
        }

        // 5. Move the Controller
        controller.Move(move * (moveSpeed * Time.deltaTime));

        // 6. Rotate to Face Movement
        if (move != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        // 4. Check if grounded to reset falling velocity
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small downward force to keep grounded
        }

        // 5. Calculate and apply gravity over time
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void OnEnable() => moveAction.action.Enable();
    private void OnDisable() => moveAction.action.Disable();
}