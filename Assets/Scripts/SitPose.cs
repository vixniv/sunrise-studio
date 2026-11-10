using UnityEngine;

// Plays a looping sitting animation on a character's "Visual" sprite, for Anin (Animator) and the NPCs (SpriteFrameLoop).
// Put it on the "Visual" child next to the SpriteRenderer.
// While sitting it pauses the Animator / SpriteFrameLoop on the same object and loops Sit Frames instead.
// Yarn: <<sit "Name">> and <<stand "Name">>. Tick Start Sitting for characters already seated when the scene opens.
// A <<move_character>> or the player dragging to walk makes the character stand up first.
[RequireComponent(typeof(SpriteRenderer))]
public class SitPose : MonoBehaviour
{
    [Tooltip("Sliced sit frames in play order, e.g. dira-sit_0 .. dira-sit_2.")]
    [SerializeField] private Sprite[] sitFrames;
    [Tooltip("Sit frames per second. Idle loops run at 5.")]
    [Min(0.01f)] [SerializeField] private float framesPerSecond = 5f;
    [Tooltip("Start each copy at a random point in the loop so characters sitting together don't breathe in sync.")]
    [SerializeField] private bool randomStart = true;
    [Tooltip("Sit as soon as this object is enabled, e.g. friends already at the lunch table.")]
    [SerializeField] private bool startSitting;

    [Header("Placement")]
    [Tooltip("Added to this Visual's local position while sitting, in units (1 pixel = 0.0625). " +
             "Use it to put the character's seat on the chair without moving the root.")]
    [SerializeField] private Vector2 sitOffset = Vector2.zero;
    [Tooltip("The sit art faces the camera: keep the sprite unflipped while sitting. " +
             "A <<face_character>> while seated is remembered for when the character stands up.")]
    [SerializeField] private bool faceFront = true;

    [Header("Sorting (optional)")]
    [Tooltip("Tick to draw the character on another sorting layer/order while sitting, e.g. behind a table that walkers pass in front of.")]
    [SerializeField] private bool overrideSorting;
    [SerializeField] private string sitSortingLayer = "Default";
    [SerializeField] private int sitSortingOrder = 5;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private SpriteFrameLoop frameLoop;
    private PlayerMove playerMove;

    private bool sitting;
    private float sitStartTime;
    private bool animatorWasEnabled;
    private bool frameLoopWasEnabled;
    private Vector3 standLocalPosition;
    private bool standFlipX;
    private int standSortingLayerId;
    private int standSortingOrder;

    public bool IsSitting => sitting;

    private void Awake()
    {
        CacheComponents();
    }

    // Yarn can call Sit/Stand on a Visual that has never been active, so Awake may not have run yet.
    private void CacheComponents()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        frameLoop = GetComponent<SpriteFrameLoop>();
        playerMove = GetComponentInParent<PlayerMove>(true);
    }

    private void OnEnable()
    {
        if (startSitting) Sit();
    }

    public void Sit()
    {
        if (sitting) return;
        CacheComponents();
        if (sitFrames == null || sitFrames.Length == 0)
        {
            Debug.LogError($"SitPose on \"{name}\" has no Sit Frames assigned.", this);
            return;
        }

        sitting = true;
        sitStartTime = Time.time - (randomStart ? Random.Range(0, sitFrames.Length) / framesPerSecond : 0f);

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
        transform.localPosition = standLocalPosition + (Vector3)sitOffset;

        standFlipX = spriteRenderer.flipX;
        if (faceFront) spriteRenderer.flipX = false;
        standSortingLayerId = spriteRenderer.sortingLayerID;
        standSortingOrder = spriteRenderer.sortingOrder;
        if (overrideSorting)
        {
            spriteRenderer.sortingLayerName = sitSortingLayer;
            spriteRenderer.sortingOrder = sitSortingOrder;
        }

        ShowFrame();
    }

    public void Stand()
    {
        if (!sitting) return;
        sitting = false;

        transform.localPosition = standLocalPosition;
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
        if (!sitting) return;

        // The player gets up as soon as they walk (drag, keyboard, or a scripted WalkTo).
        if (playerMove != null && playerMove.MoveX != 0f)
        {
            Stand();
            return;
        }

#if UNITY_EDITOR
        // Lets Sit Offset be tuned in the Inspector during Play mode and seen right away.
        transform.localPosition = standLocalPosition + (Vector3)sitOffset;
#endif
        ShowFrame();
    }

    // Called by <<face_character>> / <<teleport_character>> while seated. With Face Front the sit art stays unflipped
    // and the facing is used when the character stands up; otherwise the sit art flips right away.
    public void SetFlipX(bool flipX)
    {
        CacheComponents();
        if (sitting && faceFront)
            standFlipX = flipX;
        else
            spriteRenderer.flipX = flipX;
    }

    private void ShowFrame()
    {
        // Scaled time, so the loop pauses with the game like an Animator would.
        Sprite sprite = sitFrames[(int)((Time.time - sitStartTime) * framesPerSecond) % sitFrames.Length];
        if (sprite != spriteRenderer.sprite)
            spriteRenderer.sprite = sprite;
    }
}
