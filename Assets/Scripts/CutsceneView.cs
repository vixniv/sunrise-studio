using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Full-screen picture cutscene: a picture on top, typed text below. Yarn drives it:
//   <<cutscene_image "cutscene_prologue_1">>  opens the cutscene (fades in) or changes the picture
//   <<cutscene_image_keep_text "name">>        changes the picture, the text under it stays
//   any line while it is open                  types out under the picture
//   <<cutscene_end>>                           fades the cutscene out; the scene shows again
// Tap while the text types: the whole line shows. Tap again: the next line or picture.
// A picture with no line after it waits for one tap before the next picture or the end.
// Put it on a full-screen panel of a Screen Space canvas that sorts above the dialogue HUD.
// The panel needs an Image with Raycast Target on (the black background) so it receives the taps.
public class CutsceneView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private CanvasGroup group;
    [Tooltip("The Image that shows the cutscene picture (top of the screen).")]
    [SerializeField] private Image picture;
    [Tooltip("The text under the picture.")]
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Optional: shown when a tap goes on (text done, or a picture with no text).")]
    [SerializeField] private GameObject nextIndicator;
    [Tooltip("Every cutscene picture. <<cutscene_image \"name\">> finds them by sprite name.")]
    [SerializeField] private List<Sprite> pictures = new List<Sprite>();
    [SerializeField, Min(1f)] private float charactersPerSecond = 30f;
    [Tooltip("Seconds the whole cutscene takes to fade in and out.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;
    [Tooltip("Seconds the picture takes to dim to black and back when it changes. 0 swaps it at once.")]
    [SerializeField, Min(0f)] private float pictureChangeSeconds = 0.3f;

    private static CutsceneView active;

    private Coroutine routine;
    private bool open;
    private bool typing;
    private bool lineShowing;
    private bool lineDone = true;
    private bool lineSinceImage;
    private bool waitingForTap;
    private bool tapped;
    private float visibleCharacters;
    private int lastTypedCharacter;
    private int charactersSinceBlip;

    // The cutscene panel of the loaded scene, or null when the scene has none.
    public static CutsceneView Active
    {
        get
        {
            if (active == null) active = FindAnyObjectByType<CutsceneView>(FindObjectsInactive.Include);
            return active;
        }
    }

    public bool IsOpen => open;
    // True while a picture fades or waits for its tap; Yarn waits for it before going on.
    public bool IsBusy => routine != null;
    // True once the current line was read and tapped away.
    public bool LineFinished => lineDone;

    private void Awake()
    {
        active = this;
        if (group == null) group = GetComponent<CanvasGroup>();
        SetClosed();
    }

    private void OnDestroy()
    {
        if (active == this) active = null;
    }

    // <<cutscene_image>>: opens the cutscene on this picture, or changes to it when the cutscene is already open.
    // keepText: the text already under the picture stays instead of being cleared.
    public void ShowPicture(string spriteName, bool keepText = false)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        Sprite sprite = FindPicture(spriteName);
        if (sprite == null)
            Debug.LogWarning($"<<cutscene_image>>: no sprite named \"{spriteName}\" in {name}'s Pictures list.", this);
        Run(ShowPictureRoutine(sprite, keepText));
    }

    // <<cutscene_end>>: fades the cutscene out.
    public void Close()
    {
        if (!open) return;
        Run(CloseRoutine());
    }

    // Types the line under the picture. LineFinished turns true once the player taps it away.
    public void ShowLine(string text)
    {
        lineSinceImage = true;
        lineShowing = true;
        lineDone = false;
        SetIndicator(false);
        if (bodyText == null)
        {
            lineDone = true;
            return;
        }

        bodyText.text = text;
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = 0;
        visibleCharacters = 0f;
        lastTypedCharacter = 0;
        charactersSinceBlip = 0;
        typing = bodyText.textInfo.characterCount > 0;
        if (!typing) CompleteTyping();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!open) return;

        if (waitingForTap)
        {
            tapped = true;
            return;
        }
        if (typing)
        {
            CompleteTyping();
            return;
        }
        if (lineShowing && !lineDone)
        {
            SunriseAudio.Play("dialogue_advance");
            lineDone = true;
            lineShowing = false;
            SetIndicator(false);
        }
    }

    private void Update()
    {
        if (!typing || bodyText == null) return;

        visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;
        int count = bodyText.textInfo.characterCount;
        int nextVisible = Mathf.Min(Mathf.FloorToInt(visibleCharacters), count);
        bool blip = false;
        for (int i = lastTypedCharacter; i < nextVisible; i++)
        {
            if (!bodyText.textInfo.characterInfo[i].isVisible) continue;
            if (++charactersSinceBlip >= 2)
            {
                charactersSinceBlip = 0;
                blip = true;
            }
        }
        lastTypedCharacter = nextVisible;
        bodyText.maxVisibleCharacters = nextVisible;
        if (blip) SunriseAudio.Play("dialogue_blip");
        if (nextVisible >= count) CompleteTyping();
    }

    private void CompleteTyping()
    {
        typing = false;
        if (bodyText != null) bodyText.maxVisibleCharacters = int.MaxValue;
        SetIndicator(true);
    }

    private IEnumerator ShowPictureRoutine(Sprite sprite, bool keepText)
    {
        if (!open)
        {
            open = true;
            ClearText();
            SetPicture(sprite, Color.white);
            group.blocksRaycasts = true;
            group.interactable = true;
            yield return FadeGroup(0f, 1f);
        }
        else
        {
            // A picture without text stays until the player taps.
            if (!lineSinceImage) yield return WaitForTap();
            if (!keepText) ClearText();
            if (pictureChangeSeconds > 0f && picture != null)
            {
                yield return TintPicture(Color.white, Color.black, pictureChangeSeconds * 0.5f);
                SetPicture(sprite, Color.black);
                yield return TintPicture(Color.black, Color.white, pictureChangeSeconds * 0.5f);
            }
            else
            {
                SetPicture(sprite, Color.white);
            }
        }
        lineSinceImage = false;
        routine = null;
    }

    private IEnumerator CloseRoutine()
    {
        if (!lineSinceImage) yield return WaitForTap();
        SetIndicator(false);
        yield return FadeGroup(group.alpha, 0f);
        SetClosed();
        routine = null;
    }

    private IEnumerator WaitForTap()
    {
        tapped = false;
        waitingForTap = true;
        SetIndicator(true);
        while (!tapped) yield return null;
        waitingForTap = false;
        SetIndicator(false);
        SunriseAudio.Play("dialogue_advance");
    }

    private IEnumerator FadeGroup(float from, float to)
    {
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }
        group.alpha = to;
    }

    private IEnumerator TintPicture(Color from, Color to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            picture.color = Color.Lerp(from, to, t / seconds);
            yield return null;
        }
        picture.color = to;
    }

    private void Run(IEnumerator next)
    {
        if (routine != null) StopCoroutine(routine);
        waitingForTap = false;
        routine = StartCoroutine(next);
    }

    private void SetClosed()
    {
        open = false;
        typing = false;
        lineShowing = false;
        lineDone = true;
        lineSinceImage = false;
        waitingForTap = false;
        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        ClearText();
        SetIndicator(false);
    }

    private void ClearText()
    {
        typing = false;
        if (bodyText != null) bodyText.text = string.Empty;
        SetIndicator(false);
    }

    private void SetPicture(Sprite sprite, Color tint)
    {
        if (picture == null) return;
        picture.sprite = sprite;
        // No sprite (a typo in Yarn) shows black instead of a white box.
        picture.color = sprite != null ? tint : Color.black;
    }

    private void SetIndicator(bool visible)
    {
        if (nextIndicator != null && nextIndicator.activeSelf != visible) nextIndicator.SetActive(visible);
    }

    private Sprite FindPicture(string spriteName)
    {
        string wanted = spriteName.Trim();
        foreach (Sprite sprite in pictures)
        {
            if (sprite == null) continue;
            // Sprite Mode Multiple names the sprite "file_0"; Single names it "file". Both match "file".
            if (string.Equals(sprite.name, wanted, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sprite.name, wanted + "_0", System.StringComparison.OrdinalIgnoreCase))
                return sprite;
        }
        return null;
    }
}
