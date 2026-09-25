using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AirplaneController : MonoBehaviour
{
    public float FlySpeed = 5;
    public float YawAmount = 120;
    public float PitchAmount = 60;

    private float Yaw;
    private float Pitch;

    private Vector2 moveInput;

    void Update()
    {
        // Move forward
        transform.position += transform.forward * FlySpeed * Time.deltaTime;

        // Yaw
        Yaw += moveInput.x * YawAmount * Time.deltaTime;

        // Pitch
        Pitch += -moveInput.y * PitchAmount * Time.deltaTime;

        // Roll visual
        float roll = Mathf.Lerp(0, 30, Mathf.Abs(moveInput.x))
                     * -Mathf.Sign(moveInput.x);

        // Apply rotation
        transform.rotation = Quaternion.Euler(
            Pitch,
            Yaw,
            roll
        );
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
}