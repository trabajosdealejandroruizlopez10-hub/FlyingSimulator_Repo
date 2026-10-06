using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerController))]
public class GrapplingHook : MonoBehaviour
{
    private enum State { Idle, Pulling, Hanging }

    [Header("Apuntado")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float maxDistance = 25f;
    [SerializeField] private LayerMask hookMask = ~0;

    [Header("Movimiento")]
    [SerializeField] private float pullSpeed = 22f;
    [SerializeField] private float pullAcceleration = 60f;
    [SerializeField] private float arriveDistance = 0.4f;
    [SerializeField] private float maxPullTime = 3f;
    [SerializeField] private float cooldown = 0.3f;

    [Header("Al llegar")]
    [SerializeField] private bool stickOnWallsAndCeilings = true;
    [SerializeField, Range(0f, 1f)] private float floorNormalY = 0.7f;
    [SerializeField, Range(0f, 1f)] private float momentumKept = 0.4f;

    [Header("Soltarse con Salto")]
    [SerializeField] private float jumpOffUpSpeed = 9f;
    [SerializeField] private float jumpOffAwaySpeed = 4f;

    [Header("Cuerda")]
    [SerializeField] private Transform ropeOrigin;
    [SerializeField] private Material ropeMaterial;
    [SerializeField] private float ropeWidth = 0.05f;
    [SerializeField] private Color ropeColor = Color.white;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private PlayerController player;
    private LineRenderer line;

    private State state = State.Idle;
    private Vector3 hitPoint;
    private Vector3 hitNormal;
    private Vector3 hangPoint;

    private float currentSpeed;
    private float stateTimer;
    private float cooldownTimer;
    private float bestDistance;
    private float stuckTimer;

    private readonly RaycastHit[] hits = new RaycastHit[8];

    public bool IsHooked => state != State.Idle;

    #region Unity

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        player = GetComponent<PlayerController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        SetupLine();
    }

    private void OnEnable()
    {
        player.JumpPressed += OnJumpPressed;
    }

    private void OnDisable()
    {
        player.JumpPressed -= OnJumpPressed;

        if (state != State.Idle)
        {
            state = State.Idle;
            player.MovementLocked = false;
            line.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        cooldownTimer -= dt;

        if (state == State.Idle) return;

        Vector3 center = capsule.bounds.center;
        Vector3 toTarget = hangPoint - center;
        float dist = toTarget.magnitude;

        if (state == State.Pulling)
        {
            stateTimer += dt;

            if (dist <= arriveDistance)
            {
                Arrive();
                return;
            }

            if (dist < bestDistance - 0.05f)
            {
                bestDistance = dist;
                stuckTimer = 0f;
            }
            else
            {
                stuckTimer += dt;
            }

            if (stuckTimer > 0.4f || stateTimer > maxPullTime)
            {
                Release(false);
                return;
            }

            currentSpeed = Mathf.MoveTowards(currentSpeed, pullSpeed, pullAcceleration * dt);
            float speed = Mathf.Min(currentSpeed, dist / dt); 
            rb.linearVelocity = toTarget.normalized * speed;

            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.01f)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(flat), 12f * dt));
        }
        else
        {
            rb.linearVelocity = Vector3.ClampMagnitude(toTarget * 15f, 10f);

            Vector3 facing = new Vector3(-hitNormal.x, 0f, -hitNormal.z);
            if (facing.sqrMagnitude > 0.01f)
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(facing), 10f * dt));
        }
    }

    private void LateUpdate()
    {
        if (state == State.Idle) return;

        Vector3 start = ropeOrigin != null ? ropeOrigin.position
        : capsule.bounds.center + Vector3.up * 0.5f;
        line.SetPosition(0, start);
        line.SetPosition(1, hitPoint);
    }

    #endregion

    #region Input (Unity Events)

    public void OnGrapple(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;

        if (state == State.Idle) Fire();
        else Release(false);
    }

    private void OnJumpPressed()
    {
        if (state == State.Hanging)
            Release(true);
    }

    #endregion

    #region Gancho

    private void Fire()
    {
        if (cooldownTimer > 0f || cameraTransform == null) return;

        Vector3 origin = cameraTransform.position;
        float camToPlayer = Vector3.Distance(origin, capsule.bounds.center);

        int count = Physics.RaycastNonAlloc(origin, cameraTransform.forward, hits,
        maxDistance + camToPlayer, hookMask,
        QueryTriggerInteraction.Ignore);

        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(transform)) continue;
            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                best = i;
            }
        }

        if (best < 0) return;

        RaycastHit hit = hits[best];
        if (Vector3.Distance(capsule.bounds.center, hit.point) > maxDistance) return;

        hitPoint = hit.point;
        hitNormal = hit.normal;

        float reach = capsule.radius +
        (capsule.height * 0.5f - capsule.radius) * Mathf.Abs(hitNormal.y);
        hangPoint = hitPoint + hitNormal * (reach + 0.05f);

        state = State.Pulling;
        stateTimer = 0f;
        stuckTimer = 0f;
        bestDistance = Vector3.Distance(capsule.bounds.center, hangPoint);
        currentSpeed = Mathf.Max(rb.linearVelocity.magnitude, pullSpeed * 0.3f);

        player.MovementLocked = true;
        line.enabled = true;
    }

    private void Arrive()
    {
        bool isPlatform = hitNormal.y >= floorNormalY;

        if (isPlatform || !stickOnWallsAndCeilings)
        {
            Release(false);
            return;
        }

        state = State.Hanging;
        rb.linearVelocity = Vector3.zero;
    }

    private void Release(bool withJump)
    {
        Vector3 vel = rb.linearVelocity;

        state = State.Idle;
        cooldownTimer = cooldown;
        line.enabled = false;
        player.MovementLocked = false;

        if (withJump)
        {
            player.ConsumeJumpBuffer();
            player.ResetAirJumps(); 

            if (hitNormal.y < -0.5f)
            {
                vel = Vector3.zero; 
            }
            else
            {
                Vector3 away = new Vector3(hitNormal.x, 0f, hitNormal.z).normalized;
                vel = away * jumpOffAwaySpeed + Vector3.up * jumpOffUpSpeed;
            }
        }
        else
        {
            vel *= momentumKept;
            if (hitNormal.y >= floorNormalY) vel.y = 0f; 
        }

        rb.linearVelocity = vel;
    }

    private void SetupLine()
    {
        line = GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = ropeWidth;
        line.startColor = line.endColor = ropeColor;

        if (ropeMaterial != null)
        {
            line.material = ropeMaterial;
        }
        else
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null) line.material = new Material(shader);
        }

        line.enabled = false;
    }

    #endregion
}
