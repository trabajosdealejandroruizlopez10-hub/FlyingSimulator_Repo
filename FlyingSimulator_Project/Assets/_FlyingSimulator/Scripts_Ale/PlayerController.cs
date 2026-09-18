using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;


public class PlayerController : MonoBehaviour
{
    [SerializeField] public Rigidbody rb;
    [SerializeField] public GameObject camHolder;
    [SerializeField] public float speed = 5f;
    [SerializeField] public float sensitivity = 0.1f;
    [SerializeField] public float maxForce = 10f;
    [SerializeField] public float jumpForce = 5f;

    [Header("Ground check")]
    [SerializeField] public float groundCheckDistance = 1.1f;
    [SerializeField] public LayerMask groundMask = ~0;
    Vector2 move;
    Vector2 look;
    float lookRotation;
    bool jumpRequested;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnMove(InputAction.CallbackContext Context)
    {
        move = Context.ReadValue<Vector2>();
    }
    public void OnLook(InputAction.CallbackContext Context)
    {
        look = Context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext Context)
    {
        if (Context.performed)
            jumpRequested = true;
    }


    private void FixedUpdate()
    {
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 targetVelocity = new Vector3(move.x, 0, move.y) * speed;
        targetVelocity = transform.TransformDirection(targetVelocity);

        Vector3 velocityChange = targetVelocity - currentVelocity;
        velocityChange.y = 0;
        velocityChange = Vector3.ClampMagnitude(velocityChange, maxForce);

        rb.AddForce(velocityChange, ForceMode.VelocityChange);

        if (jumpRequested && IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        jumpRequested = false;
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundMask);
    }

    private void LateUpdate()
    {
        transform.Rotate(Vector3.up * look.x * sensitivity);


        lookRotation -= look.y * sensitivity;
        lookRotation = Mathf.Clamp(lookRotation, -90f, 90f);
        camHolder.transform.localEulerAngles = new Vector3(lookRotation, 0f, 0f);
    }
}
