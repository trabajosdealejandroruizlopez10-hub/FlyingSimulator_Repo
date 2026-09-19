using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CameraOrbit : MonoBehaviour
{
    [Header("Obetico")]
    [SerializeField] Transform target;
    [SerializeField] float height = 1.6f;

    [Header("Órbita")]
    [SerializeField] float distance = 5f;
    [SerializeField] float sensitivity = 0.1f;
    [SerializeField] float minPitch = -30f;
    [SerializeField] float maxPitch = 70f;

    [Header("Colisiones")]
    [SerializeField] LayerMask collisionMask = ~0;
    [SerializeField] float collisionRadius = 0.25f;
    [SerializeField] float minDistance = 0.5f;

    Vector2 look;
    float yaw;
    float pitch = 15f;


    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (target !=null)
            yaw = target.eulerAngles.y;
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        look = context.ReadValue<Vector2>();
    }

    void LateUpdate()
    {
        if (target == null) return;

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 pivot = target.position + Vector3.up * height;

        Vector3 direction = rotation * Vector3.back;
        float finalDistance = distance;


        if (Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            finalDistance = Mathf.Max(hit.distance, minDistance);
        }

        transform.position = pivot + direction * finalDistance;
        transform.rotation = rotation;
    }
}
