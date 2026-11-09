using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore;

public interface IInteractionTarget
{
    string InteractionLabel { get; }
    bool TryInteract();
}

internal static class InteractionLabelUtility
{
    // Yarn node titles end with the object name: Dorm_Bed -> Bed,
    // Day2_Dorm_KiranaBed -> Kirana Bed.
    public static string FromYarnTitle(string yarnTitle, string fallback)
    {
        string value = string.IsNullOrWhiteSpace(yarnTitle) ? fallback : yarnTitle.Trim();
        int separator = value.LastIndexOf('_');
        if (separator >= 0 && separator < value.Length - 1)
            value = value.Substring(separator + 1);
        value = value.Replace("(Clone)", "").Trim();
        if (value.Length == 0)
            return "Interact";

        StringBuilder readable = new StringBuilder(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current == '-' || current == '_')
            {
                if (readable.Length > 0 && readable[readable.Length - 1] != ' ')
                    readable.Append(' ');
                continue;
            }

            bool startsWord = index > 0 && char.IsUpper(current) &&
                              (char.IsLower(value[index - 1]) || char.IsDigit(value[index - 1]));
            if (startsWord && readable.Length > 0 && readable[readable.Length - 1] != ' ')
                readable.Append(' ');
            readable.Append(current);
        }
        return readable.ToString().Trim();
    }
}

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

    [Header("Object Label")]
    [Tooltip("9-sliced blue pill behind the object name. Found on this object when empty.")]
    [SerializeField] private SpriteRenderer labelBox;
    [Tooltip("World-space TextMeshPro child. Found automatically when empty.")]
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(0)] private int horizontalPadding = 8;
    [SerializeField, Min(0)] private int verticalPadding = 4;
    [SerializeField, Min(2)] private int minimumWidthPixels = 48;
    [Tooltip("Uniform world scale for the whole pill. Keep X and Y equal to avoid blurry text.")]
    [SerializeField, Range(0.1f, 1f)] private float visualScale = 0.32f;

    [Header("Sound")]
    [Tooltip("Plays when the player taps an interactable (ChoiceInteractable, SceneDoor). One clip for the whole game.")]
    [SerializeField] private AudioClip interactSound;
    [SerializeField, Range(0f, 1f)] private float interactVolume = 1f;

    private static InteractionCue instance;
    private AudioSource audioSource;
    // Interactables that want the cue now; the value says whether they show their own cue instead of this one.
    private static readonly Dictionary<Component, bool> candidates = new Dictionary<Component, bool>();
    private static readonly List<Component> stale = new List<Component>();
    private static Component target;
    private static bool hudPresenterActive;

    private Renderer[] renderers;
    private SpriteRenderer sprite;
    private Transform player;
    private bool visible = true;
    private Component labeledTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        candidates.Clear();
        target = null;
        hudPresenterActive = false;
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

    public static bool HasCurrentTarget => target != null;

    // Screen-space HUD presentation avoids low-resolution world-camera scaling.
    public static void SetHudPresenterActive(bool active) => hudPresenterActive = active;

    public static bool TryGetPresentation(out string objectLabel, out Bounds worldBounds)
    {
        objectLabel = string.Empty;
        worldBounds = default;
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;
        if (candidates.TryGetValue(target, out bool hasOwnCue) && hasOwnCue)
            return false;

        objectLabel = target is IInteractionTarget interaction
            ? interaction.InteractionLabel
            : InteractionLabelUtility.FromYarnTitle(null, target.name);
        worldBounds = BoundsOf(target);
        return !string.IsNullOrWhiteSpace(objectLabel);
    }

    public static bool TryInteractCurrent() => target is IInteractionTarget interactable && interactable.TryInteract();

    // World interactions accept taps anywhere, but UI buttons must own taps landing on UI.
    public static bool PointerOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
            return false;
        var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    // Plays the shared interact sound. Does nothing when the scene has no InteractionCue or no clip is assigned.
    public static void PlayInteractSound()
    {
        if (instance == null || instance.interactSound == null) return;
        instance.audioSource.PlayOneShot(instance.interactSound, instance.interactVolume);
    }

    private void Awake()
    {
        instance = this;
        // Non-uniform scaling makes world-space TMP and 9-sliced edges blurry.
        // Keep the compact cue, but scale both axes equally.
        transform.localScale = new Vector3(visualScale, visualScale, 1f);
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D: same volume wherever the camera is.
        }
        renderers = GetComponentsInChildren<Renderer>(true);
        if (labelBox == null) labelBox = GetComponent<SpriteRenderer>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        sprite = labelBox != null ? labelBox : GetComponent<SpriteRenderer>();
        if (labelBox != null && label != null)
            labelBox.drawMode = SpriteDrawMode.Sliced;
        if (label != null)
            label.isOrthographic = true;
        foreach (Renderer r in renderers)
        {
            r.sortingLayerName = sortingLayer;
            r.sortingOrder = sortingOrder;
        }
        if (label != null && label.TryGetComponent(out Renderer labelRenderer))
            labelRenderer.sortingOrder = sortingOrder + 1;
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
            labeledTarget = null;
            SetVisible(false);
            return;
        }

        if (hudPresenterActive)
        {
            SetVisible(false);
            return;
        }

        UpdateLabel(target);
        Bounds bounds = BoundsOf(target);
        // Sprite bounds are local to the pivot, so this is the distance from the cue's pivot down to its bottom edge.
        float pivotToBottom = sprite != null && sprite.sprite != null ? -sprite.sprite.bounds.min.y * transform.lossyScale.y : 0f;
        float x = Mathf.Round(bounds.center.x * pixelsPerUnit) / pixelsPerUnit;
        float y = Mathf.Round((bounds.max.y + gap + pivotToBottom) * pixelsPerUnit) / pixelsPerUnit;
        transform.position = new Vector3(x, y, transform.position.z);
        SetVisible(true);
    }

    private void UpdateLabel(Component current)
    {
        if (current == labeledTarget || label == null || labelBox == null)
            return;
        labeledTarget = current;

        string text = current is IInteractionTarget interaction
            ? interaction.InteractionLabel
            : InteractionLabelUtility.FromYarnTitle(null, current.name);
        label.text = text;
        label.horizontalAlignment = HorizontalAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.ForceMeshUpdate();

        float textWidth = Mathf.Round(label.GetPreferredValues(text, 1000f, 0f).x * pixelsPerUnit);
        FaceInfo face = label.font.faceInfo;
        float capHeight = Mathf.Round(
            face.capLine * face.scale * label.fontSize * 0.1f / face.pointSize * pixelsPerUnit);

        int width = Mathf.Max(minimumWidthPixels, Mathf.CeilToInt(textWidth) + horizontalPadding * 2);
        int height = Mathf.Max(2, Mathf.CeilToInt(capHeight) + verticalPadding * 2);
        width += width % 2;
        height += height % 2;

        Vector2 boxSize = new Vector2(width, height) / pixelsPerUnit;
        labelBox.size = boxSize;
        label.rectTransform.sizeDelta = boxSize;
        label.rectTransform.localPosition = new Vector3(0f, 0f, -0.01f);
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
