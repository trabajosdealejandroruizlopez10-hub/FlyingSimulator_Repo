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
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        sprintAction = new InputAction("Sprint", InputActionType.Button);
        sprintAction.AddBinding("<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        jumpBufferCounter = jumpBufferTime;
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        return forward * input.y + right * input.x;
    }

    #endregion

    #region Colisiones

    private void OnCollisionEnter(Collision collision) => ProcessCollision(collision);
    private void OnCollisionStay(Colision collision) => ProcessCollision(collision);

    private void ProcessCollision(Collision collision)
    {
        for(int i = 0; i < collision.contactCount; i++)
        {
            Vector3 n = collision.GetContact(i).normal;
            if (n.y < minGroundDot && n.y > -0,5)
                pendingWalls.Add(n);
        }
    }

    private void ConsumeContacts()
    {
        IsGrounded = false;
        groundNormal = Vector3.up;

        if (groundIgnoreTimer <= 0f && Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask,
                                                            QueryTriggerInteraction.Ignore))
        {
            IdGrounded = true;

            if (Physics.Raycast(groundCheck.position + Vector3.up * 0,1f, Vector3.down,
                                out RaycastHit hit, groundCheckRadius + 0.3f, groundMask,
                                QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y >= minGroundDot) groundNormal = hit.normal;
                else IsGrounded = false;
            }
        }

    activeWalls.Clear();
    activeWalls.AddRange(pendingWalls);
    pendingWalls.Clear();

    #endregion

    #region Lógica

    private void UpdateTimers(float dt)
{
    groundIgnoreTimer -= dt;
    jumpBufferCounter -= dt;

    if (IsGrounded)
    {
        coyoteCounter = coyoteiTime;
        airJumpsUsed = 0;
    }
    else
    {
        coyoteCounter -= dt;
    }
}
    private void HandleJump(ref Vector3 vel)
{
    if (jumpBufferCounter <= 0f) return;

    if (coyoteCounter > 0f)
    {
        float height = IsRunning ? runJumpHeight : walkJumpHeight;
        vel.y = JumpSpeed(height);

        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        groundIgnoreTimer = 0.1f;
        IsGrounded = false;
    }
    else if (airJumpsUsed < maxAirJumps)
    {
        vel.y = JumpSpeed(doubleJumpHeight);

        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        if (horizontal.magnitude > 0.1f)
        {
            horizontal += horiozontal.normalized * doubleJumpForwardBoost;
            horizontal = Vector3.ClampMagnitude(horizontal, maxAirSpeed);
            vel.x = horizontal.x;
            vel.z = horizontal.z;
        }

        airJumpsUsed++;
        jumpBufferCounter = 0f;
    }
}

private float JumpSpeed(float height)
{
    return Mathf.Sqrt(height * -2f * gravity);
}

private void HandleMovement(ref Vector3 vel, Vector3 moveDir, bool hasInput, float dt)
{
    float targetSpeed = IsRunning ? runSpeed : walkSpeed;

    if (IsGrounded)
    {
        Vector3 target = Vector3.ProjectOnPlane(moveDir * targetSpeed, groundNormal);
        vel = Vector3.MoveTowards(vel, target, groundAcceleration * dt);
    }
    else if (hasInput && airControl > 0f)
    {
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        float airTargetSpeed = Mathf.Max(horizontal.magnitude, targetSpeed);
        Vector3 target = moveDir * Mathf.Min(airTargetSpeed, maxAirSpeed);

        horizontal = Vector3.MoveTowards(horizontal, target, airAcceleration * airControl * dt);
        vel.x = horizontal.x;
        vel.z = horizontal.z;
    }

     private void ApplyGravity(ref Vector3 vel, float dt)
{
    if (IsGrounded) return;

    vel.y += gravity * dt;
    vel.y = Mathf.Max(vel.y, maxFallSpeed);
}

private void RemoveVelocityIntoWalls(ref Vector3 vel)
{
    for (int i = 0; i < activeWalls.Count; i++)
    {
        Vector3 flat = new Vector3(activeWalls[i].x, 0f, activeWalls[i].z);
        if (flat.sqrMagnitude < 0.0001f) continue;
        flat.Normalize();

        float into = vel.x * flat.x + vel.z * flat.z;
        if (into < 0f)
        {
            vel.x -= flat.x * into;
            vel.z -= flat.z * into;
        }
    }
}

private void HandleRotation(Vector3 moveDir, Vector3 vel, float dt)
{
    Vector3 faceDir;

    if (IsGrounded)
    {
        faceDir = moveDir;
    }
    else
    {
        faceDir = new Vector3(vel.x, 0f, vel.z);
        if (faceDir.sqrMagnitude < 0.25f) return;
    }

    if (faceDir.sqrMagnitude < 0.001f) return;

    Quaternion targetRot = Quaternion.LookRotation(faceDir, Vector3.up);
    rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, rotationSpeed * dt));
}

#endregion


    private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
}
