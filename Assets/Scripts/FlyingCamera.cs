using UnityEngine;
using TMPro;

public class FlyingCamera : MonoBehaviour
{
    public float movementSpeed = 10f;
    public float sprintMultiplier = 2f;
    public float mouseSensitivity = 3f;
    public bool invertMouseY = false;
    public float zoomMagnitude = 20f; // Adjustable zoom magnitude
    public float zoomSpeed = 5f; // Speed of zooming in and out

    [Header("Pause UI")]
    public TextMeshProUGUI pauseIndicator;

    private Vector3 velocity;
    private float yaw;
    private float pitch;
    private bool isPaused = false;
    private Camera cameraComponent; // Camera component reference
    private float defaultFOV; // Default field of view

    void Start()
    {
        // Initialize rotation based on current transform
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        // Hide the pause UI at start, if assigned
        if (pauseIndicator != null)
            pauseIndicator.gameObject.SetActive(false);

        // Get the Camera component and save the default field of view
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent != null)
        {
            defaultFOV = cameraComponent.fieldOfView;
        }
        else
        {
            Debug.LogError("No Camera component found on this GameObject!");
        }
    }

    void Update()
    {
        // If paused, skip camera movement & rotation
        if (isPaused) return;

        HandleMouseLook();
        HandleMovement();
        HandleZoom();
    }

    /// <summary>
    /// Called by the menu to pause/unpause the camera.
    /// </summary>
    public void SetPaused(bool paused)
    {
        isPaused = paused;

        if (isPaused)
        {
            // Unlock cursor so user can click UI
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Show "Paused" indicator, if any
            if (pauseIndicator != null)
                pauseIndicator.gameObject.SetActive(true);
        }
        else
        {
            // Lock cursor so user can move camera
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Hide "Paused" indicator
            if (pauseIndicator != null)
                pauseIndicator.gameObject.SetActive(false);
        }
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= (invertMouseY ? -1 : 1) * mouseY;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void HandleMovement()
    {
        velocity = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) velocity += transform.forward;
        if (Input.GetKey(KeyCode.S)) velocity -= transform.forward;
        if (Input.GetKey(KeyCode.A)) velocity -= transform.right;
        if (Input.GetKey(KeyCode.D)) velocity += transform.right;

        if (Input.GetKey(KeyCode.Space)) velocity += transform.up;
        if (Input.GetKey(KeyCode.LeftShift)) velocity -= transform.up;

        if (velocity.magnitude > 1f)
            velocity.Normalize();

        float speed = movementSpeed;
        if (Input.GetKey(KeyCode.LeftControl))
            speed *= sprintMultiplier;

        transform.position += velocity * speed * Time.deltaTime;
    }

    void HandleZoom()
    {
        if (cameraComponent == null) return;

        // Pressing "C" zooms in
        if (Input.GetKey(KeyCode.C))
        {
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, defaultFOV - zoomMagnitude, Time.deltaTime * zoomSpeed);
        }
        // Releasing "C" zooms out
        else
        {
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, defaultFOV, Time.deltaTime * zoomSpeed);
        }
    }
}