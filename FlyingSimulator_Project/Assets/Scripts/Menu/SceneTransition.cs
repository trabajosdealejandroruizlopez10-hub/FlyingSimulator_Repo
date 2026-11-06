using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Transición entre escenas: tapa la pantalla (fundido o barrido de franjas de colores),
/// carga la escena en segundo plano con una barra de progreso y vuelve a mostrar la imagen.
///
/// Uso desde cualquier script:
///     SceneTransition.Load("Game");     // por nombre
///     SceneTransition.Load(1);          // por índice de Build Settings
///
/// No hace falta ponerlo en ninguna escena: se crea solo la primera vez que se usa.
/// Si quieres cambiar los ajustes desde el Inspector, añade este componente a un
/// GameObject vacío en la escena del menú (en la raíz de la jerarquía).
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public enum Style { Fade, Wipe }

    [Header("Estilo")]
    [SerializeField] private Style style = Style.Wipe;
    [SerializeField, Min(0f)] private float outTime = 0.55f;   // tapar la pantalla
    [SerializeField, Min(0f)] private float inTime = 0.55f;    // descubrir la escena nueva
    [SerializeField] private Color fadeColor = Color.black;

    [Header("Barrido (solo estilo Wipe)")]
    [SerializeField, Range(-30f, 30f)] private float wipeAngle = 12f;
    [SerializeField]
    private Color[] barColors =
    {
        new Color(1.00f, 0.18f, 0.75f),   // rosa neón
        new Color(0.10f, 0.86f, 1.00f),   // cian
        new Color(1.00f, 0.84f, 0.16f),   // amarillo
        new Color(0.45f, 0.25f, 0.95f),   // violeta
        new Color(1.00f, 0.18f, 0.75f),
        new Color(0.10f, 0.86f, 1.00f),
    };

    [Header("Pantalla de carga")]
    [SerializeField] private bool showLoading = true;
    [SerializeField] private string loadingText = "CARGANDO";
    [Tooltip("Tiempo mínimo en la pantalla de carga, para que no parpadee si la escena carga al instante")]
    [SerializeField, Min(0f)] private float minLoadingTime = 0.8f;
    [SerializeField] private Color loadingBarColor = new Color(1.00f, 0.18f, 0.75f);

    [Header("Audio")]
    [Tooltip("Baja el volumen general al tapar la pantalla y lo sube al descubrir la nueva")]
    [SerializeField] private bool fadeAudio = true;

    private static SceneTransition instance;

    /// <summary>true mientras hay una transición en marcha.</summary>
    public static bool IsBusy => instance != null && instance.busy;

    public static void Load(string sceneName)
    {
        Get().Begin(sceneName, -1);
    }

    public static void Load(int buildIndex)
    {
        Get().Begin(null, buildIndex);
    }

    private static SceneTransition Get()
    {
        if (instance == null)
        {
            var go = new GameObject("SceneTransition");
            go.AddComponent<SceneTransition>();   // su Awake rellena "instance"
        }
        return instance;
    }

    // ------------------------------------------------------------------ UI
    private GameObject canvasGo;
    private RectTransform canvasRect;
    private RectTransform wipeRoot;
    private RectTransform[] bars;
    private Image blackImage;
    private CanvasGroup loadingGroup;
    private TMP_Text loadingLabel;
    private RectTransform barBackground;
    private RectTransform barFill;
    private float travel;
    private bool busy;
    private float savedVolume = 1f;

    private void Awake()
    {
        // Este componente hace DontDestroyOnLoad sobre su propio objeto. Si estuviera en un Canvas
        // o en un objeto con hijos, arrastraría todo eso (portada del menú incluida) al juego.
        if (GetComponent<Canvas>() != null || transform.childCount > 0)
        {
            Debug.LogWarning("[SceneTransition] No lo pongas en un Canvas ni en objetos con hijos: " +
                             "ponlo en un GameObject vacío (o no lo pongas, se crea solo). " +
                             "Se ignora este componente.", this);
            Destroy(this);
            return;
        }

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            if (busy && fadeAudio) AudioListener.volume = savedVolume;
            instance = null;
        }
    }

    private void BuildUI()
    {
        canvasGo = new GameObject("TransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;   // por encima de todo
        canvasRect = canvasGo.GetComponent<RectTransform>();

        // --- Franjas (estilo Wipe) ---
        wipeRoot = NewRect("WipeRoot", canvasRect);
        wipeRoot.anchorMin = wipeRoot.anchorMax = new Vector2(0.5f, 0.5f);
        wipeRoot.localEulerAngles = new Vector3(0f, 0f, wipeAngle);

        int n = Mathf.Max(1, barColors.Length);
        bars = new RectTransform[n];
        for (int i = 0; i < n; i++)
        {
            var bar = NewRect("Bar" + i, wipeRoot);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            var img = bar.gameObject.AddComponent<Image>();
            img.color = barColors[i % barColors.Length];
            img.raycastTarget = true;   // bloquea clics mientras dura la transición
            bars[i] = bar;
        }

        // --- Capa negra (fundido y fondo de la pantalla de carga) ---
        var blackRt = NewRect("Black", canvasRect);
        blackRt.anchorMin = Vector2.zero;
        blackRt.anchorMax = Vector2.one;
        blackRt.offsetMin = blackRt.offsetMax = Vector2.zero;
        blackImage = blackRt.gameObject.AddComponent<Image>();
        blackImage.raycastTarget = true;

        // --- Pantalla de carga ---
        var loadRt = NewRect("Loading", canvasRect);
        loadRt.anchorMin = Vector2.zero;
        loadRt.anchorMax = Vector2.one;
        loadRt.offsetMin = loadRt.offsetMax = Vector2.zero;
        loadingGroup = loadRt.gameObject.AddComponent<CanvasGroup>();
        loadingGroup.blocksRaycasts = false;
        loadingGroup.interactable = false;

        var textRt = NewRect("Text", loadRt);
        textRt.anchorMin = textRt.anchorMax = new Vector2(1f, 0f);
        textRt.pivot = new Vector2(1f, 0f);
        loadingLabel = textRt.gameObject.AddComponent<TextMeshProUGUI>();
        loadingLabel.alignment = TextAlignmentOptions.BottomRight;
        loadingLabel.fontStyle = FontStyles.Bold;
        loadingLabel.color = Color.white;
        loadingLabel.raycastTarget = false;

        barBackground = NewRect("BarBackground", loadRt);
        barBackground.anchorMin = Vector2.zero;
        barBackground.anchorMax = new Vector2(1f, 0f);
        barBackground.pivot = new Vector2(0.5f, 0f);
        var bgImg = barBackground.gameObject.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.15f);
        bgImg.raycastTarget = false;

        barFill = NewRect("BarFill", barBackground);
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        var fillImg = barFill.gameObject.AddComponent<Image>();
        fillImg.color = loadingBarColor;
        fillImg.raycastTarget = false;

        canvasGo.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Adapta tamaños a la resolución actual y deja todo en su estado inicial.</summary>
    private void PrepareVisuals()
    {
        float w = Screen.width;
        float h = Screen.height;

        // El cuadrado girado de lado "diag" cubre siempre la pantalla entera
        float diag = Mathf.Sqrt(w * w + h * h) * 1.15f;
        travel = diag * 1.05f;
        wipeRoot.sizeDelta = new Vector2(diag, diag);
        wipeRoot.localEulerAngles = new Vector3(0f, 0f, wipeAngle);
        wipeRoot.gameObject.SetActive(style == Style.Wipe);

        float bh = diag / bars.Length;
        for (int i = 0; i < bars.Length; i++)
        {
            bars[i].sizeDelta = new Vector2(diag, bh + 3f);   // +3 evita rendijas entre franjas
            bars[i].anchoredPosition = new Vector2(-travel, -diag / 2f + bh * (i + 0.5f));
        }

        SetBlack(0f);

        var textRt = loadingLabel.rectTransform;
        textRt.anchoredPosition = new Vector2(-w * 0.04f, h * 0.05f);
        textRt.sizeDelta = new Vector2(w * 0.5f, h * 0.12f);
        loadingLabel.fontSize = h * 0.055f;
        loadingLabel.text = loadingText;

        barBackground.sizeDelta = new Vector2(0f, Mathf.Max(4f, h * 0.008f));
        SetBar(0f);
        loadingGroup.alpha = 0f;
    }

    private void SetBlack(float a)
    {
        blackImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, a);
    }

    private void SetBar(float p)
    {
        barFill.anchorMax = new Vector2(Mathf.Clamp01(p), 1f);
    }

    private void SetAudio(float factor)
    {
        if (fadeAudio) AudioListener.volume = savedVolume * Mathf.Clamp01(factor);
    }

    private void SetWipe(float t, bool entering)
    {
        int n = bars.Length;
        const float stagger = 0.07f;
        float span = Mathf.Max(0.05f, 1f - stagger * (n - 1));

        for (int i = 0; i < n; i++)
        {
            float p = Mathf.Clamp01((t - stagger * i) / span);
            float e = 1f - Mathf.Pow(1f - p, 3f);   // arranca rápido y frena al final
            float x = entering ? Mathf.Lerp(-travel, 0f, e) : Mathf.Lerp(0f, travel, e);
            var pos = bars[i].anchoredPosition;
            pos.x = x;
            bars[i].anchoredPosition = pos;
        }
    }

    // ------------------------------------------------------------------ flujo
    private void Begin(string sceneName, int index)
    {
        if (busy) return;   // ignora dobles clics

        bool exists = sceneName != null
            ? Application.CanStreamedLevelBeLoaded(sceneName)
            : (index >= 0 && index < SceneManager.sceneCountInBuildSettings);

        if (!exists)
        {
            Debug.LogError("[SceneTransition] La escena '" + (sceneName ?? index.ToString()) +
                           "' no existe o no está en File > Build Profiles / Build Settings.");
            return;
        }

        StartCoroutine(Run(sceneName, index));
    }

    private IEnumerator Tween(float duration, Action<float> step)
    {
        if (duration <= 0f)
        {
            step(1f);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;   // funciona aunque timeScale sea 0
            step(Mathf.Clamp01(t));
            yield return null;
        }
    }

    private IEnumerator Run(string sceneName, int index)
    {
        busy = true;
        savedVolume = AudioListener.volume;
        canvasGo.SetActive(true);
        PrepareVisuals();

        // 1) TAPAR LA PANTALLA
        if (style == Style.Fade)
        {
            yield return Tween(outTime, t => { SetBlack(t); SetAudio(1f - t); });
        }
        else
        {
            yield return Tween(outTime, t => { SetWipe(t, true); SetAudio(1f - t); });
            yield return Tween(0.12f, t => SetBlack(t));   // fondo uniforme para la pantalla de carga
        }

        // 2) CARGAR EN SEGUNDO PLANO
        if (showLoading)
            yield return Tween(0.12f, t => loadingGroup.alpha = t);

        AsyncOperation op = sceneName != null
            ? SceneManager.LoadSceneAsync(sceneName)
            : SceneManager.LoadSceneAsync(index);

        if (op == null)
        {
            Debug.LogError("[SceneTransition] No se pudo cargar la escena.");
            yield return Uncover();
            yield break;
        }

        op.allowSceneActivation = false;
        float start = Time.unscaledTime;
        float shown = 0f;
        float minTime = showLoading ? minLoadingTime : 0f;

        // Unity se queda en 0.9 hasta que se permite activar la escena
        while (op.progress < 0.9f || Time.unscaledTime - start < minTime)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float byTime = minTime > 0f ? Mathf.Clamp01((Time.unscaledTime - start) / minTime) : 1f;
            shown = Mathf.MoveTowards(shown, Mathf.Min(real, byTime), Time.unscaledDeltaTime * 1.5f);
            SetBar(shown);
            if (showLoading)
                loadingLabel.text = loadingText + new string('.', (int)(Time.unscaledTime * 3f) % 4);
            yield return null;
        }

        SetBar(1f);
        yield return new WaitForSecondsRealtime(0.1f);

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;   // un frame para que arranquen los Awake/Start de la escena nueva

        // 3) DESCUBRIR LA ESCENA NUEVA
        yield return Uncover();
    }

    private IEnumerator Uncover()
    {
        if (style == Style.Fade)
        {
            yield return Tween(inTime, t =>
            {
                SetBlack(1f - t);
                loadingGroup.alpha = 1f - t;
                SetAudio(t);
            });
        }
        else
        {
            yield return Tween(0.15f, t =>
            {
                SetBlack(1f - t);
                loadingGroup.alpha = 1f - t;
            });
            yield return Tween(inTime, t => { SetWipe(t, false); SetAudio(t); });
        }

        if (fadeAudio) AudioListener.volume = savedVolume;
        canvasGo.SetActive(false);
        busy = false;
    }
}