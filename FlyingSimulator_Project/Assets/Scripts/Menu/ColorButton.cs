using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ilumina un botón de un color cuando el ratón pasa por encima o el mando lo selecciona.
/// Ratón y mando acaban en lo mismo (el botón queda "seleccionado"), así que
/// siempre hay UN solo botón iluminado.
/// Añádelo a cada botón y elige su color en el Inspector.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonHighlight : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
{
    [Header("Color al resaltar")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.25f, 0.8f, 1f); // rosa neón

    [Header("Qué se ilumina")]
    [SerializeField] private bool tintImage = true;
    [Tooltip("Vacío = la Image del propio botón")]
    [SerializeField] private Image image;

    [SerializeField] private bool tintText = false;
    [Tooltip("Vacío = busca un TextMeshPro en los hijos")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color textHighlightColor = Color.black;

    [Header("Extras")]
    [SerializeField, Min(0.001f)] private float fadeTime = 0.08f;
    [Tooltip("1 = sin cambio de tamaño")]
    [SerializeField] private float selectedScale = 1.05f;

    private Button button;
    private Color imageNormal;
    private Color labelNormal;
    private Vector3 baseScale;
    private float amount;   // 0 = normal, 1 = iluminado
    private float target;

    private void Awake()
    {
        button = GetComponent<Button>();

        // La transición de color del propio Button se pelearía con este script
        button.transition = Selectable.Transition.None;

        if (image == null) image = GetComponent<Image>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);

        if (image != null) imageNormal = image.color;
        if (label != null) labelNormal = label.color;
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        amount = 0f;
        target = 0f;
        Apply();
    }

    private void OnDisable()
    {
        amount = 0f;
        target = 0f;
        Apply();
    }

    // RATÓN: al pasar por encima, este botón pasa a ser el seleccionado
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.interactable || EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject != gameObject)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    // MANDO Y RATÓN: los dos llegan aquí
    public void OnSelect(BaseEventData eventData)
    {
        target = 1f;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        target = 0f;
    }

    private void Update()
    {
        if (Mathf.Approximately(amount, target)) return;

        // unscaledDeltaTime: funciona aunque Time.timeScale sea 0
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / fadeTime);
        amount = Mathf.Lerp(amount, target, k);
        if (Mathf.Abs(amount - target) < 0.002f) amount = target;
        Apply();
    }

    private void Apply()
    {
        if (tintImage && image != null)
        {
            // Mantiene el alpha original: si el fondo era transparente, sigue transparente
            var hi = new Color(highlightColor.r, highlightColor.g, highlightColor.b, imageNormal.a);
            image.color = Color.Lerp(imageNormal, hi, amount);
        }

        if (tintText && label != null)
            label.color = Color.Lerp(labelNormal, textHighlightColor, amount);

        transform.localScale = baseScale * Mathf.Lerp(1f, selectedScale, amount);
    }
}