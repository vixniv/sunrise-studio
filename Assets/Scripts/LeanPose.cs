using UnityEngine;

// Plays a looping leaning animation (back against a railing or wall) on a character's "Visual" sprite,
// for Anin (Animator) and the NPCs (SpriteFrameLoop). Works like SitPose: put it on the "Visual" child
// next to the SpriteRenderer. While leaning it pauses the Animator / SpriteFrameLoop on the same object
// and loops Lean Frames instead.
// Yarn: <<lean "Name">> and <<stand "Name">>, or the facing "lean" in the teleport commands, e.g.
// <<fade_teleport_character_to_actor "Anin" "Fal" -1.6875 "lean">>.
// Tick Start Leaning for characters already leaning when the scene opens.
// A <<move_character>> or the player dragging to walk makes the character stand up first.
[RequireComponent(typeof(SpriteRenderer))]
public class LeanPose : MonoBehaviour
{
    [Tooltip("Sliced leaning frames in play order, e.g. fal-leaning_0 .. fal-leaning_2.")]
    [SerializeField] private Sprite[] leanFrames;
    [Tooltip("Lean frames per second. Idle loops run at 5.")]
    [Min(0.01f)] [SerializeField] private float framesPerSecond = 5f;
    [Tooltip("Start each copy at a random point in the loop so characters leaning together don't breathe in sync.")]
    [SerializeField] private bool randomStart = true;
    [Tooltip("Lean as soon as this object is enabled, e.g. Fal already at the railing when the scene opens.")]
    [SerializeField] private bool startLeaning;

    [Header("Placement")]
    [Tooltip("Added to this Visual's local position while leaning, in units (1 pixel = 0.0625). " +
             "Use it to line the feet up with the floor or the back with the railing without moving the root.")]
    [SerializeField] private Vector2 leanOffset = Vector2.zero;
    [Tooltip("Multiplies this Visual's scale while leaning, e.g. 0.9 to draw the character a little smaller " +
             "(further back). Scales from the sprite pivot (the feet). 1 = unchanged. " +
             "Values other than 1 resize the pixel art, so its pixels are no longer an exact 1:1 grid.")]
    [Min(0.01f)] [SerializeField] private float leanScale = 1f;
    [Tooltip("The lean art faces the camera: keep the sprite unflipped while leaning. " +
             "A <<face_character>> while leaning is remembered for when the character stands up.")]
    [SerializeField] private bool faceFront = true;

    [Header("Sorting (optional)")]
    [Tooltip("Tick to draw the character on another sorting layer/order while leaning, e.g. in front of or behind a railing.")]
    [SerializeField] private bool overrideSorting;
    [SerializeField] private string leanSortingLayer = "Default";
    [SerializeField] private int leanSortingOrder = 5;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private SpriteFrameLoop frameLoop;
    private PlayerMove playerMove;
    private SitPose sitPose;

    private bool leaning;
    private float leanStartTime;
    private bool animatorWasEnabled;
    private bool frameLoopWasEnabled;
    private Vector3 standLocalPosition;
    private Vector3 standLocalScale;
    private bool standFlipX;
    private int standSortingLayerId;
    private int standSortingOrder;

    public bool IsLeaning => leaning;

    private void Awake()
    {
        CacheComponents();
    }

    // Yarn can call Lean/Stand on a Visual that has never been active, so Awake may not have run yet.
    private void CacheComponents()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        frameLoop = GetComponent<SpriteFrameLoop>();
        playerMove = GetComponentInParent<PlayerMove>(true);
        sitPose = GetComponent<SitPose>();
    }

    private void OnEnable()
    {
        if (startLeaning) Lean();
    }

    public void Lean()
    {
        if (leaning) return;
        CacheComponents();
        if (leanFrames == null || leanFrames.Length == 0)
        {
            Debug.LogError($"LeanPose on \"{name}\" has no Lean Frames assigned.", this);
            return;
        }

        // Both poses pause the same Animator and move the same Visual, so only one may run at a time.
        if (sitPose != null && sitPose.IsSitting) sitPose.Stand();

        leaning = true;
        leanStartTime = Time.time - (randomStart ? Random.Range(0, leanFrames.Length) / framesPerSecond : 0f);

        if (animator != null)
        {
            animatorWasEnabled = animator.enabled;
            animator.enabled = false;
        }
        if (frameLoop != null)
        {
            frameLoopWasEnabled = frameLoop.enabled;
            frameLoop.enabled = false;
        }

        standLocalPosition = transform.localPosition;
        transform.localPosition = standLocalPosition + (Vector3)leanOffset;
        standLocalScale = transform.localScale;
        transform.localScale = LeanLocalScale;

        standFlipX = spriteRenderer.flipX;
        if (faceFront) spriteRenderer.flipX = false;
        standSortingLayerId = spriteRenderer.sortingLayerID;
        standSortingOrder = spriteRenderer.sortingOrder;
        if (overrideSorting)
        {
            spriteRenderer.sortingLayerName = leanSortingLayer;
            spriteRenderer.sortingOrder = leanSortingOrder;
        }

        ShowFrame();
    }

    public void Stand()
    {
        if (!leaning) return;
        leaning = false;

        transform.localPosition = standLocalPosition;
        transform.localScale = standLocalScale;
        if (faceFront) spriteRenderer.flipX = standFlipX;
        if (overrideSorting)
        {
            spriteRenderer.sortingLayerID = standSortingLayerId;
            spriteRenderer.sortingOrder = standSortingOrder;
        }

        if (frameLoop != null && frameLoopWasEnabled) frameLoop.enabled = true;
        if (animator != null && animatorWasEnabled) animator.enabled = true;
    }

    private void Update()
    {
        if (!leaning) return;

        // The player gets up as soon as they walk (drag, keyboard, or a scripted WalkTo).
        if (playerMove != null && playerMove.MoveX != 0f)
        {
            Stand();
            return;
        }

#if UNITY_EDITOR
        // Lets Lean Offset and Lean Scale be tuned in the Inspector during Play mode and seen right away.
        transform.localPosition = standLocalPosition + (Vector3)leanOffset;
        transform.localScale = LeanLocalScale;
#endif
        ShowFrame();
    }

    // Called by <<face_character>> / <<teleport_character>> while leaning.
    // With Face Front the lean art stays unflipped and the facing is used when the character stands up.
    public void SetFacing(bool flipX)
    {
        CacheComponents();
        if (leaning && faceFront)
            standFlipX = flipX;
        else
            spriteRenderer.flipX = flipX;
    }

    private Vector3 LeanLocalScale => new Vector3(standLocalScale.x * leanScale, standLocalScale.y * leanScale, standLocalScale.z);

    private void ShowFrame()
    {
        // Scaled time, so the loop pauses with the game like an Animator would.
        if (!leaning) return;
        Sprite sprite = leanFrames[(int)((Time.time - leanStartTime) * framesPerSecond) % leanFrames.Length];
        if (sprite != spriteRenderer.sprite)
            spriteRenderer.sprite = sprite;
    }
}
