using UnityEngine;

public class AirCamera : MonoBehaviour
{
    [Header("Jugador")]
    public Transform player;
    public Rigidbody playerRb;
    [Tooltip("Se busca solo en el jugador si lo dejas vacío")]
    public AirplaneController airplane;

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

        if (airplane == null && player != null)
            airplane = player.GetComponentInParent<AirplaneController>();
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // Punto y orientación que sigue la cámara.
        // Con el controlador: la nave SIN el desplazamiento ni la pirueta del flip.
        Vector3 followPosition = player.position;
        Vector3 shipForward = player.forward;

        if (airplane != null)
        {
            followPosition = airplane.GetCameraTargetPosition();
            shipForward = airplane.GetCameraTargetRotation() * Vector3.forward;
        }

        Vector3 velocity = Vector3.zero;

        if (playerRb != null)
            velocity = playerRb.linearVelocity;

        float speed = velocity.magnitude;

        // Dirección de movimiento.
        Vector3 movementDirection;

        if (speed > 0.1f)
            movementDirection = velocity.normalized;
        else
            movementDirection = shipForward;


        Vector3 targetPosition =
            followPosition
            - movementDirection * distance
            + Vector3.up * height;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            positionSmooth * Time.deltaTime
        );


        Vector3 targetLook =
            followPosition +
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


        float turnAmount = Vector3.SignedAngle(
            shipForward,
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