using UnityEngine;

// Moves room foreground art slightly against horizontal camera travel.
// Attach to the Foreground SpriteRenderer, not to characters or UI.
[DefaultExecutionOrder(1000)]
public sealed class ForegroundParallax2D : MonoBehaviour
{
    [Tooltip("The room's Main Camera. Leave empty to use the camera tagged MainCamera.")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Extra foreground movement for each unit of camera movement.")]
    [SerializeField, Range(0f, 1f)] private float strength = 0.15f;
    [Tooltip("Snap the parallax offset to this many pixels per Unity unit. The room art uses 16.")]
    [SerializeField, Min(1)] private int pixelsPerUnit = 16;

    private float startingForegroundX;
    private float startingCameraX;
    private bool initialized;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        // Capture after the camera has reached its first rendered position, so
        // entering a room does not shift the art before the player moves.
        if (!initialized)
        {
            startingForegroundX = transform.position.x;
            startingCameraX = targetCamera.transform.position.x;
            initialized = true;
            return;
        }

        float cameraTravel = targetCamera.transform.position.x - startingCameraX;
        float offset = Mathf.Round(cameraTravel * strength * pixelsPerUnit) / pixelsPerUnit;
        Vector3 position = transform.position;
        position.x = startingForegroundX - offset;
        transform.position = position;
    }
}
