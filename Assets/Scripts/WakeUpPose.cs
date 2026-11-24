using System.Collections;
using UnityEngine;

// Plays Anin's wake-up animation (lying in bed, then sitting up) on her "Visual" sprite.
// Put it on the "Visual" child next to the SpriteRenderer, the same object as SitPose.
// A YarnCutscene with this assigned in "Wake Up" calls LieDown() as the scene opens (still behind the
// transition), then PlayWakeUp() once the transition has opened, and only then starts its Yarn node.
// After the last frame a circle transition closes and reopens with Anin standing in her idle animation.
// While posed it pauses the Animator on the same object, like SitPose.
[RequireComponent(typeof(SpriteRenderer))]
public class WakeUpPose : MonoBehaviour
{
    [Tooltip("Sliced wake-up frames in play order: anin-wakeup_0 .. anin-wakeup_6. The first frame shows while the scene fades in.")]
    [SerializeField] private Sprite[] wakeFrames;
    [Tooltip("Wake-up frames per second.")]
    [Min(0.01f)] [SerializeField] private float framesPerSecond = 6f;
    [Tooltip("Seconds the first frame (asleep) stays on screen after the transition opens, before Anin starts waking up.")]
    [Min(0f)] [SerializeField] private float asleepSeconds = 0.6f;
    [Tooltip("Seconds the last frame stays before the circle transition closes.")]
    [Min(0f)] [SerializeField] private float afterSeconds = 0.3f;
    [Tooltip("Seconds the circle transition stays closed while Anin goes back to her standing idle.")]
    [Min(0f)] [SerializeField] private float coverHoldSeconds = 0.5f;

    [Header("Placement")]
    [Tooltip("Added to this Visual's local position while posed, in units (1 pixel = 0.0625). " +
             "Use it to put the bed art on the dorm bed without moving the player root.")]
    [SerializeField] private Vector2 poseOffset = Vector2.zero;
    [Tooltip("Mirror the wake-up art.")]
    [SerializeField] private bool flipArt;

    [Header("Sorting (optional)")]
    [Tooltip("Tick to draw Anin on another sorting layer/order while in bed, e.g. above the bed art.")]
    [SerializeField] private bool overrideSorting;
    [SerializeField] private string poseSortingLayer = "Default";
    [SerializeField] private int poseSortingOrder = 5;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PlayerMove playerMove;
    private SitPose sitPose;

    private bool posed;
    private bool waking;
    private Sprite poseSprite;
    private bool animatorWasEnabled;
    private Vector3 standLocalPosition;
    private bool standFlipX;
    private int standSortingLayerId;
    private int standSortingOrder;

    public bool IsPosed => posed;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        sitPose = GetComponent<SitPose>();
        playerMove = GetComponentInParent<PlayerMove>(true);
    }

    // Shows the first frame (asleep in bed). Call it while the screen is still covered.
    public void LieDown()
    {
        CacheComponents();
        if (wakeFrames == null || wakeFrames.Length == 0)
        {
            Debug.LogError($"WakeUpPose on \"{name}\" has no Wake Frames assigned.", this);
            return;
        }
        if (sitPose != null && sitPose.IsSitting) sitPose.Stand();

        if (!posed)
        {
            posed = true;
            if (animator != null)
            {
                animatorWasEnabled = animator.enabled;
                animator.enabled = false;
            }

            standLocalPosition = transform.localPosition;
            standFlipX = spriteRenderer.flipX;
            standSortingLayerId = spriteRenderer.sortingLayerID;
            standSortingOrder = spriteRenderer.sortingOrder;
        }

        transform.localPosition = standLocalPosition + (Vector3)poseOffset;
        spriteRenderer.flipX = flipArt;
        if (overrideSorting)
        {
            spriteRenderer.sortingLayerName = poseSortingLayer;
            spriteRenderer.sortingOrder = poseSortingOrder;
        }
        ShowFrame(wakeFrames[0]);
    }

    private void ShowFrame(Sprite frame)
    {
        poseSprite = frame;
        spriteRenderer.sprite = frame;
    }

    // Plays asleep hold, an optional sound, the wake-up frames, and the after hold.
    public IEnumerator PlayWakeUp(float asleepHoldOverrideSeconds = -1f, string wakeSound = null)
    {
        if (!posed) LieDown();
        if (!posed) yield break;

        waking = true;
        // Scaled time, so the animation pauses with the game like an Animator would.
        float asleepHold = asleepHoldOverrideSeconds >= 0f ? asleepHoldOverrideSeconds : asleepSeconds;
        if (asleepHold > 0f) yield return new WaitForSeconds(asleepHold);

        if (!string.IsNullOrWhiteSpace(wakeSound))
        {
            AudioClip clip = SunriseAudio.Play(wakeSound);
            if (clip != null)
                yield return new WaitForSecondsRealtime(clip.length);
            else
                Debug.LogWarning($"WakeUpPose on \"{name}\" could not play sound \"{wakeSound}\".", this);
        }

        float frameTime = 1f / framesPerSecond;
        for (int i = 1; i < wakeFrames.Length && posed; i++)
        {
            ShowFrame(wakeFrames[i]);
            yield return new WaitForSeconds(frameTime);
        }

        if (afterSeconds > 0f && posed) yield return new WaitForSeconds(afterSeconds);
        waking = false;
        if (!posed) yield break;

        // The black cover hides the change from the bed pose to standing idle.
        CircleTransition transition = CircleTransition.Instance;
        if (transition == null || !transition.CoverBlack(GetUp, coverHoldSeconds))
        {
            GetUp();
            yield break;
        }
        while (transition != null && transition.IsTransitioning)
            yield return null;
    }

    // Back to the normal standing Animator at the player's position.
    public void GetUp()
    {
        if (!posed) return;
        posed = false;
        waking = false;

        transform.localPosition = standLocalPosition;
        spriteRenderer.flipX = standFlipX;
        if (overrideSorting)
        {
            spriteRenderer.sortingLayerID = standSortingLayerId;
            spriteRenderer.sortingOrder = standSortingOrder;
        }
        if (animator != null && animatorWasEnabled) animator.enabled = true;
    }

    private void Update()
    {
        if (!posed || waking) return;

        // Anin gets out of bed as soon as she walks (drag, keyboard, or a scripted WalkTo).
        if (playerMove != null && playerMove.MoveX != 0f)
        {
            GetUp();
            playerMove.Face(playerMove.MoveX);
        }
#if UNITY_EDITOR
        else
        {
            // Lets Pose Offset be tuned in the Inspector during Play mode and seen right away.
            transform.localPosition = standLocalPosition + (Vector3)poseOffset;
        }
#endif
    }

    // Something else can write the sprite while Anin is in bed: CharacterOutfit swapping the Animator
    // controller rebinds the disabled Animator, which puts the SpriteRenderer's default sprite back.
    // Whether that runs before or after LieDown depends on Start order, which differs between the Editor and a build.
    // LateUpdate runs after all of that, so the bed frame always wins.
    private void LateUpdate()
    {
        if (!posed || poseSprite == null) return;
        if (spriteRenderer.sprite != poseSprite) spriteRenderer.sprite = poseSprite;
    }

    private void OnDisable()
    {
        GetUp();
    }
}
