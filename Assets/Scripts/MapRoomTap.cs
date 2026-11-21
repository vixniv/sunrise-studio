using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Attach to any Room GameObject (with a Collider2D) under PlayableMap/Rooms
// so tapping the room moves the MapPlayer icon to the center of the room and opens its scene.
[RequireComponent(typeof(Collider2D))]
public class MapRoomTap : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Optional override. Leave empty to use this Room GameObject's own name as the scene name.")]
    public string destinationScene = "";

    [Header("Player Icon Movement")]
    [Tooltip("Optional. Leave empty to automatically find MapPlayer in the scene.")]
    public Transform mapPlayer;

    [Tooltip("How long the player icon takes to glide to the middle of this room before loading the scene.")]
    [Min(0f)]
    public float moveDuration = 0.35f;

    [Header("Return Marker (Optional)")]
    [Tooltip("Optional custom point inside this room. Leave empty to use the exact center of this room's Collider2D.")]
    public Transform returnPoint;

    [Header("Tap Detection")]
    [Tooltip("Camera used to convert screen taps to world coordinates. Leave empty to use Camera.main.")]
    public Camera mapCamera;

    [SerializeField, Range(0.001f, 0.1f)]
    float tapMaxTravel = 0.03f;

    [SerializeField, Min(0.05f)]
    float tapMaxSeconds = 0.4f;

    static bool transitioning;

    Collider2D roomCollider;
    bool trackingTap;
    Vector2 pressPosition;
    float pressTime;
    float maxTravel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        transitioning = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        transitioning = false;
    }

    public string ResolvedDestinationScene =>
        !string.IsNullOrWhiteSpace(destinationScene)
            ? destinationScene.Trim()
            : gameObject.name.Trim();

    public bool LeadsTo(string sceneName) =>
        !string.IsNullOrWhiteSpace(sceneName) &&
        !string.IsNullOrWhiteSpace(ResolvedDestinationScene) &&
        string.Equals(
            ResolvedDestinationScene,
            sceneName.Trim(),
            StringComparison.OrdinalIgnoreCase);

    public Vector2 RoomCenter
    {
        get
        {
            if (returnPoint != null)
                return returnPoint.position;

            Collider2D col = roomCollider != null ? roomCollider : GetComponent<Collider2D>();
            return col != null ? (Vector2)col.bounds.center : (Vector2)transform.position;
        }
    }

    public Vector2 ReturnPosition => RoomCenter;

    void Awake()
    {
        roomCollider = GetComponent<Collider2D>();
    }

    void OnDisable()
    {
        trackingTap = false;
    }

    void Update()
    {
        if (transitioning || Time.timeScale <= 0f)
        {
            trackingTap = false;
            return;
        }

        if (CircleTransition.Instance != null && CircleTransition.Instance.IsTransitioning)
        {
            trackingTap = false;
            return;
        }

        Pointer pointer = Pointer.current;
        if (pointer == null)
        {
            trackingTap = false;
            return;
        }

        Vector2 position = pointer.position.ReadValue();

        if (pointer.press.wasPressedThisFrame)
        {
            if (InteractionCue.PointerOverUi(position) || !HitsThisRoom(position))
            {
                trackingTap = false;
                return;
            }

            trackingTap = true;
            pressPosition = position;
            pressTime = Time.unscaledTime;
            maxTravel = 0f;
        }

        if (!trackingTap)
            return;

        maxTravel = Mathf.Max(maxTravel, Vector2.Distance(position, pressPosition));

        if (!pointer.press.wasReleasedThisFrame)
            return;

        trackingTap = false;

        float screenScale = Mathf.Max(1f, Mathf.Max(Screen.width, Screen.height));
        bool isTap = maxTravel <= screenScale * tapMaxTravel &&
                     Time.unscaledTime - pressTime <= tapMaxSeconds;

        if (isTap && HitsThisRoom(position))
            EnterRoom();
    }

    bool HitsThisRoom(Vector2 screenPosition)
    {
        if (roomCollider == null || !roomCollider.enabled)
            return false;

        Camera cam = mapCamera != null ? mapCamera : Camera.main;
        if (cam == null)
            return false;

        float zDistance = Mathf.Abs(cam.transform.position.z);
        Vector3 worldPosition = cam.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, zDistance));

        return roomCollider.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
    }

    public void EnterRoom()
    {
        if (transitioning)
            return;

        string targetScene = ResolvedDestinationScene;
        if (string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.LogError($"{name}: MapRoomTap has no Destination Scene.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogWarning(
                $"{name}: scene \"{targetScene}\" is not in the Build Profile scene list.",
                this);
            return;
        }

        transitioning = true;
        StartCoroutine(MovePlayerAndEnter(targetScene));
    }

    IEnumerator MovePlayerAndEnter(string targetScene)
    {
        Transform player = ResolveMapPlayer();
        if (player != null)
        {
            if (player.TryGetComponent(out Rigidbody2D rb))
            {
                rb.linearVelocity = Vector2.zero;
                rb.simulated = false;
            }

            Vector3 startPos = player.position;
            Vector2 center = RoomCenter;
            Vector3 endPos = new(center.x, center.y, startPos.z);

            if (moveDuration > 0f && Vector2.Distance(startPos, endPos) > 0.001f)
            {
                float elapsed = 0f;
                while (elapsed < moveDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / moveDuration);
                    float eased = Mathf.SmoothStep(0f, 1f, progress);
                    player.position = Vector3.Lerp(startPos, endPos, eased);
                    yield return null;
                }
            }

            player.position = endPos;
        }

        if (CircleTransition.Instance != null)
            CircleTransition.Instance.LoadSceneWithBlackFade(targetScene);
        else
            SceneManager.LoadScene(targetScene);
    }

    Transform ResolveMapPlayer()
    {
        if (mapPlayer != null)
            return mapPlayer;

        MapPlayerMove move = FindAnyObjectByType<MapPlayerMove>();
        if (move != null)
            return move.transform;

        MapDoorInteractor interactor = FindAnyObjectByType<MapDoorInteractor>();
        return interactor != null ? interactor.transform : null;
    }
}
