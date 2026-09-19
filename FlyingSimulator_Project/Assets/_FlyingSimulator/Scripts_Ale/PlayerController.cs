using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;


public class PlayerController : MonoBehaviour
{
    [SerializeField] public Rigidbody rb;
    [SerializeField] public Transform cam;
    [SerializeField] public float speed = 5f;
    [SerializeField] public float turnSpeed = 720f;
    [SerializeField] public float maxForce = 10f;
    [SerializeField] public float jumpForce = 5f;

    [Header("Ground check")]
    [SerializeField] public float groundCheckDistance = 1.1f;
    [SerializeField] public LayerMask groundMask = ~0;
    
    Vector2 move;
    bool jumpRequested;

    private void Start()
    {
        if (cam == null)
            cam = Camera.main.transform;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            jumpRequested = true;
    }


    private void FixedUpdate()
    {
        Vector3 camForward = cam.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cam.right;
        camRight.y = 0;
        camRight.Normalize();

        Vector3 moveDir = camForward * move.y + camRight * move.x;
        moveDir = Vector3.ClampMagnitude(moveDir, 1f);





        Vector3 targetVelocity = moveDir * speed;
        Vector3 velocityChange = targetVelocity - rb.linearVelocity;
        velocityChange.y = 0;
        velocityChange = Vector3.ClampMagnitude(velocityChange, maxForce);
        rb.AddForce(velocityChange, ForceMode.VelocityChange);
        
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime));
        }
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

}
