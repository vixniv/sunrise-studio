using System.Collections.Generic;
using UnityEngine;

// The one cue for the scene: put it on the Cue child of the Player prefab.
// Interactables (ChoiceInteractable, SceneDoor) ask for it while the player is inside their trigger. It shows above the
// nearest of them, and only that one takes taps. Objects without an interactable component never ask, so never get it.
// An interactable with its own cue object (not this one) is still picked the same way, but shows its own cue instead.
// Toggles its renderers rather than the GameObject so LateUpdate keeps running while hidden.
public class InteractionCue : MonoBehaviour
{
    [Tooltip("World gap between the top of the interactable's sprite and the bottom of the cue.")]
    [SerializeField] private float gap = 0.375f;
    [Tooltip("Draws the cue above room foreground art.")]
    [SerializeField] private string sortingLayer = "Foreground";
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private int pixelsPerUnit = 16;

    private static InteractionCue instance;
    // Interactables that want the cue now; the value says whether they show their own cue instead of this one.
    private static readonly Dictionary<Component, bool> candidates = new Dictionary<Component, bool>();
    private static readonly List<Component> stale = new List<Component>();
    private static Component target;

    private Renderer[] renderers;
    private SpriteRenderer sprite;
    private Transform player;
    private bool visible = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        candidates.Clear();
        target = null;
    }

    // True when cue is this shared cue, so the interactable must not toggle it itself.
    public static bool IsSharedCue(GameObject cue) => cue != null && cue.GetComponent<InteractionCue>() != null;

    // Call every frame: want is true while the player is in range and the interactable can be used.
    public static void Request(Component interactable, bool want, bool hasOwnCue)
    {
        if (want) candidates[interactable] = hasOwnCue;
        else candidates.Remove(interactable);
    }

    // Whether this interactable should show its cue and take taps. With no shared cue in the scene every one does.
    public static bool IsTarget(Component interactable) => instance == null || target == interactable;

    private void Awake()
    {
        instance = this;
        renderers = GetComponentsInChildren<Renderer>(true);
        sprite = GetComponent<SpriteRenderer>();
        foreach (Renderer r in renderers)
        {
            r.sortingLayerName = sortingLayer;
            r.sortingOrder = sortingOrder;
        }
        PlayerMove playerMove = GetComponentInParent<PlayerMove>();
        player = playerMove != null ? playerMove.transform : transform.parent;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        target = null;
    }

    private void LateUpdate()
    {
        target = PickNearest();
        if (target == null || candidates[target])
        {
            SetVisible(false);
            return;
        }

        Bounds bounds = BoundsOf(target);
        // Sprite bounds are local to the pivot, so this is the distance from the cue's pivot down to its bottom edge.
        float pivotToBottom = sprite != null && sprite.sprite != null ? -sprite.sprite.bounds.min.y * transform.lossyScale.y : 0f;
        float x = Mathf.Round(bounds.center.x * pixelsPerUnit) / pixelsPerUnit;
        float y = Mathf.Round((bounds.max.y + gap + pivotToBottom) * pixelsPerUnit) / pixelsPerUnit;
        transform.position = new Vector3(x, y, transform.position.z);
        SetVisible(true);
    }

    private Component PickNearest()
    {
        Component best = null;
        float bestDistance = float.MaxValue;
        stale.Clear();
        foreach (Component candidate in candidates.Keys)
        {
            // A destroyed interactable never cancels its request.
            if (candidate == null || !candidate.gameObject.activeInHierarchy) { stale.Add(candidate); continue; }
            float distance = player != null ? Mathf.Abs(BoundsOf(candidate).center.x - player.position.x) : 0f;
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        foreach (Component candidate in stale) candidates.Remove(candidate);
        return best;
    }

    // The interactable's sprite, or its "Visual" child's, else its collider. Invisible (alpha 0) sprites still count.
    private static Bounds BoundsOf(Component interactable)
    {
        if (interactable.TryGetComponent(out Renderer own)) return own.bounds;
        Transform visual = interactable.transform.Find("Visual");
        if (visual != null && visual.TryGetComponent(out Renderer child)) return child.bounds;
        if (interactable.TryGetComponent(out Collider2D col)) return col.bounds;
        return new Bounds(interactable.transform.position, Vector3.zero);
    }

    private void SetVisible(bool show)
    {
        if (visible == show) return;
        visible = show;
        foreach (Renderer r in renderers) r.enabled = show;
    }
}
