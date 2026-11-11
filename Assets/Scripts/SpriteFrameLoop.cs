using UnityEngine;

// Loops a set of sliced sprites on this SpriteRenderer, for characters without an Animator Controller.
// Put it on the NPC's "Visual" child next to the SpriteRenderer.
// With Walk Frames assigned, Yarn moves (<<move_character>>, <<start_character_move>>) play the walk loop
// while the character moves and return to the idle loop when it arrives.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameLoop : MonoBehaviour
{
    private enum MoveState { Idle, Walk, Run }

    [Tooltip("Sliced idle frames in play order, e.g. dira-idle_0 .. dira-idle_3.")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Idle frames per second. Anin's idle runs at 5.")]
    [Min(0.01f)] [SerializeField] private float framesPerSecond = 5f;
    [Tooltip("Start each copy at a random point in the idle loop so NPCs standing together don't breathe in sync.")]
    [SerializeField] private bool randomStart = true;

    [Header("Walk")]
    [Tooltip("Sliced walk frames in play order, e.g. dira-walk_0 .. dira-walk_7. Leave empty to slide in idle.")]
    [SerializeField] private Sprite[] walkFrames;
    [Tooltip("Walk frames per second. Anin's walk runs at 10.")]
    [Min(0.01f)] [SerializeField] private float walkFramesPerSecond = 10f;
    [Tooltip("A \"run\" move plays the walk frames this many times faster (there is no separate run sheet).")]
    [Min(0.01f)] [SerializeField] private float runFrameRateMultiplier = 1.5f;

    private SpriteRenderer spriteRenderer;
    private float startOffset;
    private MoveState moveState = MoveState.Idle;
    private float moveStartTime;

    public bool HasWalkFrames => walkFrames != null && walkFrames.Length > 0;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        startOffset = randomStart && frames != null && frames.Length > 0
            ? Random.Range(0, frames.Length) / framesPerSecond
            : 0f;
        Show();
    }

    private void Update()
    {
        Show();
    }

    // Called by YarnCommands when a cutscene move starts (walking = true) and when it ends (walking = false).
    public void SetMoving(bool walking, bool running)
    {
        MoveState next = !walking ? MoveState.Idle : running ? MoveState.Run : MoveState.Walk;
        if (next == moveState) return;

        moveState = next;
        // Each walk starts on its first frame.
        moveStartTime = Time.time;
        if (spriteRenderer != null) Show();
    }

    private void Show()
    {
        Sprite[] sheet;
        float time;
        float fps;
        if (moveState != MoveState.Idle && HasWalkFrames)
        {
            sheet = walkFrames;
            time = Time.time - moveStartTime;
            fps = walkFramesPerSecond * (moveState == MoveState.Run ? runFrameRateMultiplier : 1f);
        }
        else
        {
            sheet = frames;
            time = Time.time + startOffset;
            fps = framesPerSecond;
        }

        if (sheet == null || sheet.Length == 0) return;
        // Scaled time, so the loop pauses with the game like an Animator would.
        Sprite sprite = sheet[(int)(time * fps) % sheet.Length];
        if (sprite != spriteRenderer.sprite)
            spriteRenderer.sprite = sprite;
    }
}
