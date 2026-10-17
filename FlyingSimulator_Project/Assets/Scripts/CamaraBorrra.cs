using UnityEngine;

public class AirCamera : MonoBehaviour
{
    [Header("Jugador")]
    public Transform player;
    public Rigidbody playerRb;

    [Header("Posición")]
    public float distance = 8f;
    public float height = 3f;
    public float lookAhead = 6f;

    [Header("Suavizado")]
    public float positionSmooth = 6f;
    public float rotationSmooth = 8f;

    [Header("Velocidad")]
    public float maxSpeed = 50f;
    public float maxFOVIncrease = 12f;

    [Header("Inclinación")]
    public float maxTilt = 8f;
    public float tiltSmooth = 5f;

    [Header("FOV")]
    public float normalFOV = 70f;

    private Camera cam;
    private Vector3 currentVelocity;
    private float currentTilt;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (cam != null)
            cam.fieldOfView = normalFOV;
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // --------------------------------
        // 1. VELOCIDAD DEL JUGADOR
        // --------------------------------

        Vector3 velocity = Vector3.zero;

        if (playerRb != null)
            velocity = playerRb.linearVelocity;

        float speed = velocity.magnitude;

        // Dirección de movimiento.
        Vector3 movementDirection;

        if (speed > 0.1f)
            movementDirection = velocity.normalized;
        else
            movementDirection = player.forward;


        // --------------------------------
        // 2. POSICIÓN DE LA CÁMARA
        // --------------------------------

        Vector3 targetPosition =
            player.position
            - movementDirection * distance
            + Vector3.up * height;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            positionSmooth * Time.deltaTime
        );


        // --------------------------------
        // 3. PUNTO HACIA EL QUE MIRAMOS
        // --------------------------------

        Vector3 targetLook =
            player.position +
            movementDirection * lookAhead;

        Vector3 lookDirection =
            targetLook - transform.position;

        if (lookDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSmooth * Time.deltaTime
            );
        }


        // --------------------------------
        // 4. INCLINACIÓN AL GIRAR
        // --------------------------------

        float turnAmount = Vector3.SignedAngle(
            player.forward,
            movementDirection,
            Vector3.up
        );

        float targetTilt = Mathf.Clamp(
            -turnAmount,
            -maxTilt,
            maxTilt
        );

        currentTilt = Mathf.Lerp(
            currentTilt,
            targetTilt,
            tiltSmooth * Time.deltaTime
        );

        transform.rotation *= Quaternion.Euler(
            0f,
            0f,
            currentTilt
        );


        // --------------------------------
        // 5. FOV SEGÚN VELOCIDAD
        // --------------------------------

        if (cam != null)
        {
            float speedPercent =
                Mathf.Clamp01(speed / maxSpeed);

            float targetFOV =
                normalFOV +
                maxFOVIncrease * speedPercent;

            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                targetFOV,
                5f * Time.deltaTime
            );
        }
    }
}
