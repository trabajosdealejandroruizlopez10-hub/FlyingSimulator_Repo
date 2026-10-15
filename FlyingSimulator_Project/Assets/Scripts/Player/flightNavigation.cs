using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class AirplaneController : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] float FlySpeed = 5;
    [SerializeField] float YawAmount = 120;
    [SerializeField] float PitchAmount = 60;
    [SerializeField] GameObject Nave;
    [SerializeField] TextMeshProUGUI SpeedText;
    [SerializeField] float Speed = 0;

    [Header("Turbo")]
    [SerializeField] float MaxTurboLevel = 5;
    [SerializeField] float TurboDecayTime = 0.5f;

    [Header("Drift")]
    [Tooltip("Multiplicador del giro mientras derrapas (más giro = más facilidad)")]
    [SerializeField] float DriftYawMultiplier = 1.6f;
    [Tooltip("Giro mínimo y máximo dentro del drift según cuánto empujes el stick")]
    [SerializeField] float DriftControlMin = 0.5f;
    [SerializeField] float DriftControlMax = 1.5f;
    [Tooltip("Inclinación visual de la nave durante el drift")]
    [SerializeField] float DriftRoll = 45f;
    [SerializeField] float RollSmooth = 8f;
    [Tooltip("Cuánto hay que mover el stick para poder iniciar el drift")]
    [SerializeField] float DriftStartDeadzone = 0.2f;

    [Header("Mini-turbos del drift (estilo Mario Kart)")]
    [Tooltip("Segundos de drift necesarios para alcanzar cada nivel")]
    [SerializeField] float[] DriftTierTimes = { 0.8f, 1.6f, 2.4f };
    [Tooltip("Niveles de turbo que se añaden al soltar el drift en cada nivel")]
    [SerializeField] int[] TurboPerTier = { 1, 2, 4 };

    [Header("Feedback visual (opcional)")]
    [SerializeField] ParticleSystem[] DriftParticles;
    [SerializeField]
    Color[] TierColors =
    {
        new Color(0.2f, 0.6f, 1f),   // nivel 1 azul
        new Color(1f, 0.55f, 0.1f),  // nivel 2 naranja
        new Color(0.8f, 0.3f, 1f)    // nivel 3 morado
    };

    private Rigidbody rb;

    // Turbo
    private float turboLevel = 0;
    private float turboTimer = 0f;

    // Rotación
    private float Yaw;
    private float Pitch;
    private float currentRoll;

    // Input
    private Vector2 moveInput;
    private bool driftHeld = false;

    // Drift
    private bool drifting = false;
    private int driftDirection = 0;
    private float driftTime = 0f;
    private int driftTier = 0;

    private void Start()
    {
        rb = Nave.GetComponent<Rigidbody>();
        SetDriftParticles(0);
    }

    void Update()
    {
        HandleDrift();

        // =========================
        // MOVIMIENTO
        // =========================

        float yawInput = moveInput.x;
        float yawMultiplier = 1f;

        if (drifting)
        {
            // Igual que en Mario Kart: siempre giras hacia el lado del drift,
            // y el stick solo decide si cierras más o menos la curva.
            float control = (driftDirection == 1)
                ? Remap(moveInput.x, -1f, 1f, DriftControlMin, DriftControlMax)
                : Remap(moveInput.x, -1f, 1f, DriftControlMax, DriftControlMin);

            yawInput = driftDirection * control;
            yawMultiplier = DriftYawMultiplier;
        }

        // Yaw
        Yaw += yawInput * YawAmount * yawMultiplier * Time.deltaTime;

        // Pitch
        Pitch += -moveInput.y * PitchAmount * Time.deltaTime;
        Pitch = Mathf.Clamp(Pitch, -80f, 80f);

        // Roll visual (suavizado)
        float targetRoll = drifting
            ? -driftDirection * DriftRoll
            : Mathf.Lerp(0, 30, Mathf.Abs(moveInput.x)) * -Mathf.Sign(moveInput.x);

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, RollSmooth * Time.deltaTime);

        // Aplicar rotación
        transform.rotation = Quaternion.Euler(Pitch, Yaw, currentRoll);

        // =========================
        // TURBO (decaimiento)
        // =========================

        if (turboLevel > 0)
        {
            turboTimer -= Time.deltaTime;

            if (turboTimer <= 0)
            {
                turboLevel--;
                turboTimer = TurboDecayTime;
            }
        }

        // =========================
        // VELOCIDAD
        // =========================

        float currentSpeed = FlySpeed + turboLevel * 3f;
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    // =========================
    // DRIFT
    // =========================

    private void HandleDrift()
    {
        // Iniciar: botón pulsado + stick hacia un lado
        if (driftHeld && !drifting && Mathf.Abs(moveInput.x) > DriftStartDeadzone)
        {
            drifting = true;
            driftDirection = moveInput.x > 0 ? 1 : -1;
            driftTime = 0f;
            driftTier = 0;
        }

        if (!drifting) return;

        // Acumular tiempo y calcular nivel
        driftTime += Time.deltaTime;

        int newTier = 0;
        for (int i = 0; i < DriftTierTimes.Length; i++)
        {
            if (driftTime >= DriftTierTimes[i]) newTier = i + 1;
        }

        if (newTier != driftTier)
        {
            driftTier = newTier;
            SetDriftParticles(driftTier);
        }
    }

    private void EndDrift()
    {
        if (!drifting) return;

        // Recompensa según el nivel alcanzado
        if (driftTier > 0)
        {
            int index = Mathf.Min(driftTier - 1, TurboPerTier.Length - 1);
            AddTurbo(TurboPerTier[index]);
        }

        drifting = false;
        driftDirection = 0;
        driftTime = 0f;
        driftTier = 0;
        SetDriftParticles(0);
    }

    private void SetDriftParticles(int tier)
    {
        if (DriftParticles == null) return;

        foreach (var ps in DriftParticles)
        {
            if (ps == null) continue;

            if (tier <= 0)
            {
                ps.Stop();
                continue;
            }

            var main = ps.main;
            main.startColor = TierColors[Mathf.Min(tier - 1, TierColors.Length - 1)];
            if (!ps.isPlaying) ps.Play();
        }
    }

    private static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        return Mathf.Lerp(outMin, outMax, Mathf.InverseLerp(inMin, inMax, value));
    }

    public void AddTurbo()
    {
        AddTurbo(1);
    }

    public void AddTurbo(int amount)
    {
        turboLevel = Mathf.Clamp(turboLevel + amount, 0, MaxTurboLevel);

        // Reinicia el tiempo de descenso
        turboTimer = TurboDecayTime;

        Debug.Log("Turbo Level: " + turboLevel);
    }

    public float GetTurboLevel()
    {
        return turboLevel;
    }

    public bool IsDrifting()
    {
        return drifting;
    }

    public int GetDriftTier()
    {
        return driftTier;
    }


    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnDrift(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            driftHeld = true;
        }
        else if (context.canceled)
        {
            driftHeld = false;
            EndDrift();
        }
    }

    private void FixedUpdate()
    {
        // Actualizar la velocidad en el UI
        if (SpeedText != null)
            SpeedText.text = "Speed: " + (FlySpeed + turboLevel * 3f).ToString("F0") + " GB/S";
    }
}