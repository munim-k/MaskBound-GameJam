using UnityEngine;
using Unity.Netcode;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// OLD Input System version (Project Settings -> Input Manager).
/// Required axes/buttons:
/// - Axes: "Horizontal", "Vertical", "Mouse X", "Mouse Y"
/// - Button: "Jump"
/// Optional:
/// - Sprint key: LeftShift (hardcoded)
/// - Slide key: LeftControl (hardcoded)
///
/// Cinemachine:
/// - Create CameraTarget child, set Cinemachine VCam Follow & LookAt to CameraTarget.
/// - Assign cameraTarget in inspector.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FluidThirdPersonController_OldInput : NetworkBehaviour
{
    [Header("References")]
    public Transform cameraTarget;           // Cinemachine follows/looks at this
    public Camera mainCamera;                // If null, uses Camera.main

    [SerializeField] private Animator animator;

    [Header("Move")]
    public float sprintSpeed = 6.5f;
    public float acceleration = 18f;
    public float deceleration = 22f;
    [Range(0.02f, 0.35f)] public float rotationSmoothTime = 0.10f;
    [Range(0f, 1f)] public float airControl = 0.65f;

    [Header("Gravity / Jump")]
    public float gravity = -20f;
    public float jumpHeight = 1.35f;
    [Range(1, 4)] public int maxJumps = 2;    // 2 = double jump
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;

    [Header("Ground Check")]
    public LayerMask groundLayers = ~0;
    public float groundedOffset = -0.08f;
    public float groundedRadius = 0.28f;

    [Header("Slide")]
    public float slideDuration = 0.65f;
    public float slideImpulseSpeed = 10.5f;
    public float slideFriction = 14f;
    [Range(0.35f, 0.9f)] public float slideHeightMultiplier = 0.55f;
    public float minSpeedToSlide = 2.0f;
    public bool lockSlideDirection = true;

    [Header("Camera Feel (Cinemachine)")]
    [Range(0f, 1f)] public float cameraAutoPanStrength = 0.6f;
    public float cameraAutoPanSpeed = 12f;
    public float cameraPanMinInput = 0.15f;

    [Tooltip("Mouse sensitivity multiplier for OLD input.")]
    public float mouseSensitivity = 2.0f;

    public float topClamp = 70f;
    public float bottomClamp = -30f;

    [Header("Keys (Old Input)")]
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode slideKey = KeyCode.LeftControl;

    [Header("Audio")]
    private EventInstance slideEvent;

    // runtime
    private CharacterController _cc;

    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private bool _jumpPressed;
    private bool _sprintHeld;
    private bool _slidePressed;

    private float _speed;
    private float _rotationVelocity;
    private float _verticalVelocity;

    private float _targetYaw;
    private float _targetPitch;

    private bool _grounded;
    private bool _landAudioPlayed;
    private float _lastGroundedTime;
    private float _lastJumpPressedTime;
    private int _jumpsRemaining;

    // slide
    private bool _isSliding;
    private float _slideTimer;
    private float _slideCurrentSpeed;
    private Vector3 _slideDir;
    private float _originalHeight;
    private Vector3 _originalCenter;


    // Animations
    private static readonly int _animSpeed = Animator.StringToHash("Speed");
    private static readonly int _animGrounded = Animator.StringToHash("Grounded");
    private static readonly int _animVerticalVelocity = Animator.StringToHash("VerticalVelocity");
    private static readonly int _animJump = Animator.StringToHash("Jump");
    private static readonly int _animSliding = Animator.StringToHash("IsSliding");

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _originalHeight = _cc.height;
        _originalCenter = _cc.center;

        if (!mainCamera) mainCamera = Camera.main;
    }

    private void Start()
    {
        if (cameraTarget)
        {
            var e = cameraTarget.rotation.eulerAngles;
            _targetYaw = e.y;
            _targetPitch = 0f; // start level; you can also read e.x if you want
        }

        _jumpsRemaining = maxJumps;
        
        if(IsOwner)
        {
            slideEvent = AudioManager.instance.CreateInstance(FMODEvents.instance.playerSlide);
            _landAudioPlayed = true;
        }

    }

    private void Update()
    {
        if (!IsOwner) return;

        ReadInputs();

        GroundCheck();

        HandleSlide();
        HandleJumpAndGravity();
        HandleMovement();

        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Use _speed for the blend tree (0 = Idle, 4 = Walk, 6.5 = Sprint)
        animator.SetFloat(_animSpeed, _speed);
        
        // Vertical velocity helps transitions for falling vs jumping
        animator.SetFloat(_animVerticalVelocity, _verticalVelocity);
        
        // Boolean states
        animator.SetBool(_animGrounded, _grounded);
        animator.SetBool(_animSliding, _isSliding);
    }

    private void LateUpdate()
    {
        HandleCamera();
    }

    // --------------------
    // INPUT (OLD)
    // --------------------
    private void ReadInputs()
    {
        _moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

        // Mouse (old input): already per-frame delta, so DO NOT multiply by deltaTime
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");
        _lookInput = new Vector2(mx, -my);

        if (Input.GetButtonDown("Jump"))
        {
            _jumpPressed = true;
            _lastJumpPressedTime = Time.time;
        }

        _sprintHeld = Input.GetKey(sprintKey);

        if (Input.GetKeyDown(slideKey))
        {
            _slidePressed = true;
        }
    }

    // --------------------
    // GROUND
    // --------------------
    private void GroundCheck()
    {
        Vector3 spherePos = new Vector3(transform.position.x, transform.position.y + groundedOffset, transform.position.z);
        _grounded = Physics.CheckSphere(spherePos, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);

        if (_grounded)
        {
            if(!_landAudioPlayed)
            {
                if(IsOwner){
                    AudioManager.instance.PlayOneShot(FMODEvents.instance.playerLand, transform.position);
                    _landAudioPlayed = true;
                }
            }

            _lastGroundedTime = Time.time;
            _jumpsRemaining = maxJumps;

            if (_verticalVelocity < 0f)
                _verticalVelocity = -2f;
        }
    }

    // --------------------
    // MOVE
    // --------------------
    private void HandleMovement()
    {
        // Slide handles horizontal while active
        if (_isSliding)
        {
            _cc.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
            return;
        }

        float targetMaxSpeed = sprintSpeed;
        float inputMag = Mathf.Clamp01(_moveInput.magnitude);
        float desiredSpeed = targetMaxSpeed * inputMag;

        float rate = (desiredSpeed > _speed) ? acceleration : deceleration;
        if (!_grounded) rate *= airControl;

        _speed = Mathf.MoveTowards(_speed, desiredSpeed, rate * Time.deltaTime);

        // camera-relative direction
        Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
        Vector3 moveDirWorld = Vector3.zero;

        if (inputDir.sqrMagnitude > 0.0001f && mainCamera)
        {
            float camYaw = mainCamera.transform.eulerAngles.y;
            Vector3 camForward = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
            Vector3 camRight = Quaternion.Euler(0f, camYaw, 0f) * Vector3.right;
            moveDirWorld = (camForward * inputDir.z + camRight * inputDir.x).normalized;

            // rotate player toward move direction
            float targetRot = Mathf.Atan2(moveDirWorld.x, moveDirWorld.z) * Mathf.Rad2Deg;
            float rot = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRot, ref _rotationVelocity, rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, rot, 0f);
        }

        Vector3 horizontal = moveDirWorld * (_speed * Time.deltaTime);
        Vector3 vertical = new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime;

        _cc.Move(horizontal + vertical);
    }

    // --------------------
    // JUMP / GRAVITY
    // --------------------
    private void HandleJumpAndGravity()
    {
        _verticalVelocity += gravity * Time.deltaTime;

        bool hasCoyote = (Time.time - _lastGroundedTime) <= coyoteTime;
        bool hasBuffer = (Time.time - _lastJumpPressedTime) <= jumpBuffer;

        if (hasBuffer && (_grounded || hasCoyote || _jumpsRemaining > 0))
        {
            DoJump();
            _lastJumpPressedTime = -999f; // consume
        }

        _jumpPressed = false;
    }

    private void DoJump()
    {
        if (_grounded || (Time.time - _lastGroundedTime) <= coyoteTime)
        {
            _jumpsRemaining = Mathf.Max(0, maxJumps - 1);
        }
        else
        {
            _jumpsRemaining = Mathf.Max(0, _jumpsRemaining - 1);
        }

        if (_isSliding) StopSlide();

        if (animator != null) animator.SetTrigger(_animJump);

        if(IsOwner){
            AudioManager.instance.PlayOneShot(FMODEvents.instance.playerJump, transform.position);
            _landAudioPlayed = false;
        }
        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    // --------------------
    // SLIDE
    // --------------------
    private void HandleSlide()
    {
        if (_slidePressed && !_isSliding && _grounded)
        {
            float horizSpeed = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
            if (horizSpeed >= minSpeedToSlide)
                StartSlide(horizSpeed);
        }

        _slidePressed = false;

        if (!_isSliding) return;

        _slideTimer -= Time.deltaTime;
        if (_slideTimer <= 0f)
        {
            StopSlide();
            return;
        }

        _slideCurrentSpeed = Mathf.MoveTowards(_slideCurrentSpeed, 0f, slideFriction * Time.deltaTime * slideImpulseSpeed);

        if (!lockSlideDirection && _moveInput.magnitude > 0.1f && mainCamera)
        {
            Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
            float camYaw = mainCamera.transform.eulerAngles.y;
            Vector3 desired = (Quaternion.Euler(0f, camYaw, 0f) * inputDir).normalized;
            _slideDir = Vector3.Slerp(_slideDir, desired, 10f * Time.deltaTime);
        }

        _cc.Move(_slideDir * (_slideCurrentSpeed * Time.deltaTime));
    }

    private void StartSlide(float currentHorizSpeed)
    {
        _isSliding = true;

        PLAYBACK_STATE slideState;
        slideEvent.getPlaybackState(out slideState);
        if (slideState != PLAYBACK_STATE.PLAYING && slideEvent.isValid())
            slideEvent.start();
        else if(slideEvent.isValid()){
            slideEvent.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); // restart
            slideEvent.start();
        }

        _slideTimer = slideDuration;

        Vector3 horizVel = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z);
        _slideDir = (horizVel.sqrMagnitude < 0.01f) ? transform.forward : horizVel.normalized;

        _slideCurrentSpeed = Mathf.Max(slideImpulseSpeed, currentHorizSpeed);

        float newHeight = _originalHeight * slideHeightMultiplier;
        _cc.height = newHeight;
        _cc.center = new Vector3(_originalCenter.x, newHeight * 0.5f, _originalCenter.z);
    }

    private void StopSlide()
    {
        _isSliding = false;
        if(slideEvent.isValid())
            slideEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

        _cc.height = _originalHeight;
        _cc.center = _originalCenter;
    }

    // --------------------
    // CAMERA (Cinemachine target yaw/pitch)
    // --------------------
    private void HandleCamera()
    {
        if (!cameraTarget) return;

        // manual look
        if (_lookInput.sqrMagnitude > 0.0001f)
        {
            _targetYaw += _lookInput.x * mouseSensitivity;
            _targetPitch += _lookInput.y * mouseSensitivity;
        }

        // auto-pan yaw toward movement direction
        if (cameraAutoPanStrength > 0f && _moveInput.magnitude >= cameraPanMinInput && mainCamera)
        {
            Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;
            float camYaw = mainCamera.transform.eulerAngles.y;
            Vector3 moveDirWorld = (Quaternion.Euler(0f, camYaw, 0f) * inputDir).normalized;

            float desiredYaw = Mathf.Atan2(moveDirWorld.x, moveDirWorld.z) * Mathf.Rad2Deg;

            float t = 1f - Mathf.Exp(-cameraAutoPanSpeed * Time.deltaTime);
            _targetYaw = Mathf.LerpAngle(_targetYaw, desiredYaw, t * cameraAutoPanStrength);
        }

        _targetPitch = ClampPitch(_targetPitch, bottomClamp, topClamp);

        cameraTarget.rotation = Quaternion.Euler(_targetPitch, _targetYaw, 0f);
    }


    private static float ClampPitch(float pitch, float min, float max)
    {
        // pitch is used as signed degrees, clamp directly
        pitch = Mathf.Repeat(pitch + 180f, 360f) - 180f;
        return Mathf.Clamp(pitch, min, max);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _grounded ? new Color(0, 1, 0, 0.35f) : new Color(1, 0, 0, 0.35f);
        Vector3 spherePos = new Vector3(transform.position.x, transform.position.y + groundedOffset, transform.position.z);
        Gizmos.DrawSphere(spherePos, groundedRadius);
    }
}
