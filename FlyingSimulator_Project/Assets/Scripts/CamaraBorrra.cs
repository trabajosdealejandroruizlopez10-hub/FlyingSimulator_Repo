using UnityEngine;
using UnityEngine.UI;

public class AirCamera : MonoBehaviour
{
    [System.Serializable]
    public class BoostLevelEffect
    {
        [Tooltip("Grados extra de FOV (zoom out) en este nivel")]
        public float fovIncrease;
        [Tooltip("Metros que la cámara se aleja en este nivel")]
        public float extraDistance;
        [Range(0f, 1f)]
        [Tooltip("Opacidad de los bordes oscuros (visión de túnel) en este nivel")]
        public float vignetteAlpha;
        [Range(0f, 1f)]
        [Tooltip("Intensidad del temblor continuo en este nivel")]
        public float shake;

        public BoostLevelEffect(float fov, float dist, float vignette, float shake)
        {
            fovIncrease = fov;
            extraDistance = dist;
            vignetteAlpha = vignette;
            this.shake = shake;
        }
    }

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
    [Tooltip("Anula el retraso al seguir a la nave (0 = cámara con retraso normal, 1 = sin retraso al avanzar)")]
    [Range(0f, 1f)]
    public float lagCompensation = 1f;

    [Header("Velocidad")]
    public float maxSpeed = 50f;
    public float maxFOVIncrease = 12f;

    [Header("Inclinación")]
    public float maxTilt = 8f;
    public float tiltSmooth = 5f;

    [Header("FOV")]
    public float normalFOV = 70f;
    [Tooltip("Qué tan rápido el FOV alcanza su valor objetivo")]
    public float fovSmooth = 7f;

    // =========================================================
    // FEEDBACK DE TURBO (estilo Sonic & All-Stars Racing Transformed)
    // =========================================================

    [Header("Feedback · General")]
    public bool enableBoostFeedback = true;
    [Tooltip("Qué tan rápido sube el efecto al coger turbo")]
    public float boostRiseSpeed = 8f;
    [Tooltip("Qué tan rápido se apaga el efecto cuando el turbo baja")]
    public float boostFallSpeed = 3f;
    [Tooltip("Cuánto efecto 'golpe' (0-1) se añade por cada nivel de turbo que ganas")]
    public float impulsePerTurboLevel = 0.35f;
    [Tooltip("Qué tan rápido se desvanece el golpe inicial (por segundo)")]
    public float impulseDecay = 2.5f;

    [Header("Feedback · Efecto por nivel de turbo")]
    [Tooltip("Elemento 0 = nivel de turbo 1, elemento 1 = nivel 2, etc. Si el turbo supera la lista, usa el último")]
    public BoostLevelEffect[] levelEffects =
    {
        new BoostLevelEffect(3f,  0.3f, 0.10f, 0.05f),   // nivel 1
        new BoostLevelEffect(6f,  0.6f, 0.20f, 0.10f),   // nivel 2
        new BoostLevelEffect(9f,  1.0f, 0.30f, 0.18f),   // nivel 3
        new BoostLevelEffect(12f, 1.3f, 0.40f, 0.26f),   // nivel 4
        new BoostLevelEffect(15f, 1.6f, 0.50f, 0.35f)    // nivel 5
    };

    [Header("Feedback · Golpe al subir de nivel")]
    [Tooltip("Grados extra de FOV en el golpe inicial al coger turbo")]
    public float boostFOVKick = 8f;

    [Header("Feedback · Visión de túnel")]
    public Color vignetteColor = Color.black;
    [Range(0f, 1f)]
    [Tooltip("Opacidad extra de los bordes en el golpe inicial al coger turbo")]
    public float vignetteImpulseAlpha = 0.25f;
    [Tooltip("Radio donde empieza a oscurecerse (0 = centro, 1 = borde de la pantalla)")]
    public float vignetteInnerRadius = 0.5f;
    [Tooltip("Radio donde llega a la máxima oscuridad")]
    public float vignetteOuterRadius = 1.2f;

    [Header("Feedback · Temblor")]
    [Range(0f, 1f)]
    [Tooltip("Temblor extra en el golpe inicial al coger turbo")]
    public float impulseShake = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("Temblor por cada nivel de drift acumulado")]
    public float driftShakePerTier = 0.12f;
    [Tooltip("Desplazamiento máximo del temblor (metros)")]
    public float shakePositionAmount = 0.12f;
    [Tooltip("Rotación máxima del temblor (grados)")]
    public float shakeRotationAmount = 1.2f;
    [Tooltip("Velocidad del temblor. Más alto = más vibración")]
    public float shakeFrequency = 22f;

    private Camera cam;
    private Vector3 currentVelocity;
    private float currentTilt;

    // Velocidad real de seguimiento (calculada por posición, no depende del Rigidbody)
    private Vector3 lastFollowPosition;
    private bool hasLastFollow = false;

    // Feedback
    private float lastTurbo = 0f;
    private float smoothedFOV = 0f;       // efectos del nivel de turbo actual (suavizados)
    private float smoothedDistance = 0f;
    private float smoothedVignette = 0f;
    private float smoothedShake = 0f;
    private float impulse = 0f;           // 0-1 golpe inicial al coger turbo
    private float shakeIntensity = 0f;
    private float vignetteAlpha = 0f;

    // Temblor aplicado en el frame anterior (se deshace antes de calcular el siguiente)
    private Vector3 lastShakePosition = Vector3.zero;
    private Quaternion lastShakeRotation = Quaternion.identity;

    // Viñeta generada por código
    private GameObject vignetteCanvas;
    private RawImage vignetteImage;
    private Texture2D vignetteTexture;

    void Start()
    {
        cam = GetComponent<Camera>();

        if (cam != null)
            cam.fieldOfView = normalFOV;

        // Buscar el controlador: en el jugador, en sus padres y, si no, en toda la escena
        if (airplane == null && player != null)
            airplane = player.GetComponentInParent<AirplaneController>();

        if (airplane == null)
            airplane = FindFirstObjectByType<AirplaneController>();

        if (airplane == null)
            Debug.LogWarning("AirCamera: no encuentro el AirplaneController. Sin él, la cámara copiará la pirueta del flip y no habrá feedback de turbo.", this);

        if (airplane != null)
            lastTurbo = airplane.GetTurboLevel();

        CreateVignette();
    }

    void OnDestroy()
    {
        if (vignetteCanvas != null) Destroy(vignetteCanvas);
        if (vignetteTexture != null) Destroy(vignetteTexture);
    }

    void LateUpdate()
    {
        // Deshacer el temblor del frame anterior para que no se acumule en el suavizado
        transform.position -= lastShakePosition;
        transform.rotation *= Quaternion.Inverse(lastShakeRotation);
        lastShakePosition = Vector3.zero;
        lastShakeRotation = Quaternion.identity;

        if (player == null)
            return;

        UpdateFeedback();

        // Punto y orientación que sigue la cámara.
        // Con el controlador: la nave SIN la pirueta del flip (la cámara nunca copia el giro del flip).
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


        // Velocidad real del punto que seguimos (por diferencia de posición entre frames)
        Vector3 followVelocity = Vector3.zero;

        if (hasLastFollow && Time.deltaTime > 0f)
            followVelocity = (followPosition - lastFollowPosition) / Time.deltaTime;

        lastFollowPosition = followPosition;
        hasLastFollow = true;

        // Un Lerp siempre se queda atrás una distancia de velocidad / suavizado.
        // Adelantamos el objetivo justo esa distancia para que la cámara no se quede atrás.
        Vector3 lagOffset = followVelocity * (lagCompensation / Mathf.Max(positionSmooth, 0.01f));

        // Con el turbo la cámara se aleja un poco (zoom out)
        float currentDistance = distance + smoothedDistance;

        Vector3 targetPosition =
            followPosition
            + lagOffset
            - movementDirection * currentDistance
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

            // Zoom por velocidad + zoom out del turbo + golpe inicial
            float targetFOV =
                normalFOV +
                maxFOVIncrease * speedPercent +
                smoothedFOV +
                boostFOVKick * impulse;

            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                targetFOV,
                fovSmooth * Time.deltaTime
            );
        }

        UpdateVignette();
        ApplyShake();
    }

    // =========================================================
    // FEEDBACK
    // =========================================================

    private void UpdateFeedback()
    {
        float dt = Time.deltaTime;

        // Efecto objetivo según el NIVEL de turbo actual (0 = sin efecto)
        float targetFOV = 0f, targetDistance = 0f, targetVignette = 0f, targetShake = 0f;
        int driftTier = 0;

        if (enableBoostFeedback && airplane != null)
        {
            float turbo = airplane.GetTurboLevel();
            int level = Mathf.RoundToInt(turbo);

            if (level > 0 && levelEffects != null && levelEffects.Length > 0)
            {
                BoostLevelEffect effect = levelEffects[Mathf.Min(level, levelEffects.Length) - 1];
                targetFOV = effect.fovIncrease;
                targetDistance = effect.extraDistance;
                targetVignette = effect.vignetteAlpha;
                targetShake = effect.shake;
            }

            // Ha subido el turbo (aro o fin de drift): golpe inicial proporcional a los niveles ganados
            if (turbo > lastTurbo)
                impulse = Mathf.Clamp01(impulse + (turbo - lastTurbo) * impulsePerTurboLevel);

            lastTurbo = turbo;
            driftTier = airplane.GetDriftTier();
        }
        else
        {
            lastTurbo = 0f;
        }

        // Sube rápido, baja despacio
        smoothedFOV = SmoothToward(smoothedFOV, targetFOV, dt);
        smoothedDistance = SmoothToward(smoothedDistance, targetDistance, dt);
        smoothedVignette = SmoothToward(smoothedVignette, targetVignette, dt);
        smoothedShake = SmoothToward(smoothedShake, targetShake, dt);

        impulse = Mathf.MoveTowards(impulse, 0f, impulseDecay * dt);

        if (enableBoostFeedback)
        {
            shakeIntensity = Mathf.Clamp01(
                smoothedShake +
                impulse * impulseShake +
                driftTier * driftShakePerTier);

            vignetteAlpha = Mathf.Clamp01(smoothedVignette + impulse * vignetteImpulseAlpha);
        }
        else
        {
            shakeIntensity = 0f;
            vignetteAlpha = 0f;
        }
    }

    private float SmoothToward(float current, float target, float dt)
    {
        float rate = target > current ? boostRiseSpeed : boostFallSpeed;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
    }

    private void ApplyShake()
    {
        if (shakeIntensity <= 0.001f)
            return;

        // Ruido Perlin: temblor suave y continuo, no saltos aleatorios
        float t = Time.time * shakeFrequency;
        float nx = Noise(t, 0f);
        float ny = Noise(t, 17.3f);
        float nz = Noise(t, 41.7f);

        Vector3 positionOffset =
            (transform.right * nx + transform.up * ny)
            * shakePositionAmount * shakeIntensity;

        float rot = shakeRotationAmount * shakeIntensity;
        Quaternion rotationOffset = Quaternion.Euler(ny * rot * 0.6f, nx * rot * 0.6f, nz * rot);

        transform.position += positionOffset;
        transform.rotation *= rotationOffset;

        lastShakePosition = positionOffset;
        lastShakeRotation = rotationOffset;
    }

    private static float Noise(float t, float seed)
    {
        return Mathf.PerlinNoise(t, seed) * 2f - 1f;
    }

    // =========================================================
    // VIÑETA (visión de túnel)
    // =========================================================

    private void CreateVignette()
    {
        vignetteCanvas = new GameObject("BoostVignetteCanvas");

        Canvas canvas = vignetteCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        GameObject imageObject = new GameObject("Vignette");
        imageObject.transform.SetParent(vignetteCanvas.transform, false);

        vignetteImage = imageObject.AddComponent<RawImage>();
        vignetteImage.raycastTarget = false;

        RectTransform rect = vignetteImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        vignetteTexture = BuildVignetteTexture(128);
        vignetteImage.texture = vignetteTexture;
        vignetteImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private Texture2D BuildVignetteTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float inner = vignetteInnerRadius;
        float outer = Mathf.Max(vignetteOuterRadius, inner + 0.01f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Coordenadas de -1 a 1 desde el centro (la esquina vale ~1.41)
                float px = (x / (size - 1f)) * 2f - 1f;
                float py = (y / (size - 1f)) * 2f - 1f;
                float d = Mathf.Sqrt(px * px + py * py);

                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
                tex.SetPixel(x, y, new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, a));
            }
        }

        tex.Apply();
        return tex;
    }

    private void UpdateVignette()
    {
        if (vignetteImage == null)
            return;

        vignetteImage.color = new Color(1f, 1f, 1f, vignetteAlpha);
    }
}