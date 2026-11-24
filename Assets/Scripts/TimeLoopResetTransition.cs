using System;
using System.Collections;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dramatic / Psychological-Horror 4-Quadrant Calendar Time Loop Reset transition:
/// 1. Sleepy eyelids slowly close (droop halfway -> resist/bounce open -> heavy final close) with URP scene blur.
/// 2. Innocent white DECEMBER calendar page ("10" in PatrickHand + full circle + grid lines, weekdays, and neighbor dates 9/11/16/17/18) holds calmly for 1s.
/// 3. Sudden smash-cut into the horror calendar aesthetic (full "10" + full circle + crimson/ash grid, trembling, not yet undoing) for 0.5s.
/// 4. 4-Quadrant undo sequence (with calendar grid lines & neighbor dates visible in macro close-up):
///    - Q1 Top-Right (2s, creeping zoom, 1.00 -> 0.75)
///    - Q2 Bottom-Right (2s, creeping zoom, 0.75 -> 0.50)
///    - Q3 Bottom-Left (2s, creeping zoom, 0.50 -> 0.25)
///    - Q4 Top-Left + Zoom Out (2s, zooms out to full calendar sheet, 0.25 -> 0.00)
/// 5. Sudden pitch-black silence -> groggy eyelids open (halfway -> sleepy blink down -> full open) as scene blur clears.
/// </summary>
public class TimeLoopResetTransition : MonoBehaviour
{
    public static TimeLoopResetTransition Instance { get; private set; }

    [Header("Eyelids & Backdrop")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform topEyelid;
    [SerializeField] RectTransform bottomEyelid;
    [SerializeField] Image fullBackdropImage;
    [SerializeField] Image halftoneImage;
    [SerializeField] Image vignetteGrainImage;
    [SerializeField] Image flashOverlayImage;

    [Header("Number 10 & Circle (Main + Horror Ghost Echo)")]
    [SerializeField] CanvasGroup dateRigGroup;
    [SerializeField] RectTransform dateRigRect;
    [SerializeField] TMP_Text dayNumberText;
    [SerializeField] TMP_Text dayGhostText;
    [SerializeField] Image dateCircleImage;
    [SerializeField] Image dateCircleGhostImage;

    [Header("Calendar Page Graphics (Header, Grid Lines, Weekdays & Neighbor Dates)")]
    [SerializeField] Image calendarHeaderBar;
    [SerializeField] TMP_Text monthHeaderText;
    [SerializeField] Image[] calendarGridLines;
    [SerializeField] TMP_Text[] weekdayTexts;
    [SerializeField] TMP_Text[] neighborDayTexts;

    [Header("Fonts (Calm vs Horror)")]
    [SerializeField] TMP_FontAsset calmFont;
    [SerializeField] TMP_FontAsset horrorFont;

    [Header("Calm Intro Colors")]
    [SerializeField] Color calmBackgroundColor = new Color32(0x9E, 0xA6, 0x9B, 0xFF);
    [SerializeField] Color calmPadPaperColor = new Color32(0xF5, 0xEF, 0xE2, 0xFF);
    [SerializeField] Color calmInkColor = new Color32(0x1D, 0x1F, 0x1E, 0xFF);
    [SerializeField] Color calmHeaderBarColor = new Color32(0xC8, 0x28, 0x32, 0xFF);
    [SerializeField] Color calmGridLineColor = new Color32(0x1D, 0x1F, 0x1E, 0xFF);
    [SerializeField] Color calmNeighborDayColor = new Color32(0x1D, 0x1F, 0x1E, 0xFF);

    [Header("Horror Palette")]
    [SerializeField] Color abyssInkColor = new Color32(0x05, 0x04, 0x08, 0xFF);
    [SerializeField] Color bonePaperColor = new Color32(0xEC, 0xE2, 0xD0, 0xFF);
    [SerializeField] Color bloodCrimsonColor = new Color32(0xD9, 0x18, 0x24, 0xFF);
    [SerializeField] Color deepRustColor = new Color32(0x4A, 0x07, 0x0E, 0xFF);

    [Header("Sleepy Eyelid & Blur Settings")]
    [SerializeField] bool playEyelidClose = true;
    [SerializeField] bool playEyelidOpen = true;
    [SerializeField] bool enableSceneBlur = true;
    [Range(0.5f, 1.5f)]
    [SerializeField] float maxBlurRadius = 1.5f;
    [Min(0.05f)]
    [SerializeField] float fadeToBlackDuration = 0.85f;
    [Min(0.05f)]
    [SerializeField] float calendarFadeInDuration = 0.45f;
    [Min(0.05f)]
    [SerializeField] float fadeFromBlackDuration = 0.85f;
    [Min(0.05f)]
    [SerializeField] float closeDroopDuration = 0.60f;
    [Min(0.05f)]
    [SerializeField] float closeResistBounceDuration = 0.34f;
    [Min(0.05f)]
    [SerializeField] float closeFinalShutDuration = 0.68f;
    [Min(0.05f)]
    [SerializeField] float openHalfwayDuration = 0.55f;
    [Min(0.05f)]
    [SerializeField] float openBlinkDownDuration = 0.30f;
    [Min(0.05f)]
    [SerializeField] float openFinalClearDuration = 0.75f;

    [Header("Intro & Quadrant Timing")]
    [Min(0f)]
    [SerializeField] float calmCalendarHoldDuration = 1.0f;
    [Min(0f)]
    [SerializeField] float horrorRevealHoldDuration = 0.5f;
    [Min(0.1f)]
    [SerializeField] float quadrantDuration = 2f;
    [SerializeField] float quadrantStartZoom = 2.35f;
    [SerializeField] float quadrantCreepZoom = 2.68f;
    [SerializeField] Vector2 quadrantFocusOffset = new Vector2(46f, 46f);
    [Min(0.1f)]
    [SerializeField] float quadrantIVZoomOutDuration = 1.15f;
    [Min(0f)]
    [SerializeField] float blackoutSilenceDuration = 0.35f;

    [Header("Horror Intensity")]
    [SerializeField] float shakeIntensity = 9f;
    [SerializeField] float ghostSplitPixels = 6.5f;

    [Header("FMOD Audio")]
    [Tooltip("Optional FMOD Studio Event Emitter on this object (auto-found if left empty). Plays when the calm calendar hold begins.")]
    [SerializeField] StudioEventEmitter resetEmitter;
    [SerializeField] EventReference quadrantCutEvent;
    [SerializeField] EventReference finalSnapEvent;
    [SerializeField] EventReference wakeUpEvent;

    Sprite runtimeRingSprite;
    Sprite runtimeVignetteSprite;
    Sprite runtimeTopLidSprite;
    Sprite runtimeBottomLidSprite;

    Volume runtimeBlurVolume;
    VolumeProfile runtimeBlurProfile;
    DepthOfField runtimeDepthOfField;

    bool transitioning;
    public bool IsTransitioning => transitioning;

    public void ResetForTitleScreen()
    {
        StopAllCoroutines();
        transitioning = false;
        ResetIdleState();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (resetEmitter == null)
            resetEmitter = GetComponent<StudioEventEmitter>();

        EnsureProceduralTextures();
        EnsureBlurVolume();
        ResetIdleState();
    }

    void OnDestroy()
    {
        DestroyRuntimeSprite(runtimeRingSprite);
        DestroyRuntimeSprite(runtimeVignetteSprite);
        DestroyRuntimeSprite(runtimeTopLidSprite);
        DestroyRuntimeSprite(runtimeBottomLidSprite);

        if (runtimeBlurProfile != null)
            Destroy(runtimeBlurProfile);
    }

    static void DestroyRuntimeSprite(Sprite sprite)
    {
        if (sprite == null) return;
        if (sprite.texture != null) Destroy(sprite.texture);
        Destroy(sprite);
    }

    public bool PlayLoopReset(
        string dayLabel = "10",
        Action whileCovered = null,
        Action onRewindStart = null,
        string sceneToLoad = null,
        bool holdBlackAtEnd = false)
    {
        if (transitioning || !HasRequiredReferences())
            return false;

        StartCoroutine(LoopResetRoutine(dayLabel, whileCovered, onRewindStart, sceneToLoad, holdBlackAtEnd));
        return true;
    }

    [ContextMenu("Test Time Loop Reset (Dec 10)")]
    void TestTimeLoopReset()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode first to test TimeLoopResetTransition.", this);
            return;
        }

        TimeLoopResetTransition target = Instance != null ? Instance : this;
        if (!target.gameObject.scene.IsValid())
        {
            Debug.LogWarning("No active TimeLoopResetCanvas found in the scene. Drag TimeLoopResetCanvas into your scene and press Play.", this);
            return;
        }

        target.PlayLoopReset("10");
    }

    IEnumerator LoopResetRoutine(
        string dayLabel,
        Action whileCovered,
        Action onRewindStart,
        string sceneToLoad,
        bool holdBlackAtEnd)
    {
        EnsureProceduralTextures();
        EnsureBlurVolume();

        transitioning = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        float closedHeight = GetCanvasHeight() * 0.68f;

        SetEyelidColor(abyssInkColor);
        SetEyelidHeight(0f);
        SetSceneBlur(0f);
        SetImageAlpha(fullBackdropImage, 0f);
        SetImageAlpha(flashOverlayImage, 0f);
        SetImageAlpha(halftoneImage, 0f);
        SetImageAlpha(vignetteGrainImage, 0f);

        if (dayNumberText != null) dayNumberText.text = dayLabel;
        if (dayGhostText != null) dayGhostText.text = dayLabel;

        if (dateRigGroup != null)
            dateRigGroup.alpha = 0f;

        // Skip 1 frame to avoid Inspector menu deltaTime spikes
        yield return null;

        // 1. SLEEPY EYELID CLOSE (or blur + fade to black)
        if (playEyelidClose)
        {
            yield return AnimateSleepyClose(closedHeight);

            // 2. CALM CALENDAR INTRO (1s): White calendar sheet, red DECEMBER header, grid lines, weekdays, neighbor dates, PatrickHand "10" + full circle
            fullBackdropImage.color = calmBackgroundColor;
            SetImageAlpha(fullBackdropImage, 1f);
            SetEyelidHeight(0f);

            ApplyCalmCalendarStyle();

            ApplyCameraFraming(Vector2.zero, 1f, 0f, Vector2.zero);
            if (dateRigGroup != null)
                dateRigGroup.alpha = 1f;
        }
        else
        {
            // Blur scene while fading full backdrop to pitch black
            yield return AnimateFadeAndBlur(fullBackdropImage, abyssInkColor, 0f, 1f, 0f, 1f, fadeToBlackDuration, EaseInOutSine);

            // 2. CALM CALENDAR INTRO: Prepare calendar at alpha 0, then fade both backdrop & calendar in from black
            SetEyelidHeight(0f);
            ApplyCalmCalendarStyle();
            ApplyCameraFraming(Vector2.zero, 1f, 0f, Vector2.zero);

            if (dateRigGroup != null)
                dateRigGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < calendarFadeInDuration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float t = EaseInOutSine(Mathf.Clamp01(elapsed / calendarFadeInDuration));
                fullBackdropImage.color = Color.Lerp(abyssInkColor, calmBackgroundColor, t);
                if (dateRigGroup != null)
                    dateRigGroup.alpha = t;
                yield return null;
            }

            fullBackdropImage.color = calmBackgroundColor;
            SetImageAlpha(fullBackdropImage, 1f);
            if (dateRigGroup != null)
                dateRigGroup.alpha = 1f;
        }

        if (resetEmitter != null)
            resetEmitter.Play();

        if (calmCalendarHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(calmCalendarHoldDuration);

        // 3. SUDDEN HORROR REVEAL (0.5s): Full calendar sheet snaps into horror style (circle still 1.0, not yet undoing)
        ApplyHorrorCalendarStyle(dreadLevel: 0.45f, inImpactFlash: false);
        if (vignetteGrainImage != null)
            vignetteGrainImage.color = new Color(1f, 1f, 1f, 0.85f);

        onRewindStart?.Invoke();
        PlayFmodEvent(quadrantCutEvent);

        if (horrorRevealHoldDuration > 0f)
        {
            yield return RunHorrorQuadrant(
                startFill: 1.00f,
                endFill: 1.00f,
                focus: Vector2.zero,
                startScale: 1.04f,
                endScale: 1.0f,
                dutchAngle: -1.5f,
                dreadLevel: 0.45f,
                zoomOutMode: false,
                duration: horrorRevealHoldDuration);
        }

        // 4. 4-Quadrant Undo Sequence (Top-Right -> Bottom-Right -> Bottom-Left -> Top-Left Zoom Out)
        Vector2 q1Focus = new Vector2(quadrantFocusOffset.x, quadrantFocusOffset.y);
        Vector2 q2Focus = new Vector2(quadrantFocusOffset.x, -quadrantFocusOffset.y);
        Vector2 q3Focus = new Vector2(-quadrantFocusOffset.x, -quadrantFocusOffset.y);

        // Quadrant I (Top-Right, 2s): 1.00 -> 0.75
        PlayFmodEvent(quadrantCutEvent);
        yield return RunHorrorQuadrant(
            startFill: 1.00f,
            endFill: 0.75f,
            focus: q1Focus,
            startScale: quadrantStartZoom,
            endScale: quadrantCreepZoom,
            dutchAngle: -2.8f,
            dreadLevel: 0.35f,
            zoomOutMode: false,
            duration: quadrantDuration);

        // Quadrant II (Bottom-Right, 2s): 0.75 -> 0.50
        PlayFmodEvent(quadrantCutEvent);
        yield return RunHorrorQuadrant(
            startFill: 0.75f,
            endFill: 0.50f,
            focus: q2Focus,
            startScale: quadrantStartZoom,
            endScale: quadrantCreepZoom,
            dutchAngle: 3.6f,
            dreadLevel: 0.58f,
            zoomOutMode: false,
            duration: quadrantDuration);

        // Quadrant III (Bottom-Left, 2s): 0.50 -> 0.25
        PlayFmodEvent(quadrantCutEvent);
        yield return RunHorrorQuadrant(
            startFill: 0.50f,
            endFill: 0.25f,
            focus: q3Focus,
            startScale: quadrantStartZoom,
            endScale: quadrantCreepZoom + 0.12f,
            dutchAngle: -4.5f,
            dreadLevel: 0.82f,
            zoomOutMode: false,
            duration: quadrantDuration);

        // Quadrant IV (Top-Left + Zoom Out, 2s): 0.25 -> 0.00
        PlayFmodEvent(quadrantCutEvent);
        yield return RunHorrorQuadrant(
            startFill: 0.25f,
            endFill: 0.00f,
            focus: Vector2.zero,
            startScale: quadrantCreepZoom,
            endScale: 1.0f,
            dutchAngle: 0f,
            dreadLevel: 1.0f,
            zoomOutMode: true,
            duration: quadrantDuration);

        // 5. Sudden pitch-black cut + dead silence the instant the circle vanishes!
        PlayFmodEvent(finalSnapEvent);
        whileCovered?.Invoke();

        if (dateRigGroup != null) dateRigGroup.alpha = 0f;
        if (halftoneImage != null) SetImageAlpha(halftoneImage, 0f);
        if (vignetteGrainImage != null) SetImageAlpha(vignetteGrainImage, 0f);
        fullBackdropImage.color = abyssInkColor;

        // The reset can replace the room while the full-screen backdrop hides it.
        // This was previously passed by callers but never used.
        if (!string.IsNullOrWhiteSpace(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);

        if (flashOverlayImage != null)
        {
            flashOverlayImage.color = new Color(bonePaperColor.r, bonePaperColor.g, bonePaperColor.b, 0.65f);
            yield return new WaitForSecondsRealtime(0.05f);
            SetImageAlpha(flashOverlayImage, 0f);
        }

        if (blackoutSilenceDuration > 0f)
            yield return new WaitForSecondsRealtime(blackoutSilenceDuration);

        if (holdBlackAtEnd)
        {
            SetSceneBlur(0f);
            SetImageAlpha(fullBackdropImage, 1f);
            SetEyelidHeight(0f);
            transitioning = false;
            yield break;
        }

        // 6. GROGGY WAKE-UP EYELID OPEN (or fade from black + clear blur)
        PlayFmodEvent(wakeUpEvent);
        if (playEyelidOpen)
        {
            SetSceneBlur(1f);
            SetImageAlpha(fullBackdropImage, 0f);
            SetEyelidColor(abyssInkColor);
            SetEyelidHeight(closedHeight);

            yield return AnimateSleepyOpen(closedHeight);
        }
        else
        {
            SetEyelidHeight(0f);
            yield return AnimateFadeAndBlur(fullBackdropImage, abyssInkColor, 1f, 0f, 1f, 0f, fadeFromBlackDuration, EaseOutCubic);
        }

        ResetIdleState();
        transitioning = false;
    }

    void ApplyCalmCalendarStyle()
    {
        if (dayNumberText != null)
        {
            if (calmFont != null) dayNumberText.font = calmFont;
            dayNumberText.color = calmInkColor;
        }
        if (dayGhostText != null)
            dayGhostText.gameObject.SetActive(false);

        if (dateCircleImage != null)
        {
            dateCircleImage.color = calmHeaderBarColor;
            dateCircleImage.fillAmount = 1f;
        }
        if (dateCircleGhostImage != null)
            dateCircleGhostImage.gameObject.SetActive(false);

        if (calendarHeaderBar != null)
            calendarHeaderBar.color = calmPadPaperColor;
        if (monthHeaderText != null)
            monthHeaderText.color = calmInkColor;

        if (calendarGridLines != null)
        {
            for (int i = 0; i < calendarGridLines.Length; i++)
            {
                if (calendarGridLines[i] == null) continue;
                string lineName = calendarGridLines[i].name;
                if (lineName == "WeekdayBar_0")
                    calendarGridLines[i].color = new Color32(0xF2, 0x5C, 0x66, 0xFF); // Sunday coral-red bar
                else if (lineName.StartsWith("WeekdayBar_"))
                    calendarGridLines[i].color = new Color32(0x78, 0x7B, 0x7D, 0xFF); // Mon-Sat grey bars
                else
                    calendarGridLines[i].color = new Color(calmInkColor.r, calmInkColor.g, calmInkColor.b, 0.55f);
            }
        }

        if (weekdayTexts != null)
        {
            for (int i = 0; i < weekdayTexts.Length; i++)
                if (weekdayTexts[i] != null)
                    weekdayTexts[i].color = new Color(calmInkColor.r, calmInkColor.g, calmInkColor.b, 0.65f);
        }

        if (neighborDayTexts != null)
        {
            Color holidayRed = new Color32(0xE0, 0x3E, 0x45, 0xFF);
            Color mutedGrey = new Color(calmInkColor.r, calmInkColor.g, calmInkColor.b, 0.45f);
            for (int i = 0; i < neighborDayTexts.Length; i++)
            {
                if (neighborDayTexts[i] == null) continue;
                if (calmFont != null) neighborDayTexts[i].font = calmFont;
                string t = neighborDayTexts[i].text;
                if (t == "11")
                    neighborDayTexts[i].color = calmInkColor;
                else if (t == "7" || t == "14" || t == "21" || t == "24" || t == "25" || t == "26" || t == "28")
                    neighborDayTexts[i].color = holidayRed;
                else
                    neighborDayTexts[i].color = mutedGrey;
            }
        }
    }

    void ApplyHorrorCalendarStyle(float dreadLevel, bool inImpactFlash)
    {
        if (dayNumberText != null && horrorFont != null && dayNumberText.font != horrorFont)
            dayNumberText.font = horrorFont;

        if (dayGhostText != null)
        {
            if (horrorFont != null && dayGhostText.font != horrorFont)
                dayGhostText.font = horrorFont;
            if (!dayGhostText.gameObject.activeSelf)
                dayGhostText.gameObject.SetActive(true);
            dayGhostText.color = new Color(bloodCrimsonColor.r, bloodCrimsonColor.g, bloodCrimsonColor.b, 0.55f);
        }

        if (dateCircleGhostImage != null && !dateCircleGhostImage.gameObject.activeSelf)
        {
            dateCircleGhostImage.gameObject.SetActive(true);
            dateCircleGhostImage.color = new Color(bloodCrimsonColor.r, bloodCrimsonColor.g, bloodCrimsonColor.b, 0.6f);
        }

        Color gridCol = inImpactFlash
            ? abyssInkColor
            : Color.Lerp(new Color(bonePaperColor.r, bonePaperColor.g, bonePaperColor.b, 0.75f),
                         new Color(bloodCrimsonColor.r, bloodCrimsonColor.g, bloodCrimsonColor.b, 0.95f),
                         dreadLevel);

        if (calendarGridLines != null)
        {
            for (int i = 0; i < calendarGridLines.Length; i++)
                if (calendarGridLines[i] != null)
                    calendarGridLines[i].color = gridCol;
        }

        if (calendarHeaderBar != null)
            calendarHeaderBar.color = inImpactFlash
                ? bonePaperColor
                : Color.Lerp(new Color32(0x0D, 0x09, 0x10, 0xFF), new Color32(0x1C, 0x06, 0x09, 0xFF), dreadLevel);

        if (monthHeaderText != null)
            monthHeaderText.color = gridCol;

        Color subTextCol = inImpactFlash
            ? new Color(abyssInkColor.r, abyssInkColor.g, abyssInkColor.b, 0.6f)
            : Color.Lerp(new Color(bonePaperColor.r, bonePaperColor.g, bonePaperColor.b, 0.28f),
                         new Color(bloodCrimsonColor.r, bloodCrimsonColor.g, bloodCrimsonColor.b, 0.48f),
                         dreadLevel);

        if (weekdayTexts != null)
        {
            for (int i = 0; i < weekdayTexts.Length; i++)
                if (weekdayTexts[i] != null)
                    weekdayTexts[i].color = subTextCol;
        }

        if (neighborDayTexts != null)
        {
            for (int i = 0; i < neighborDayTexts.Length; i++)
            {
                if (neighborDayTexts[i] != null)
                {
                    if (horrorFont != null && neighborDayTexts[i].font != horrorFont)
                        neighborDayTexts[i].font = horrorFont;
                    neighborDayTexts[i].color = subTextCol;
                }
            }
        }
    }

    IEnumerator AnimateSleepyClose(float closedHeight)
    {
        yield return AnimateEyelidAndBlur(0f, closedHeight * 0.62f, 0f, 0.72f, closeDroopDuration, EaseInOutSine);
        yield return AnimateEyelidAndBlur(closedHeight * 0.62f, closedHeight * 0.38f, 0.72f, 0.45f, closeResistBounceDuration, EaseOutSine);
        yield return AnimateEyelidAndBlur(closedHeight * 0.38f, closedHeight, 0.45f, 1f, closeFinalShutDuration, EaseInCubic);
    }

    IEnumerator AnimateSleepyOpen(float closedHeight)
    {
        yield return AnimateEyelidAndBlur(closedHeight, closedHeight * 0.44f, 1f, 0.65f, openHalfwayDuration, EaseOutSine);
        yield return AnimateEyelidAndBlur(closedHeight * 0.44f, closedHeight * 0.66f, 0.65f, 0.80f, openBlinkDownDuration, EaseInOutSine);
        yield return AnimateEyelidAndBlur(closedHeight * 0.66f, 0f, 0.80f, 0f, openFinalClearDuration, EaseOutCubic);
    }

    IEnumerator AnimateEyelidAndBlur(
        float startH,
        float endH,
        float startBlur,
        float endBlur,
        float duration,
        Func<float, float> ease)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = ease(Mathf.Clamp01(elapsed / duration));
            SetEyelidHeight(Mathf.Lerp(startH, endH, t));
            SetSceneBlur(Mathf.Lerp(startBlur, endBlur, t));
            yield return null;
        }
        SetEyelidHeight(endH);
        SetSceneBlur(endBlur);
    }

    IEnumerator AnimateFadeAndBlur(
        Image overlay,
        Color baseColor,
        float startAlpha,
        float endAlpha,
        float startBlur,
        float endBlur,
        float duration,
        Func<float, float> ease)
    {
        if (overlay != null)
        {
            Color c = baseColor;
            c.a = startAlpha;
            overlay.color = c;
        }
        SetSceneBlur(startBlur);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = ease(Mathf.Clamp01(elapsed / duration));
            if (overlay != null)
            {
                Color c = baseColor;
                c.a = Mathf.Lerp(startAlpha, endAlpha, t);
                overlay.color = c;
            }
            SetSceneBlur(Mathf.Lerp(startBlur, endBlur, t));
            yield return null;
        }

        if (overlay != null)
        {
            Color c = baseColor;
            c.a = endAlpha;
            overlay.color = c;
        }
        SetSceneBlur(endBlur);
    }

    IEnumerator RunHorrorQuadrant(
        float startFill,
        float endFill,
        Vector2 focus,
        float startScale,
        float endScale,
        float dutchAngle,
        float dreadLevel,
        bool zoomOutMode,
        float duration)
    {
        float elapsed = 0f;
        Vector2 zoomOutStartFocus = new Vector2(-quadrantFocusOffset.x * 0.6f, quadrantFocusOffset.y * 0.6f);
        bool isHoldingFullCircle = Mathf.Approximately(startFill, endFill);

        while (elapsed < duration)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            elapsed += dt;
            float rawT = Mathf.Clamp01(elapsed / duration);

            float fill = isHoldingFullCircle
                ? startFill
                : Mathf.Lerp(startFill, endFill, Mathf.Clamp01(rawT + Mathf.Sin(elapsed * 42f) * 0.012f * dreadLevel * (1f - rawT)));

            if (dateCircleImage != null)
                dateCircleImage.fillAmount = fill;
            if (dateCircleGhostImage != null)
                dateCircleGhostImage.fillAmount = isHoldingFullCircle ? fill : Mathf.Clamp01(fill + 0.025f * dreadLevel);

            bool inImpactFlash = elapsed < 0.075f;
            if (flashOverlayImage != null)
            {
                if (inImpactFlash)
                {
                    Color fc = dreadLevel > 0.7f ? bloodCrimsonColor : bonePaperColor;
                    fc.a = Mathf.Lerp(0.75f, 0f, elapsed / 0.075f);
                    flashOverlayImage.color = fc;
                }
                else
                {
                    SetImageAlpha(flashOverlayImage, 0f);
                }
            }

            ApplyHorrorCalendarStyle(dreadLevel, inImpactFlash);

            Color currentCircleColor = inImpactFlash
                ? abyssInkColor
                : Color.Lerp(bonePaperColor, bloodCrimsonColor, Mathf.Clamp01(dreadLevel * 0.85f + rawT * 0.25f));
            Color currentNumColor = inImpactFlash
                ? abyssInkColor
                : Color.Lerp(bonePaperColor, new Color32(0xFF, 0xD6, 0xD6, 0xFF), dreadLevel * 0.4f);

            if (dateCircleImage != null) dateCircleImage.color = currentCircleColor;
            if (dayNumberText != null) dayNumberText.color = currentNumColor;

            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * (7f + dreadLevel * 10f));
            if (fullBackdropImage != null)
                fullBackdropImage.color = Color.Lerp(abyssInkColor, deepRustColor, dreadLevel * 0.42f * pulse);

            if (halftoneImage != null)
            {
                Color hc = bloodCrimsonColor;
                hc.a = 0.05f + 0.14f * dreadLevel * pulse;
                halftoneImage.color = hc;
                halftoneImage.rectTransform.anchoredPosition = new Vector2(
                    Mathf.Sin(elapsed * 53f) * 3f * dreadLevel,
                    Mathf.Cos(elapsed * 47f) * 3f * dreadLevel);
            }

            Vector2 activeFocus = focus;
            float activeScale;
            float activeAngle;

            if (zoomOutMode)
            {
                float zoomT = EaseOutCubic(Mathf.Clamp01(elapsed / Mathf.Max(0.1f, quadrantIVZoomOutDuration)));
                activeFocus = Vector2.Lerp(zoomOutStartFocus, Vector2.zero, zoomT);
                activeScale = Mathf.Lerp(startScale, endScale, zoomT);
                activeAngle = Mathf.Lerp(4.2f, 0f, zoomT);
            }
            else
            {
                activeScale = Mathf.Lerp(startScale, endScale, rawT);
                activeAngle = dutchAngle;
            }

            float impactDecay = Mathf.Exp(-elapsed * 14f);
            float shakeMag = (shakeIntensity * impactDecay) + (2.4f * dreadLevel);
            Vector2 shakeOffset = new Vector2(
                Mathf.Sin(elapsed * 97f) * shakeMag,
                Mathf.Cos(elapsed * 113f) * shakeMag);

            ApplyCameraFraming(activeFocus, activeScale, activeAngle, shakeOffset);

            Vector2 ghostOffset = new Vector2(
                Mathf.Cos(elapsed * 67f) * ghostSplitPixels * (0.4f + dreadLevel + impactDecay),
                Mathf.Sin(elapsed * 83f) * ghostSplitPixels * 0.6f * (0.4f + dreadLevel));

            if (dayGhostText != null)
                dayGhostText.rectTransform.anchoredPosition = ghostOffset;
            if (dateCircleGhostImage != null)
                dateCircleGhostImage.rectTransform.anchoredPosition = -ghostOffset * 0.85f;

            yield return null;
        }

        if (dateCircleImage != null) dateCircleImage.fillAmount = endFill;
        if (dateCircleGhostImage != null) dateCircleGhostImage.fillAmount = endFill;
        if (flashOverlayImage != null) SetImageAlpha(flashOverlayImage, 0f);
    }

    void ApplyCameraFraming(Vector2 focusPointInRig, float scale, float angleDeg, Vector2 shakeOffset)
    {
        if (dateRigRect == null)
            return;

        dateRigRect.localScale = Vector3.one * scale;
        dateRigRect.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        dateRigRect.anchoredPosition = (-focusPointInRig * scale) + shakeOffset;
    }

    void EnsureBlurVolume()
    {
        if (!enableSceneBlur || runtimeBlurVolume != null)
            return;

        GameObject volGo = new GameObject("EyelidBlurVolume");
        volGo.transform.SetParent(transform, false);

        runtimeBlurVolume = volGo.AddComponent<Volume>();
        runtimeBlurVolume.isGlobal = true;
        runtimeBlurVolume.priority = 999f;
        runtimeBlurVolume.weight = 0f;

        runtimeBlurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeDepthOfField = runtimeBlurProfile.Add<DepthOfField>(true);
        runtimeDepthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        runtimeDepthOfField.gaussianStart.Override(0f);
        runtimeDepthOfField.gaussianEnd.Override(0.01f);
        runtimeDepthOfField.gaussianMaxRadius.Override(maxBlurRadius);
        runtimeDepthOfField.highQualitySampling.Override(true);

        runtimeBlurVolume.sharedProfile = runtimeBlurProfile;
    }

    void SetSceneBlur(float weight)
    {
        if (!enableSceneBlur || runtimeBlurVolume == null)
            return;

        float clamped = Mathf.Clamp01(weight);
        runtimeBlurVolume.weight = clamped;
        if (runtimeDepthOfField != null)
            runtimeDepthOfField.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, maxBlurRadius, clamped));
    }

    void SetEyelidHeight(float height)
    {
        if (topEyelid != null)
            topEyelid.sizeDelta = new Vector2(topEyelid.sizeDelta.x, height);
        if (bottomEyelid != null)
            bottomEyelid.sizeDelta = new Vector2(bottomEyelid.sizeDelta.x, height);
    }

    void SetEyelidColor(Color color)
    {
        if (topEyelid != null)
        {
            Image img = topEyelid.GetComponent<Image>();
            if (img != null) img.color = color;
        }
        if (bottomEyelid != null)
        {
            Image img = bottomEyelid.GetComponent<Image>();
            if (img != null) img.color = color;
        }
    }

    void EnsureProceduralTextures()
    {
        if (runtimeTopLidSprite == null)
            runtimeTopLidSprite = CreateFeatheredLidSprite(featherBottom: true);
        if (runtimeBottomLidSprite == null)
            runtimeBottomLidSprite = CreateFeatheredLidSprite(featherBottom: false);

        if (topEyelid != null)
        {
            Image topImg = topEyelid.GetComponent<Image>();
            if (topImg != null) topImg.sprite = runtimeTopLidSprite;
        }
        if (bottomEyelid != null)
        {
            Image botImg = bottomEyelid.GetComponent<Image>();
            if (botImg != null) botImg.sprite = runtimeBottomLidSprite;
        }

        if (runtimeRingSprite == null)
        {
            const int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            float center = (size - 1) * 0.5f;
            float baseRadius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float r1 = baseRadius + Mathf.Sin(angle * 3f + 0.4f) * 3.5f + Mathf.Cos(angle * 7f) * 2.0f;
                    float r2 = baseRadius - 3.2f + Mathf.Cos(angle * 5f - 1.1f) * 4.2f + Mathf.Sin(angle * 11f) * 1.8f;
                    float r3 = baseRadius + 2.8f + Mathf.Sin(angle * 4f + 2.3f) * 3.0f;

                    float scratchNoise = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                    float fineGrit = Mathf.PerlinNoise(x * 0.28f + 17f, y * 0.28f + 31f);

                    float s1 = Mathf.Clamp01(1f - (Mathf.Abs(dist - r1) - (3.8f * scratchNoise)) / 1.6f);
                    float s2 = Mathf.Clamp01(1f - (Mathf.Abs(dist - r2) - (2.6f * fineGrit)) / 1.5f) * 0.85f;
                    float s3 = Mathf.Clamp01(1f - (Mathf.Abs(dist - r3) - 1.8f) / 1.4f) * (fineGrit > 0.42f ? 0.7f : 0f);

                    float combined = Mathf.Clamp01(s1 + s2 + s3);
                    if (fineGrit < 0.22f)
                        combined *= 0.35f;

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(combined * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            runtimeRingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        ConfigureRingImage(dateCircleImage);
        ConfigureRingImage(dateCircleGhostImage);

        if (runtimeVignetteSprite == null && vignetteGrainImage != null)
        {
            const int vSize = 256;
            Texture2D vTex = new Texture2D(vSize, vSize, TextureFormat.RGBA32, false);
            vTex.filterMode = FilterMode.Bilinear;
            vTex.wrapMode = TextureWrapMode.Clamp;
            Color32[] vPixels = new Color32[vSize * vSize];

            for (int y = 0; y < vSize; y++)
            {
                for (int x = 0; x < vSize; x++)
                {
                    float nx = (x / (float)(vSize - 1)) * 2f - 1f;
                    float ny = (y / (float)(vSize - 1)) * 2f - 1f;
                    float radial = Mathf.Sqrt(nx * nx + ny * ny);
                    float vig = Mathf.SmoothStep(0.35f, 1.25f, radial);
                    float grain = (Mathf.PerlinNoise(x * 0.45f, y * 0.45f) - 0.5f) * 0.16f;
                    float a = Mathf.Clamp01(vig * 0.92f + Mathf.Max(0f, grain));
                    vPixels[y * vSize + x] = new Color32(3, 2, 5, (byte)(a * 255f));
                }
            }

            vTex.SetPixels32(vPixels);
            vTex.Apply();
            runtimeVignetteSprite = Sprite.Create(vTex, new Rect(0, 0, vSize, vSize), new Vector2(0.5f, 0.5f), 100f);
            vignetteGrainImage.sprite = runtimeVignetteSprite;
        }
    }

    static Sprite CreateFeatheredLidSprite(bool featherBottom)
    {
        const int h = 64;
        Texture2D tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[h];

        const float featherZone = 0.28f;
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float alpha = featherBottom
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / featherZone))
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / featherZone));
            pixels[y] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
    }

    void ConfigureRingImage(Image img)
    {
        if (img == null || runtimeRingSprite == null)
            return;

        img.sprite = runtimeRingSprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Top;
        img.fillClockwise = false;
    }

    static void PlayFmodEvent(EventReference eventRef)
    {
        if (!eventRef.IsNull)
        {
            RuntimeManager.PlayOneShot(eventRef);
            return;
        }

        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }
        audioSource.PlayOneShot(clip, sfxVolume * SunriseAudio.SfxVolume);
    }

    static void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    static float EaseInCubic(float x) => x * x * x;
    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseOutSine(float x) => Mathf.Sin(x * Mathf.PI * 0.5f);
    static float EaseInOutSine(float x) => -(Mathf.Cos(Mathf.PI * x) - 1f) * 0.5f;

    float GetCanvasHeight()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            RectTransform rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.rect.height > 0f)
                return rt.rect.height;
        }
        return 270f;
    }

    void ResetIdleState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        SetEyelidColor(abyssInkColor);
        SetEyelidHeight(0f);
        SetSceneBlur(0f);
        SetImageAlpha(fullBackdropImage, 0f);
        SetImageAlpha(flashOverlayImage, 0f);
        SetImageAlpha(halftoneImage, 0f);
        SetImageAlpha(vignetteGrainImage, 0f);

        if (dateRigGroup != null)
            dateRigGroup.alpha = 0f;
        if (dateRigRect != null)
        {
            dateRigRect.localScale = Vector3.one;
            dateRigRect.localRotation = Quaternion.identity;
            dateRigRect.anchoredPosition = Vector2.zero;
        }
    }

    bool HasRequiredReferences()
    {
        if (canvasGroup != null && topEyelid != null && bottomEyelid != null &&
            fullBackdropImage != null && dateRigGroup != null && dateRigRect != null)
            return true;

        Debug.LogError("TimeLoopResetTransition is missing required UI references in the Inspector.", this);
        return false;
    }
}
