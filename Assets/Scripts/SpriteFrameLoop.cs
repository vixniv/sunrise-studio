using UnityEngine;

// Loops a set of sliced sprites on this SpriteRenderer, for characters that only have an idle sheet
// (no Animator Controller needed). Put it on the NPC's "Visual" child next to the SpriteRenderer.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFrameLoop : MonoBehaviour
{
    [Tooltip("Sliced frames in play order, e.g. dira-idle_0 .. dira-idle_3.")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Frames per second. Anin's idle runs at 5.")]
    [Min(0.01f)] [SerializeField] private float framesPerSecond = 5f;
    [Tooltip("Start each copy at a random point in the loop so NPCs standing together don't breathe in sync.")]
    [SerializeField] private bool randomStart = true;

    private SpriteRenderer spriteRenderer;
    private float startOffset;
    private int shownFrame = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        startOffset = randomStart && frames != null && frames.Length > 0
            ? Random.Range(0, frames.Length) / framesPerSecond
            : 0f;
        shownFrame = -1;
        Show();
    }

    private void Update()
    {
        Show();
    }

    private void Show()
    {
        if (frames == null || frames.Length == 0) return;
        // Scaled time, so the loop pauses with the game like an Animator would.
        int frame = (int)((Time.time + startOffset) * framesPerSecond) % frames.Length;
        if (frame == shownFrame) return;
        shownFrame = frame;
        spriteRenderer.sprite = frames[frame];
    }
}
