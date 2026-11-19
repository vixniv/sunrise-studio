using System.Collections;
using SunriseStudio.UI;
using UnityEngine;
using UnityEngine.UI;

// A centered picture over gameplay. The normal dialogue HUD stays in front of it.
// Put this on an active child of the room's HUD, behind BubbleRoot in sibling order.
public sealed class StorySplashOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image picture;
    [SerializeField] private DialogueHudView dialogueHud;

    [Header("Temporary character art")]
    [Tooltip("Assign cutscene_prologue_2a here for all four until portraits are ready.")]
    [SerializeField] private Sprite characterPlaceholder;
    [Tooltip("Optional individual portraits replace the shared placeholder.")]
    [SerializeField] private Sprite kirana;
    [SerializeField] private Sprite lila;
    [SerializeField] private Sprite dira;
    [SerializeField] private Sprite fal;

    [Header("Calendar art (optional)")]
    [SerializeField] private Sprite calendarNormal;
    [SerializeField] private Sprite calendarCircled;

    [SerializeField, Min(0f)] private float fadeSeconds = 0.3f;
    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (picture == null) picture = GetComponent<Image>();
        if (dialogueHud == null) dialogueHud = GetComponentInParent<DialogueHudView>(true);
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        if (picture != null) picture.raycastTarget = false;
    }

    private void OnDisable()
    {
        if (dialogueHud != null) dialogueHud.SetSplashPresentation(false);
    }

    public void Show(string id)
    {
        if (group == null || picture == null)
        {
            Debug.LogError("StorySplashOverlay needs a CanvasGroup and Image.", this);
            return;
        }

        Sprite sprite = Resolve(id);
        if (sprite == null)
            Debug.LogWarning($"Story splash '{id}' has no sprite yet; showing a black placeholder.", this);

        group.alpha = 0f;
        picture.sprite = sprite;
        picture.color = sprite == null ? Color.black : Color.white;
        picture.preserveAspect = true;
        if (dialogueHud != null) dialogueHud.SetSplashPresentation(true);
        RunFade(1f);
    }

    public void Hide()
    {
        if (group != null) RunFade(0f);
    }

    private void RunFade(float target)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float start = group.alpha;
        if (fadeSeconds > 0f)
        {
            for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, elapsed / fadeSeconds);
                yield return null;
            }
        }
        group.alpha = target;
        if (target <= 0f && dialogueHud != null) dialogueHud.SetSplashPresentation(false);
        fadeRoutine = null;
    }

    private Sprite Resolve(string id)
    {
        switch ((id ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "kirana": return kirana != null ? kirana : characterPlaceholder;
            case "lila": return lila != null ? lila : characterPlaceholder;
            case "dira": return dira != null ? dira : characterPlaceholder;
            case "fal": return fal != null ? fal : characterPlaceholder;
            case "calendar_normal": return calendarNormal;
            case "calendar_circled": return calendarCircled;
            default:
                Debug.LogError($"Unknown story splash id '{id}'.", this);
                return null;
        }
    }
}
