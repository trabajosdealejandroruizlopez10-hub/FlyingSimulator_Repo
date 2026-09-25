using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AirplaneController : MonoBehaviour
{
    [SerializeField] float FlySpeed = 5;
    [SerializeField] float YawAmount = 120;
    [SerializeField] float PitchAmount = 60;

    // TURBO
    [SerializeField] float MaxTurboLevel = 5;
    [SerializeField] float TurboDecayTime = 2f;

    private float turboLevel = 0;
    private float turboTimer = 0f;

    private float Yaw;
    private float Pitch;

    private Vector2 moveInput;

    void Update()
    {
        // =========================
        // MOVIMIENTO
        // =========================

        // Yaw
        Yaw += moveInput.x * YawAmount * Time.deltaTime;

        // Pitch
        Pitch += -moveInput.y * PitchAmount * Time.deltaTime;

        // Limitar pitch
        Pitch = Mathf.Clamp(Pitch, -80f, 80f);

        // Roll visual
        float roll = Mathf.Lerp(0, 30, Mathf.Abs(moveInput.x))
                     * -Mathf.Sign(moveInput.x);

        // Aplicar rotación
        transform.rotation = Quaternion.Euler(
            Pitch,
            Yaw,
            roll
        );


        // =========================
        // TURBO
        // =========================

        if (turboLevel > 0)
        {
            turboTimer -= Time.deltaTime;

            if (turboTimer <= 0)
            {
                turboLevel--;
                turboTimer = TurboDecayTime;
            }
        }


        // =========================
        // VELOCIDAD
        // =========================

        float currentSpeed = FlySpeed;

        // Cada nivel aumenta la velocidad
        currentSpeed += turboLevel * 2f;

        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }


    // =========================
    // TURBO
    // =========================

    public void AddTurbo()
    {
        turboLevel++;

        // Máximo 5
        turboLevel = Mathf.Clamp(turboLevel, 0, MaxTurboLevel);

        // Reinicia el tiempo de descenso
        turboTimer = TurboDecayTime;

        Debug.Log("Turbo Level: " + turboLevel);
    }


    public float GetTurboLevel()
    {
        return turboLevel;
    }


   
    // INPUT
 

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }
}