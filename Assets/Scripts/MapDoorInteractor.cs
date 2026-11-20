using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MapDoorInteractor : MonoBehaviour
{
    [Header("Tap To Enter")]
    [Tooltip("Legacy tap-to-enter on MapDoors. Keep off when using MapRoomTap directly on Rooms.")]
    public bool tapToEnterRooms = false;

    [Tooltip("Camera used to convert screen taps to map coordinates. Leave empty to use Camera.main.")]
    public Camera mapCamera;

    [SerializeField, Range(0.001f, 0.1f)]
    float tapMaxTravel = 0.03f;

    [SerializeField, Min(0.05f)]
    float tapMaxSeconds = 0.4f;

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

    readonly List<Collider2D> tapHits = new();
    ContactFilter2D tapFilter;
    MapDoor[] mapDoors;
    bool hasRoomTaps;

    Rigidbody2D rb;
    Collider2D playerCollider;
    MapPlayerMove movement;
    MapDoor nearbyDoor;
    bool entering;
    bool trackingTap;
    Vector2 tapPressPosition;
    float tapPressTime;
    float tapMaxObservedTravel;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();
        movement = GetComponent<MapPlayerMove>();
        mapDoors = FindObjectsByType<MapDoor>(FindObjectsSortMode.None);
        hasRoomTaps = FindAnyObjectByType<MapRoomTap>() != null;
        tapFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false
        };

        if (visual == null)
            visual = transform.Find("Visual");

        ShowPrompt(false);
    }

    void Update()
    {
        if (entering || Time.timeScale <= 0f)
        {
            trackingTap = false;
            return;
        }

        if (hasRoomTaps)
        {
            trackingTap = false;
            return;
        }

        if (CircleTransition.Instance != null && CircleTransition.Instance.IsTransitioning)
        {
            trackingTap = false;
            return;
        }

        if (tapToEnterRooms)
        {
            ReadRoomTap();
            return;
        }

        if (nearbyDoor == null)
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
            TryEnterNearbyDoor();
        }
    }

    void ReadRoomTap()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            trackingTap = false;
            return;
        }

        Vector2 position = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            if (InteractionCue.PointerOverUi(position))
            {
                trackingTap = false;
                return;
            }

            trackingTap = true;
            tapPressPosition = position;
            tapPressTime = Time.unscaledTime;
            tapMaxObservedTravel = 0f;
        }

        if (!trackingTap)
            return;

        tapMaxObservedTravel = Mathf.Max(
            tapMaxObservedTravel,
            Vector2.Distance(position, tapPressPosition));

        if (!pointer.press.wasReleasedThisFrame)
            return;

        trackingTap = false;

        float screenScale = Mathf.Max(1f, Mathf.Max(Screen.width, Screen.height));
        bool isTap = tapMaxObservedTravel <= screenScale * tapMaxTravel &&
                     Time.unscaledTime - tapPressTime <= tapMaxSeconds;

        if (isTap)
            TryEnterRoomAtScreenPosition(position);
    }

    void TryEnterRoomAtScreenPosition(Vector2 screenPosition)
    {
        Camera cam = mapCamera != null ? mapCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogError("MapDoorInteractor: no active Camera found to detect room taps.", this);
            return;
        }

        float zDistance = Mathf.Abs(cam.transform.position.z);
        Vector3 worldPosition = cam.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, zDistance));
        Vector2 worldPoint = new(worldPosition.x, worldPosition.y);

        tapHits.Clear();
        int hitCount = Physics2D.OverlapPoint(worldPoint, tapFilter, tapHits);
        if (hitCount == 0)
            return;

        if (mapDoors == null || mapDoors.Length == 0)
            mapDoors = FindObjectsByType<MapDoor>(FindObjectsSortMode.None);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = tapHits[i];
            MapDoor door = FindDoorForCollider(hit);
            if (door != null)
            {
                EnterTappedDoor(door);
                return;
            }
        }
    }

    MapDoor FindDoorForCollider(Collider2D hit)
    {
        if (hit == null || mapDoors == null)
            return null;

        for (int i = 0; i < mapDoors.Length; i++)
        {
            MapDoor door = mapDoors[i];
            if (door != null && door.isActiveAndEnabled && door.ContainsCollider(hit))
                return door;
        }

        return null;
    }

    void EnterTappedDoor(MapDoor door)
    {
        if (entering || door == null)
            return;

        if (string.IsNullOrWhiteSpace(door.destinationScene))
        {
            SunriseAudio.Play("ui_error");
            Debug.LogError($"Door {door.name} has no Destination Scene assigned.", door);
            return;
        }

        entering = true;
        ShowPrompt(false);

        if (movement != null)
            movement.enabled = false;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        LoadDestination(door);
    }

    // Public UI hook for the map's on-screen Enter button.
    public void TryEnterNearbyDoor()
    {
        if (tapToEnterRooms || entering || nearbyDoor == null)
            return;

        StartCoroutine(EnterDoor(nearbyDoor));
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (tapToEnterRooms)
            return;

        if (!other.TryGetComponent(out MapDoor door))
            return;

        nearbyDoor = door;
        ShowPrompt(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (tapToEnterRooms)
            return;

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

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // Ignore only this building. Other buildings remain solid.
        if (playerCollider != null)
        {
            Physics2D.IgnoreCollision(
                playerCollider,
                door.buildingCollider,
                true);
        }

        Vector2 startPosition = rb != null ? rb.position : (Vector2)transform.position;
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

            Vector2 nextPosition = Vector2.Lerp(startPosition, endPosition, eased);
            if (rb != null)
                rb.MovePosition(nextPosition);
            else
                transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

            if (visual != null)
                visual.localScale =
                    Vector3.Lerp(startScale, endScale, eased);

            yield return new WaitForFixedUpdate();
        }

        if (rb != null)
            rb.MovePosition(endPosition);
        else
            transform.position = new Vector3(endPosition.x, endPosition.y, transform.position.z);

        LoadDestination(door);
    }

    void LoadDestination(MapDoor door)
    {
        string targetScene = door.destinationScene.Trim();
        SunriseAudio.QueueDoorClose(door.enterVolume);

        if (CircleTransition.Instance != null)
        {
            CircleTransition.Instance.LoadScene(
                targetScene,
                door.enterEvent,
                "door_open",
                door.enterSound,
                door.enterVolume);
        }
        else
        {
            if (door.enterEvent.IsNull || !SunriseAudio.TryPlayFmodEvent(door.enterEvent, door.enterVolume))
                SunriseAudio.TryPlay("door_open", door.enterVolume);
            SceneManager.LoadScene(targetScene);
        }
    }

    void ShowPrompt(bool visible)
    {
        if (enterPrompt != null)
            enterPrompt.SetActive(visible);
    }
}
