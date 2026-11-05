using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Menusystem : MonoBehaviour
{
    [Tooltip("Botón que empieza seleccionado (hace falta para que el mando funcione)")]
    [SerializeField] private Button primerBoton;

    [Header("Transición")]
    [Tooltip("Nombre de la escena de juego. Vacío = la siguiente escena de Build Settings")]
    [SerializeField] private string escenaJuego = "";

    private GameObject ultimoSeleccionado;

    private void Start()
    {
        if (primerBoton != null)
            primerBoton.Select();
    }

    private void Update()
    {
        // Si se hace clic en un hueco vacío, Unity deselecciona todo y el mando deja de
        // responder. Aquí se recupera el último botón seleccionado.
        var es = EventSystem.current;
        if (es == null) return;

        if (es.currentSelectedGameObject != null)
            ultimoSeleccionado = es.currentSelectedGameObject;
        else if (ultimoSeleccionado != null && ultimoSeleccionado.activeInHierarchy)
            es.SetSelectedGameObject(ultimoSeleccionado);
    }

    public void Jugar()
    {
        if (string.IsNullOrEmpty(escenaJuego))
            SceneTransition.Load(SceneManager.GetActiveScene().buildIndex + 1);
        else
            SceneTransition.Load(escenaJuego);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // Application.Quit() no hace nada en el editor
#else
        Application.Quit();
#endif
    }
}