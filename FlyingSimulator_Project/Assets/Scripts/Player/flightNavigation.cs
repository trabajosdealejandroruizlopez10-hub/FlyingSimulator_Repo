using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AirplaneController : MonoBehaviour
{
    public float FlySpeed = 5;
    public float YawAmount = 120;

    private float Yaw;

    private Vector2 moveInput;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // move forward
        transform.position += transform.forward * FlySpeed * Time.deltaTime;

        // yaw, pitch, roll
        Yaw += moveInput.x * YawAmount * Time.deltaTime;
        float pitch = Mathf.Lerp(0, 20, Mathf.Abs(moveInput.y)) * Mathf.Sign(moveInput.y);
        float roll = Mathf.Lerp(0, 30, Mathf.Abs(moveInput.x)) * -Mathf.Sign(moveInput.x); 

        // apply rotation
        transform.rotation = Quaternion.Euler(Vector3.up * Yaw + Vector3.right * pitch + Vector3.forward * roll);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
               moveInput = context.ReadValue<Vector2>();
    }
}
