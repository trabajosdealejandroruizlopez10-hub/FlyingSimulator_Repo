using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.Rendering;
public class PlayerInteractable : MonoBehaviour
{
    [SerializeField] Transform Origin;
    [SerializeField] float originheight = 1f;
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] float interactRadius = 0.3f;
    [SerializeField] LayerMask interactableMask;
    [SerializeField] TMP_Text promptText;
    [SerializeField] IInteractable current;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed && current != null && current.CanInteract)
            current.Interact();
    }

    // Update is called once per frame
    void Update()
    {
        IInteractable found = null;

        Vector3 origin = this.Origin == null
        ? this.Origin.position
        : transform.position + Vector3.up * originheight;

        Vector3 direction = transform.forward;

        if (Physics.SphereCast(origin, interactRadius, direction, out RaycastHit hit, interactRange, interactableMask))
        {
            found = hit.collider.GetComponentInParent<IInteractable>();
        }

        if (found == current)
        {
            current = found;
            UpdatePrompt();
        }
        else if (current == null)
        {
            UpdatePrompt();
        }
    }

    private void UpdatePrompt()
    {
        if (promptText == null) return;

        bool show = current != null && current.CanInteract;
        promptText.gameObject.SetActive(show);
        if (show) promptText.text = current.Prompt;
    }
}
