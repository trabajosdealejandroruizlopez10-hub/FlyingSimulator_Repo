using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Ponlo en el objeto padre del fuego (FullFire). Cada HIJO directo es una variante completa del fuego
/// (con sus propias capas de partículas) y corresponde a un nivel de turbo:
///   hijo 0 = nivel 1, hijo 1 = nivel 2, ...
/// Cada variante la editas con el Particle System normal (tamaño, forma, duración, color, emisión...).
/// - Sin turbo: ninguna variante emite.
/// - Con turbo: se enciende la variante del nivel actual y la anterior se apaga con suavidad.
/// </summary>
public class TurboFireVFX : MonoBehaviour
{
    [Tooltip("Se busca solo en el padre o en la escena si lo dejas vacío")]
    [SerializeField] AirplaneController airplane;

    [Tooltip("Una variante por nivel (elemento 0 = nivel 1). Si lo dejas vacío, usa los hijos directos en orden. Si el turbo supera la lista, usa la última")]
    [SerializeField] GameObject[] levelFires;

    [Tooltip("Para el botón del menú: cuántas variantes crear duplicando la primera")]
    [SerializeField] int levelsToCreate = 12;

    private ParticleSystem[][] variantSystems;
    private int lastLevel = -1;
    private int activeIndex = -1;

    private void Start()
    {
        if (airplane == null)
            airplane = GetComponentInParent<AirplaneController>();

        if (airplane == null)
            airplane = FindFirstObjectByType<AirplaneController>();

        if (airplane == null)
            Debug.LogWarning("TurboFireVFX: no encuentro el AirplaneController.", this);

        // Sin lista explícita: cada hijo directo es una variante
        if (levelFires == null || levelFires.Length == 0)
        {
            levelFires = new GameObject[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                levelFires[i] = transform.GetChild(i).gameObject;
        }

        variantSystems = new ParticleSystem[levelFires.Length][];

        for (int i = 0; i < levelFires.Length; i++)
        {
            if (levelFires[i] == null)
            {
                variantSystems[i] = new ParticleSystem[0];
                continue;
            }

            // Tienen que estar activas para poder emitir; el control lo hacemos con Play/Stop
            levelFires[i].SetActive(true);

            variantSystems[i] = levelFires[i].GetComponentsInChildren<ParticleSystem>(true);

            // Empezar apagado y limpio
            foreach (ParticleSystem ps in variantSystems[i])
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (airplane == null || variantSystems == null) return;

        int level = Mathf.RoundToInt(airplane.GetTurboLevel());

        // Solo se actualiza cuando cambia el nivel
        if (level == lastLevel) return;
        lastLevel = level;

        Apply(level);
    }

    private void Apply(int level)
    {
        int newIndex = -1;

        if (level > 0 && variantSystems.Length > 0)
            newIndex = Mathf.Min(level, variantSystems.Length) - 1;

        if (newIndex == activeIndex) return;

        // La variante anterior deja de emitir y sus partículas se desvanecen solas
        if (activeIndex >= 0)
        {
            foreach (ParticleSystem ps in variantSystems[activeIndex])
                ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        // La nueva empieza a emitir
        if (newIndex >= 0)
        {
            foreach (ParticleSystem ps in variantSystems[newIndex])
                ps.Play(false);
        }

        activeIndex = newIndex;
    }

#if UNITY_EDITOR
    // Menú del componente (tres puntos) → crea las variantes que faltan duplicando la primera
    [ContextMenu("Crear variantes duplicando la primera")]
    private void CreateVariants()
    {
        if (transform.childCount == 0)
        {
            Debug.LogWarning("TurboFireVFX: primero crea un hijo con las 3 capas del fuego (será el nivel 1).", this);
            return;
        }

        Transform template = transform.GetChild(0);
        template.name = "Fuego_Nivel_1";

        for (int i = transform.childCount; i < levelsToCreate; i++)
        {
            GameObject copy = Instantiate(template.gameObject, transform);
            copy.name = "Fuego_Nivel_" + (i + 1);
            Undo.RegisterCreatedObjectUndo(copy, "Crear variante de fuego");
        }
    }
#endif
}