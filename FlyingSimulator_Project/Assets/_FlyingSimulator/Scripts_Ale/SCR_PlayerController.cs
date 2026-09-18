using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class SCR_PlayerController : MonoBehaviour
{
    [SerializeField] public Rigidbody rb;
    [SerializeField] public GameObject camHolder;
    [SerializeField] public float speed, sensitivity, maxForce;
    Vector2 move, look;
    float lookRotation;

    public void OnMove(InputAction.CallbackContext Context)
    {
        move = Context.ReadValue<Vector2>();
    }
    public void OnLook(InputAction.CallbackContext Context)
    {
        look = Context.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 targetVelocity = new Vector3(move.x, 0, move.y);
        targetVelocity *= speed;

        targetVelocity = transform.TransformDirection(targetVelocity);

        Vector3 velocityChange = (targetVelocity - currentVelocity);

        Vector3.ClampMagnitude(velocityChange, maxForce);

        rb.AddForce(velocityChange, ForceMode.velocityChange);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
