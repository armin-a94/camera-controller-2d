using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Demo : MonoBehaviour
{

    private void Start()
    {
        CameraController.Singleton.onTap += OnTap;
    }

    private void OnTap(Vector2 position)
    {
        Debug.Log("Clicked On: " + position);
    }
    
}