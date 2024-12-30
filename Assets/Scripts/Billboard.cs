using UnityEngine;

/// <summary>
/// Makes a GameObject always face the main camera.
/// Attach this script to any GameObject you want to billboard.
/// </summary>
public class Billboard : MonoBehaviour
{
    void Update()
    {
        if (Camera.main != null)
        {
            // Make the label face the camera
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            
            // Ensure the scale remains unchanged
            // This line is optional and should remain commented out unless needed
            // transform.localScale = Vector3.one;
        }
    }
}