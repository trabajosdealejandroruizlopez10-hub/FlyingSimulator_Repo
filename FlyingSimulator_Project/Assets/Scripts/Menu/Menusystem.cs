using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement
public class Menusystem : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Load the main menu scene
        SceneManager.LoadScene("MainMenu");
    }
}
