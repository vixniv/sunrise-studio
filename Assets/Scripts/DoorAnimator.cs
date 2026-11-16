using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Plays a door's opening and closing frames on this SpriteRenderer. Put it on the door sprite object in a room.
// Yarn drives it by Door Id:
//   <<open_door "Dorm">> / <<close_door "Dorm">>     open or close and wait for the animation
//   <<door_enter "Kirana" "Dorm">>                   door opens, Kirana walks in to where she is placed in the scene, door closes behind her
//   <<door_enter_to "Kirana" "Dorm" -6.5>>            same, but she walks to world X -6.5
//   <<door_exit "Kirana" "Dorm">>                    Kirana walks to the door, it opens, she leaves, it closes
// Optional: a SceneDoor with this assigned as its Door Animation also opens it when the player leaves, and plays it
// closing when the player comes back in through that door. Leave that field empty to animate only from Yarn.
// With Hide When Closed, the sprite shows only while the door is open or moving, for rooms whose background
// already paints the closed door (the Dorm).
[RequireComponent(typeof(SpriteRenderer))]
public class DoorAnimator : MonoBehaviour
{
    private static readonly List<DoorAnimator> doors = new List<DoorAnimator>();

    [Tooltip("Name used by the Yarn door commands, such as \"Dorm\" in <<open_door \"Dorm\">>. Case and spaces are ignored.")]
    [SerializeField] private string doorId = "Dorm";
    [Tooltip("Frames from closed to fully open, in play order, e.g. door-open_0 .. door-open_3.")]
    [SerializeField] private Sprite[] openingFrames;
    [Tooltip("Frames from fully open to closed, in play order, e.g. door-close_0 .. door-close_3.")]
    [SerializeField] private Sprite[] closingFrames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 12f;
    [Tooltip("Show the door open when the scene starts.")]
    [SerializeField] private bool startOpen;
    [Tooltip("Hide the sprite while the door rests closed, because the room background already paints the closed door. " +
             "It shows only while the door opens, stands open, or closes.")]
    [SerializeField] private bool hideWhenClosed = true;
    [Tooltip("Play door_open and door_close from the SunriseAudioLibrary when Yarn opens or closes the door.")]
    [SerializeField] private bool playSounds = true;

    [Header("Characters")]
    [Tooltip("Where characters appear and disappear in the doorway; only its X is used. Leave empty to use the middle of the door sprite.")]
    [SerializeField] private Transform walkPoint;
    [Tooltip("Walk speed of <<door_enter>> and <<door_exit>>, in units per second. The Yarn scripts use 10 for <<move_character>>.")]
    [SerializeField, Min(0.01f)] private float walkSpeed = 10f;
    [Tooltip("The door starts closing once the character entering is this far from the doorway, and starts opening once " +
             "the character leaving is this close to it.")]
    [SerializeField, Min(0f)] private float doorwayDistance = 1.5f;
    [Tooltip("When the player comes back in through the SceneDoor, seconds the door stays open before it closes.")]
    [SerializeField, Min(0f)] private float arrivalCloseDelay = 0.35f;

    private SpriteRenderer spriteRenderer;
    private Coroutine playing;
    private float fallbackWalkX;

    public bool IsOpen { get; private set; }
    public bool IsAnimating => playing != null;
    public float WalkSpeed => walkSpeed;
    public float DoorwayDistance => doorwayDistance;
    public float WalkPointX => walkPoint != null ? walkPoint.position.x : fallbackWalkX;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => doors.Clear();

    // The enabled door with this Door Id, ignoring case and spaces.
    public static bool TryFind(string id, out DoorAnimator door)
    {
        string key = Key(id);
        door = doors.Find(candidate => candidate != null && Key(candidate.doorId) == key);
        return door != null;
    }

    public static string KnownIds()
    {
        List<string> ids = new List<string>();
        foreach (DoorAnimator door in doors)
            if (door != null) ids.Add($"\"{door.doorId}\"");
        return ids.Count > 0 ? string.Join(", ", ids) : "none";
    }

    private static string Key(string value) => (value ?? string.Empty).Replace(" ", string.Empty).Trim().ToLowerInvariant();

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        IsOpen = startOpen;
        // Measured on the closed frame, so it stays put while the frames change width.
        Sprite closed = ClosedSprite;
        fallbackWalkX = closed != null ? transform.TransformPoint(closed.bounds.center).x : transform.position.x;
        ShowRestingFrame();
        if (ClosedSprite == null || OpenSprite == null)
            Debug.LogWarning($"{name}: DoorAnimator needs Opening Frames and Closing Frames.", this);
    }

    private void OnEnable() => doors.Add(this);

    private void OnDisable()
    {
        doors.Remove(this);
        // A disabled object stops its coroutines, so finish on the resting frame.
        if (playing != null)
        {
            playing = null;
            ShowRestingFrame();
        }
    }

    private Sprite ClosedSprite =>
        closingFrames != null && closingFrames.Length > 0 ? closingFrames[closingFrames.Length - 1] :
        openingFrames != null && openingFrames.Length > 0 ? openingFrames[0] : null;

    private Sprite OpenSprite =>
        openingFrames != null && openingFrames.Length > 0 ? openingFrames[openingFrames.Length - 1] :
        closingFrames != null && closingFrames.Length > 0 ? closingFrames[0] : null;

    public void Open() => Play(true, playSounds);
    public void Close() => Play(false, playSounds);

    // Open without a sound, for a SceneDoor whose scene transition already plays door_open.
    public void OpenSilently() => Play(true, false);

    // The player just came in through this door: the door shows open, then closes. Silent, because
    // SunriseAudio already plays door_close when the scene loads.
    public void PlayArrival()
    {
        StopPlaying();
        IsOpen = true;
        ShowRestingFrame();
        if (isActiveAndEnabled)
            playing = StartCoroutine(ArrivalRoutine());
    }

    private IEnumerator ArrivalRoutine()
    {
        if (arrivalCloseDelay > 0f)
            yield return new WaitForSeconds(arrivalCloseDelay);
        playing = null;
        Play(false, false);
    }

    private void Play(bool open, bool withSound)
    {
        if (IsOpen == open && playing == null)
            return;

        StopPlaying();
        IsOpen = open;
        if (withSound)
            SunriseAudio.Play(open ? "door_open" : "door_close");

        Sprite[] frames = open ? openingFrames : closingFrames;
        if (!isActiveAndEnabled || frames == null || frames.Length == 0)
        {
            ShowRestingFrame();
            return;
        }
        spriteRenderer.enabled = true;
        playing = StartCoroutine(PlayFrames(frames));
    }

    private IEnumerator PlayFrames(Sprite[] frames)
    {
        float frameSeconds = 1f / framesPerSecond;
        foreach (Sprite frame in frames)
        {
            if (frame != null)
                spriteRenderer.sprite = frame;
            yield return new WaitForSeconds(frameSeconds);
        }
        playing = null;
        ShowRestingFrame();
    }

    private void StopPlaying()
    {
        if (playing == null)
            return;
        StopCoroutine(playing);
        playing = null;
    }

    private void ShowRestingFrame()
    {
        if (spriteRenderer == null)
            return;
        Sprite sprite = IsOpen ? OpenSprite : ClosedSprite;
        if (sprite != null)
            spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = IsOpen || !hideWhenClosed;
    }
}
