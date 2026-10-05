using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System;


public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Floor Movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6.5f;
    [SerializeField] private float groundAcceleration = 45f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField, Range(0f, 80f)] private float maxSlopeAngle = 50f;

    [Header("Air Movement")]
    [SerializeField, Range(0f, 1f)] private float airControl = 30f;
    [SerializeField] private float airAcceleration = 30f;
    [SerializeField] private float maxAirSpeed = 9f;

    [Header("Salto")]
    [SerializeField] private float walkJumpHeight = 1.2f;
    [SerializeField] private float runJumpHeight = 1.8f;
    [SerializeField] private float maxAirJumps = 1;
    [SerializeField] private float doubeJumpHeight = 1.4f;
    [SerializeField] private float doubeJumpForwardBoost = 1.5f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime= 0.12f;

    [Header("Gravedad")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float maxFallSpeed = -40f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundMask = 0;

    public bool IsGrounded { get; private set; }
    public bool IsRunning { get; private set; }
    public float HorizontalSpeed
    {
        get { Vector3 v = RenderBuffer.linearVelocity; return new Vector2(v.x, v.z).magnitude; }
    }
    public float VerticalVelocity => RenderBuffer.linearVelocity.y;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;


    private readonly List<Vector3> pandingWalls = new List<Vector3>(4);
    private readonly List<Vector3> activeWalls = new List<Vector3>(4);
    private Vector3 groundNormal = Vector3.up;
    private float minGroundDot;

    #region Unity
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.sleepThreshold = 0f;


        PhysicsMaterial noFriction = new PhysicsMaterial("PlayerNoFriction")
        {
            dynamicFriction = 0,
            staticFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };

        capsule.sharedMaterial = noFriction;

        minGroundDot = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);

        if (groundCheck == null)
        {
            enabled = false;
            return;
        }

        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;

        CreateInputActions();
    }

    private void OnValidate()
    {
        minGroundDot = Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        jumpAction.performed += OnJumpPerformed;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
        sprintAction?.Dispose();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        ConsumeContacts();
        UpdateTimers(dt);

        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        bool hasInput = input.sqrMagnitude > 0.01f;
        IsRunning = hasInput && sprintAction.IsPressed();
        Vector3 moveDir = GetCameraRelativeDirection(input);

        Vector3 vel = rb.linearVelocity;

        HandleJump(ref vel);
        HandleMovement(ref vel, moveDir, hasInput, dt);
        ApplyGravity(ref vel, dt);
        RemoveVelocityIntoWalls(ref vel);

        rb.linearVelocity = vel;

        HandleRotation(moveDir, vel, dt);
    }

    #endregion

    #region Input

    private void CreateInputActions()
    {
        moveAction = new
            InputAction("Move")
    }
    Vector2 move;
    bool jumpRequested;

    private void Start()
    {
        if (cam == null)
            cam = Camera.main.transform;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            jumpRequested = true;
    }


    private void FixedUpdate()
    {
        Vector3 camForward = cam.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cam.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDir = camForward * move.y + camRight * move.x;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);





        Vector3 targetVelocity = moveDir * speed;
        Vector3 velocityChange = targetVelocity - rb.linearVelocity;
        velocityChange.y = 0;
        velocityChange = Vector3.ClampMagnitude(velocityChange, maxForce);
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
        
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
        }
        if (jumpRequested && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        jumpRequested = false;
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);
    }

}
