using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MapDoorInteractor : MonoBehaviour
{
    [Header("UI")]
    public GameObject enterPrompt;

    [Header("Input")]
    [Range(0.5f, 0.95f)]
    public float rightScreenStart = 0.6f;

    [Header("Entry Animation")]
    [Min(0.05f)]
    public float enterDuration = 0.45f;

    [Range(0.1f, 1f)]
    public float finalVisualScale = 0.65f;

    public Transform visual;

    Rigidbody2D rb;
    Collider2D playerCollider;
    MapPlayerMove movement;
    MapDoor nearbyDoor;
    bool entering;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        movement = GetComponent<MapPlayerMove>();

        if (visual == null)
            visual = transform.Find("Visual");

        ShowPrompt(false);
    }

    void Update()
    {
        if (entering || nearbyDoor == null)
            return;

        Pointer pointer = Pointer.current;

        if (pointer != null && pointer.press.wasPressedThisFrame)
        {
            Vector2 position = pointer.position.ReadValue();

            if (position.x >= Screen.width * rightScreenStart)
                StartCoroutine(EnterDoor(nearbyDoor));
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null &&
            (keyboard.eKey.wasPressedThisFrame ||
             keyboard.enterKey.wasPressedThisFrame))
        {
            StartCoroutine(EnterDoor(nearbyDoor));
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent(out MapDoor door))
            return;

        nearbyDoor = door;
        ShowPrompt(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent(out MapDoor door))
            return;

        if (door != nearbyDoor || entering)
            return;

        nearbyDoor = null;
        ShowPrompt(false);
    }

    IEnumerator EnterDoor(MapDoor door)
    {
        if (door.entryPoint == null)
        {
            Debug.LogError(
                $"Door {door.name} has no Entry Point assigned.",
                door);

            yield break;
        }

        if (door.buildingCollider == null)
        {
            Debug.LogError(
                $"Door {door.name} has no Building Collider assigned.",
                door);

            yield break;
        }

        entering = true;
        ShowPrompt(false);

        if (movement != null)
            movement.enabled = false;

        rb.linearVelocity = Vector2.zero;

        // Ignore only this building. Other buildings remain solid.
        Physics2D.IgnoreCollision(
            playerCollider,
            door.buildingCollider,
            true);

        Vector2 startPosition = rb.position;
        Vector2 endPosition = door.entryPoint.position;

        Vector3 startScale =
            visual != null ? visual.localScale : Vector3.one;

        Vector3 endScale = startScale * finalVisualScale;

        float elapsed = 0f;

        while (elapsed < enterDuration)
        {
            elapsed += Time.fixedDeltaTime;

            float progress = Mathf.Clamp01(elapsed / enterDuration);
            float eased = Mathf.SmoothStep(0f, 1f, progress);

            rb.MovePosition(
                Vector2.Lerp(startPosition, endPosition, eased));

            if (visual != null)
                visual.localScale =
                    Vector3.Lerp(startScale, endScale, eased);

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(endPosition);

        if (CircleTransition.Instance != null)
        {
            CircleTransition.Instance.LoadScene(
                door.destinationScene,
                door.enterSound,
                door.enterVolume);
        }
        else
        {
            SceneManager.LoadScene(door.destinationScene);
        }
    }

    void ShowPrompt(bool visible)
    {
        if (enterPrompt != null)
            enterPrompt.SetActive(visible);
    }
}
