using UnityEngine;

public class TurboRing : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        AirplaneController airplane = other.GetComponent<AirplaneController>();

        if (airplane != null)
        {
            airplane.AddTurbo();
        }
    }
}