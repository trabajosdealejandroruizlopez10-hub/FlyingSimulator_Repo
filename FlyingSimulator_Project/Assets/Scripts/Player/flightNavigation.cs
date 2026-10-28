using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class AirplaneController : MonoBehaviour
{
    

    public enum FlipDirection { Up, Down, Left, Right }

    [Serializable]
    public class FlipSettings
    {
        [Tooltip("Distancia total que se desplaza la nave (unidades)")]
        public float Distance = 6f;
        [Tooltip("Velocidad del desplazamiento (unidades/segundo)")]
        public float Speed = 30f;
        [Tooltip("Grados que gira la nave durante el flip (360 = vuelta completa)")]
        public float Rotation = 360f;
        [Tooltip("Desplazamiento extra hacia delante durante el flip (unidades). Pequeño = flip casi recto")]
        public float ForwardDistance = 0f;

        public FlipSettings(float distance, float speed, float rotation, float forwardDistance)
        {
            Distance = distance;
            Speed = speed;
            Rotation = rotation;
            ForwardDistance = forwardDistance;
        }
    }

    // cosikas

    [Header("Movimiento")]
    [SerializeField] float FlySpeed = 5;
    [SerializeField] float YawAmount = 120;
    [SerializeField] float PitchAmount = 60;
    [SerializeField] GameObject Nave;
    [SerializeField] TextMeshProUGUI SpeedText;
    [SerializeField] float Speed = 0;

    [Header("Controles")]
    [Tooltip("Invierte el eje vertical del pilotaje (estilo avión: empujar hacia arriba = morro hacia abajo)")]
    [SerializeField] bool InvertVerticalAxis = false;
    [Tooltip("Invierte arriba/abajo en las piruetas (flips). Solo afecta a OnFlip, no a llamadas directas a TryFlip")]
    [SerializeField] bool InvertFlipVertical = false;

    [Header("Aceleración del giro (grados/segundo²)")]
    [Tooltip("Qué tan rápido alcanza la velocidad de giro máxima. Más alto = más reactivo")]
    [SerializeField] float YawAcceleration = 300f;
    [Tooltip("Qué tan rápido frena el giro al soltar el stick. Más bajo = más inercia")]
    [SerializeField] float YawDeceleration = 250f;
    [SerializeField] float PitchAcceleration = 200f;
    [SerializeField] float PitchDeceleration = 180f;

    [Header("Giro según la velocidad (a más velocidad, más cuesta girar)")]
    [Tooltip("Velocidad a la que se considera el 100% (eje X de la curva). Por encima de este valor, se queda en el final de la curva")]
    [SerializeField] float MaxSpeedForTurning = 20f;
    [Tooltip("Eje X: 0 = velocidad base, 1 = velocidad máxima. Eje Y: multiplicador del giro (1 = giro normal, 0.5 = la mitad)")]
    [SerializeField] AnimationCurve TurnMultiplierCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.4f);
    [Tooltip("Si está activado, el cabeceo (arriba/abajo) también se endurece con la velocidad")]
    [SerializeField] bool ApplyToPitch = true;

    [Header("Turbo")]
    [Tooltip("Máximo de niveles de turbo acumulables a la vez")]
    [SerializeField] float MaxTurboLevel = 5;
    [Tooltip("Segundos que dura CADA unidad de turbo desde que se consigue, independiente de las demás")]
    [SerializeField] float TurboLifetime = 3f;



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

    [Header("Feedback visual del drift (opcional)")]
    [SerializeField] ParticleSystem[] DriftParticles;
    [SerializeField]
    Color[] TierColors =
    {
        new Color(0.2f, 0.6f, 1f),   // nivel 1 azul
        new Color(1f, 0.55f, 0.1f),  // nivel 2 naranja
        new Color(0.8f, 0.3f, 1f)    // nivel 3 morado
    };

    [Header("Flips (esquiva lateral/vertical con pirueta)")]
    [SerializeField] FlipSettings FlipUpSettings = new FlipSettings(6f, 30f, 360f, 1.5f);
    [SerializeField] FlipSettings FlipDownSettings = new FlipSettings(6f, 30f, 360f, 1.5f);
    [SerializeField] FlipSettings FlipLeftSettings = new FlipSettings(8f, 35f, 360f, 0f);
    [SerializeField] FlipSettings FlipRightSettings = new FlipSettings(8f, 35f, 360f, 0f);
    [Tooltip("Cuánto hay que empujar el stick (0-1) para disparar un flip")]
    [Range(0.1f, 1f)]
    [SerializeField] float FlipInputThreshold = 0f;
    [Tooltip("Segundos de espera tras terminar un flip antes de poder hacer otro")]
    [SerializeField] float FlipCooldown = 0.6f;
    [Tooltip("Cuánta velocidad hacia delante se pierde durante el flip (0 = nada, 0.25 = un 25%)")]
    [Range(0f, 1f)]
    [SerializeField] float FlipSlowdown = 0.25f;
    [Tooltip("Qué tan rápido frena al empezar el flip (multiplicador/segundo)")]
    [SerializeField] float FlipSlowdownSpeed = 4f;
    [Tooltip("Qué tan rápido recupera la velocidad al terminar (multiplicador/segundo). Más bajo = frenada más duradera")]
    [SerializeField] float FlipSlowdownRecovery = 1.5f;

    [Header("Cámara durante el flip")]
    [Tooltip("Objeto vacío que sigue a la nave SIN el desplazamiento del flip. Apunta tu cámara a este objeto (NO lo pongas como hijo de la nave)")]
    [SerializeField] Transform CameraTarget;
    [Tooltip("Cuánto del desplazamiento del flip sigue la cámara en cada eje (0 = nada, 1 = todo). X = izquierda/derecha, Y = arriba/abajo, Z = distancia (adelante/atrás)")]
    [SerializeField] Vector3 CameraFlipFollow = new Vector3(1f, 1f, 1f);
    [Tooltip("Qué tan rápido la cámara vuelve a centrarse en la nave al terminar el flip. Más bajo = más suave")]
    [SerializeField] float CameraRecoverSpeed = 4f;

    

    private Rigidbody rb;

    // Turbo
    private float turboLevel = 0;

    // Cada unidad de turbo tiene su propio tiempo de vida restante (segundos)
    private readonly List<float> turboCharges = new List<float>();

    // Rotación
    private float Yaw;
    private float Pitch;
    private float currentRoll;
    private float yawVelocity;
    private float pitchVelocity;

    // Input
    private Vector2 moveInput;
    private bool driftHeld = false;

    // Drift
    private bool drifting = false;
    private int driftDirection = 0;
    private float driftTime = 0f;
    private int driftTier = 0;

    // Flip
    private bool flipInputLatched = false;
    private bool isFlipping = false;
    private bool flipIsRoll = false;
    private float flipRotationSign = 1f;
    private float flipTravelled = 0f;
    private float flipCooldownTimer = 0f;
    private float flipRollOffset = 0f;
    private float flipPitchOffset = 0f;
    private float flipSpeedMultiplier = 1f;
    private Vector3 flipWorldDirection;
    private Vector3 flipForwardDirection;
    private Vector3 flipCameraOffset = Vector3.zero;
    private FlipSettings activeFlip;

    // Velocidad real hacia delante
    private float currentForwardSpeed;

    private void Start()
    {
        rb = Nave.GetComponent<Rigidbody>();
        currentForwardSpeed = FlySpeed;
        SetDriftParticles(0);
    }

    void Update()
    {
        HandleDrift();

        //mov

        float yawInput = moveInput.x;
        float yawMultiplier = 1f;

        if (drifting)
        {
            // giro bloqueado hacia el lado del drift
            float control = (driftDirection == 1)
                ? Remap(moveInput.x, -1f, 1f, DriftControlMin, DriftControlMax)
                : Remap(moveInput.x, -1f, 1f, DriftControlMax, DriftControlMin);

            yawInput = driftDirection * control;
            yawMultiplier = DriftYawMultiplier;
        }

        // Vel de giro objetivo y aceleración
        // A mas velocidad, más cuesta girar
        float turnMultiplier = GetTurnMultiplier();

        float targetYawVelocity = yawInput * YawAmount * yawMultiplier * turnMultiplier;
        float verticalInput = InvertVerticalAxis ? -moveInput.y : moveInput.y;
        float targetPitchVelocity = -verticalInput * PitchAmount * (ApplyToPitch ? turnMultiplier : 1f);

        yawVelocity = Accelerate(yawVelocity, targetYawVelocity, YawAcceleration, YawDeceleration);
        pitchVelocity = Accelerate(pitchVelocity, targetPitchVelocity, PitchAcceleration, PitchDeceleration);

        Yaw += yawVelocity * Time.deltaTime;
        Pitch += pitchVelocity * Time.deltaTime;

        // Limitar pitch 
        float clampedPitch = Mathf.Clamp(Pitch, -80f, 80f);
        if (clampedPitch != Pitch) pitchVelocity = 0f;
        Pitch = clampedPitch;

        // Roll visual (suavizado)
        float targetRoll = drifting
            ? -driftDirection * DriftRoll
            : Mathf.Lerp(0, 30, Mathf.Abs(moveInput.x)) * -Mathf.Sign(moveInput.x);

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, RollSmooth * Time.deltaTime);

        //Flipcontrol

        HandleFlip();

        // Aplicar rotación: orientación normal + pirueta del flip en ejes locales
        transform.rotation = Quaternion.Euler(Pitch, Yaw, currentRoll)
                           * Quaternion.Euler(flipPitchOffset, 0f, flipRollOffset);

        //tulbito 

        for (int i = turboCharges.Count - 1; i >= 0; i--)
        {
            turboCharges[i] -= Time.deltaTime;

            if (turboCharges[i] <= 0f)
                turboCharges.RemoveAt(i);
        }

        turboLevel = turboCharges.Count;

        //la sped nazjdedjwhqwqiwswdfrewedfgfrewfgfdssdfbngfdsdfghdfdsahgfdgfdsds
        //CHANGE HERE: la velocidad base es FlySpeed, y cada nivel de turbo añade 2 unidades de velocidad extra. El multiplicador flipSpeedMultiplier reduce la velocidad durante el flip.
        currentForwardSpeed = (FlySpeed + (turboLevel *1.3f)* 3f) * flipSpeedMultiplier;
        // El avance usa la orientación SIN la pirueta del flip, así el giro es solo visual
        Vector3 moveForward = Quaternion.Euler(Pitch, Yaw, 0f) * Vector3.forward;
        transform.position += moveForward * currentForwardSpeed * Time.deltaTime;
    }

    //losh flipsh

    
    public bool TryFlip(FlipDirection direction)
    {
        if (isFlipping || flipCooldownTimer > 0f) return false;

        FlipSettings settings;
        Vector3 localDirection;

        switch (direction)
        {
            case FlipDirection.Up:
                settings = FlipUpSettings;
                localDirection = Vector3.up;
                flipIsRoll = false;
                flipRotationSign = -1f;   // morro hacia arriba (loop)
                break;
            case FlipDirection.Down:
                settings = FlipDownSettings;
                localDirection = Vector3.down;
                flipIsRoll = false;
                flipRotationSign = 1f;
                break;
            case FlipDirection.Left:
                settings = FlipLeftSettings;
                localDirection = Vector3.left;
                flipIsRoll = true;
                flipRotationSign = 1f;
                break;
            default: // Right
                settings = FlipRightSettings;
                localDirection = Vector3.right;
                flipIsRoll = true;
                flipRotationSign = -1f;
                break;
        }

        if (settings.Distance <= 0f) return false;

        // La dirección se fija al empezar, según la orientación de la nave (sin roll ni pirueta)
        Quaternion baseRotation = Quaternion.Euler(Pitch, Yaw, 0f);
        flipWorldDirection = baseRotation * localDirection;
        flipForwardDirection = baseRotation * Vector3.forward;
        activeFlip = settings;
        flipTravelled = 0f;
        flipRollOffset = 0f;
        flipPitchOffset = 0f;
        isFlipping = true;
        return true;
    }

    private void HandleFlip()
    {
        // Frenada suave: baja al empezar el flip y se recupera al terminar
        float targetMultiplier = isFlipping ? 1f - FlipSlowdown : 1f;
        float rate = isFlipping ? FlipSlowdownSpeed : FlipSlowdownRecovery;
        flipSpeedMultiplier = Mathf.MoveTowards(flipSpeedMultiplier, targetMultiplier, rate * Time.deltaTime);

        if (!isFlipping)
        {
            if (flipCooldownTimer > 0f) flipCooldownTimer -= Time.deltaTime;

            // La cámara vuelve poco a poco a centrarse en la nave
            flipCameraOffset = Vector3.Lerp(
                flipCameraOffset,
                Vector3.zero,
                1f - Mathf.Exp(-CameraRecoverSpeed * Time.deltaTime));
            return;
        }

        // Desplazamiento
        float speed = Mathf.Max(activeFlip.Speed, 0.01f);
        float step = Mathf.Min(speed * Time.deltaTime, activeFlip.Distance - flipTravelled);
        // Desplazamiento en línea recta: principal (arriba/abajo/lados) + un poco hacia delante
        float forwardStep = activeFlip.ForwardDistance * (step / activeFlip.Distance);
        Vector3 movement = flipWorldDirection * step + flipForwardDirection * forwardStep;

        transform.position += movement;
        flipCameraOffset += movement;
        flipTravelled += step;

        // Pirueta sincronizada con el desplazamiento
        float t = Mathf.Clamp01(flipTravelled / activeFlip.Distance);
        float angle = Mathf.SmoothStep(0f, activeFlip.Rotation, t) * flipRotationSign;

        if (flipIsRoll) flipRollOffset = angle;
        else flipPitchOffset = angle;

        if (t >= 1f) EndFlip();
    }

    private void EndFlip()
    {
        isFlipping = false;
        flipRollOffset = 0f;
        flipPitchOffset = 0f;
        flipCooldownTimer = FlipCooldown;
    }

    public bool IsFlipping()
    {
        return isFlipping;
    }

    public bool CanFlip()
    {
        return !isFlipping && flipCooldownTimer <= 0f;
    }

    //drifteo

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

    // =========================
    // UTILIDADES
    // =========================

    // Acerca 'current' a 'target' con aceleración (input activo) o deceleración (sin input)
    private float Accelerate(float current, float target, float acceleration, float deceleration)
    {
        float rate = Mathf.Approximately(target, 0f) ? deceleration : acceleration;
        return Mathf.MoveTowards(current, target, rate * Time.deltaTime);
    }

    // Multiplicador de giro (1 = normal) según la velocidad actual, definido por la curva del Inspector
    public float GetTurnMultiplier()
    {
        float speedPercent = Mathf.InverseLerp(FlySpeed, Mathf.Max(MaxSpeedForTurning, FlySpeed + 0.01f), currentForwardSpeed);
        return Mathf.Max(0f, TurnMultiplierCurve.Evaluate(speedPercent));
    }

    private static float Remap(float value, float inMin, float inMax, float outMin, float outMax)
    {
        return Mathf.Lerp(outMin, outMax, Mathf.InverseLerp(inMin, inMax, value));
    }

    // =========================
    // TURBO
    // =========================

    
    public void AddTurbo()
    {
        AddTurbo(1);
    }

    public void AddTurbo(int amount)
    {
        // Cada unidad nueva entra con su propia vida completa; no se acumulan por encima del máximo
        int maxCharges = Mathf.RoundToInt(MaxTurboLevel);

        for (int i = 0; i < amount && turboCharges.Count < maxCharges; i++)
            turboCharges.Add(TurboLifetime);

        turboLevel = turboCharges.Count;
        Debug.Log("Turbo Level: " + turboLevel);
    }

    public float GetTurboLevel()
    {
        return turboLevel;
    }

    public float GetMaxTurboLevel()
    {
        return MaxTurboLevel;
    }

    public bool IsDrifting()
    {
        return drifting;
    }

    public int GetDriftTier()
    {
        return driftTier;
    }

    // =========================
    // INPUT
    // =========================

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

    // Una sola acción de tipo Value > Vector2 (stick o Composite "Up/Down/Left/Right").
    // La dirección con más peso del vector decide el flip, y hay que volver a soltar el stick para repetir.
    public void OnFlip(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            flipInputLatched = false;
            return;
        }

        Vector2 input = context.ReadValue<Vector2>();

        // Stick casi en reposo: rearmar para el siguiente flip
        if (input.magnitude < FlipInputThreshold * 0.5f)
        {
            flipInputLatched = false;
            return;
        }

        if (flipInputLatched || input.magnitude < FlipInputThreshold) return;

        flipInputLatched = true;

        FlipDirection direction;
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            direction = input.x > 0f ? FlipDirection.Right : FlipDirection.Left;
        else
        {
            bool up = input.y > 0f;
            if (InvertFlipVertical) up = !up;
            direction = up ? FlipDirection.Up : FlipDirection.Down;
        }

        TryFlip(direction);
    }

    // =========================
    // CÁMARA
    // =========================

    // Posición que debe seguir la cámara: la de la nave menos la parte del desplazamiento del flip
    // que NO queremos seguir, eje por eje (X lados, Y arriba/abajo, Z distancia).
    public Vector3 GetCameraTargetPosition()
    {
        Quaternion basis = Quaternion.Euler(Pitch, Yaw, 0f);
        Vector3 localOffset = Quaternion.Inverse(basis) * flipCameraOffset;
        localOffset = Vector3.Scale(localOffset, Vector3.one - CameraFlipFollow);

        return transform.position - basis * localOffset;
    }

    // Orientación de la nave sin la pirueta del flip (la cámara no da vueltas con el barrel roll)
    public Quaternion GetCameraTargetRotation()
    {
        return Quaternion.Euler(Pitch, Yaw, currentRoll);
    }

    private void LateUpdate()
    {
        if (CameraTarget == null) return;

        CameraTarget.SetPositionAndRotation(GetCameraTargetPosition(), GetCameraTargetRotation());
    }

    private void FixedUpdate()
    {
        // Actualizar la velocidad en el UI
        if (SpeedText != null)
            SpeedText.text = "Speed: " + currentForwardSpeed.ToString("F0") + " GB/S";
    }
}