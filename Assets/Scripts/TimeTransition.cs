using System;
using System.Collections;
using SunriseStudio.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles cohesive, paper-styled full-screen transitions using a StatusCard clone:
/// 1. Moving between Time Cycles (e.g. Morning -> Afternoon) with a staggered slide/fade,
///    pre-swap hold, bouncy press-and-pop update with a gradual backdrop color shift, and staggered exit.
/// 2. Time Loop Resets via a crisp ink cover and stamp settle.
/// Runs on unscaled time so transitions still play when gameplay is paused.
/// </summary>
public class TimeTransition : MonoBehaviour
{
    public static TimeTransition Instance { get; private set; }

    [Header("References")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform backdropRect;
    [SerializeField] Image backdropImage;
    [SerializeField] CanvasGroup contentGroup;
    [SerializeField] RectTransform contentRect;
    [SerializeField] Image statusCardImage;
    [SerializeField] TMP_Text loopNumberLabel;
    [SerializeField] TMP_Text timeTextLabel;
    [SerializeField] SunriseUITheme theme;

    [Header("StatusCard Sprites")]
    [SerializeField] Sprite morningSprite;
    [SerializeField] Sprite afternoonSprite;
    [SerializeField] Sprite eveningSprite;

    [Header("Backdrop Phase Colors")]
    [SerializeField] Color morningBackdropColor = new Color32(0xFF, 0xB3, 0x47, 0xFF);
    [SerializeField] Color afternoonBackdropColor = new Color32(0x6F, 0xB1, 0xFF, 0xFF);
    [SerializeField] Color eveningBackdropColor = new Color32(0xB2, 0x8D, 0xFF, 0xFF);

    [Header("Cycle Transition Timing")]
    [Min(0.05f)]
    [SerializeField] float cycleSlideInDuration = 0.45f;
    [Min(0.05f)]
    [SerializeField] float contentInDuration = 0.25f;
    [Min(0f)]
    [SerializeField] float cyclePreSwapHold = 1f;
    [Min(0.05f)]
    [SerializeField] float pressDownDuration = 0.1f;
    [Min(0.05f)]
    [SerializeField] float popUpDuration = 0.14f;
    [Min(0.05f)]
    [SerializeField] float settleDuration = 0.12f;
    [SerializeField] float pressMinScale = 0.88f;
    [SerializeField] float popMaxScale = 1.1f;
    [Min(0.05f)]
    [SerializeField] float backdropColorLerpDuration = 1.5f;
    [Min(0.05f)]
    [SerializeField] float contentOutDuration = 0.2f;
    [Min(0.05f)]
    [SerializeField] float cycleSlideOutDuration = 0.45f;
    [SerializeField] float contentSlideOffset = 28f;

    [Header("Loop Reset Timing")]
    [Min(0.05f)]
    [SerializeField] float resetFadeInDuration = 0.25f;
    [Min(0f)]
    [SerializeField] float resetHoldDuration = 0.9f;
    [Min(0.05f)]
    [SerializeField] float resetFadeOutDuration = 0.4f;

    static readonly Color FallbackInk = new Color32(0x07, 0x0D, 0x2B, 0xFF);

    Vector2 contentBasePos;
    bool cachedBasePosition;
    bool transitioning;

    public bool IsTransitioning => transitioning;

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

        CacheBasePosition();
        ResetIdleState();
    }

    public bool PlayCycleTransition(TimePhase targetPhase, Action whileCovered = null)
    {
        return PlayCycleTransition(GetPreviousPhase(targetPhase), targetPhase, -1, whileCovered);
    }

    public bool PlayCycleTransition(TimePhase targetPhase, int loopNumber, Action whileCovered = null)
    {
        return PlayCycleTransition(GetPreviousPhase(targetPhase), targetPhase, loopNumber, whileCovered);
    }

    /// <summary>
    /// Slides in the backdrop colored for fromPhase, then StatusCard showing fromPhase, holds,
    /// plays a bouncy press animation while swapping StatusCard to toPhase, running whileCovered,
    /// and gradually blending the backdrop color to toPhase over backdropColorLerpDuration,
    /// then slides StatusCard and backdrop out to the left.
    /// </summary>
    public bool PlayCycleTransition(
        TimePhase fromPhase,
        TimePhase toPhase,
        int loopNumber = -1,
        Action whileCovered = null)
    {
        if (transitioning || !HasRequiredReferences())
            return false;

        StartCoroutine(CycleTransitionRoutine(fromPhase, toPhase, loopNumber, whileCovered));
        return true;
    }

    public bool PlayLoopResetTransition(Action whileCovered = null, string timeLabel = "Morning")
    {
        return PlayLoopResetTransition(-1, whileCovered, timeLabel);
    }

    /// <summary>
    /// Snaps in a deep-ink cover with a StatusCard stamp settle, runs whileCovered once covered,
    /// then fades back out into the reset loop.
    /// </summary>
    public bool PlayLoopResetTransition(int newLoopNumber, Action whileCovered = null, string timeLabel = "Morning")
    {
        if (transitioning || !HasRequiredReferences())
            return false;

        StartCoroutine(LoopResetRoutine(newLoopNumber, whileCovered, timeLabel));
        return true;
    }

    [ContextMenu("Test Cycle -> Afternoon")]
    void TestCycleTransition()
    {
        if (!Application.isPlaying)
            return;

        PlayCycleTransition(TimePhase.Morning, TimePhase.Afternoon, 1);
    }

    [ContextMenu("Test Cycle -> Evening")]
    void TestCycleEveningTransition()
    {
        if (!Application.isPlaying)
            return;

        PlayCycleTransition(TimePhase.Afternoon, TimePhase.Evening, 1);
    }

    [ContextMenu("Test Loop Reset")]
    void TestLoopResetTransition()
    {
        if (!Application.isPlaying)
            return;

        PlayLoopResetTransition(2, null, "Morning");
    }

    IEnumerator CycleTransitionRoutine(
        TimePhase fromPhase,
        TimePhase toPhase,
        int loopNumber,
        Action whileCovered)
    {
        CacheBasePosition();
        transitioning = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        Color fromColor = GetPhaseBackdropColor(fromPhase);
        Color toColor = GetPhaseBackdropColor(toPhase);

        // Start with the previous phase on the StatusCard and backdrop
        ApplyPhaseStatus(fromPhase, loopNumber);
        SetContentAlpha(0f);
        SetContentOffsetX(contentSlideOffset);
        SetContentScale(1f);

        float slideDistance = GetCanvasWidth();
        backdropRect.anchoredPosition = new Vector2(slideDistance, 0f);
        Color startBackdrop = fromColor;
        startBackdrop.a = 0f;
        backdropImage.color = startBackdrop;

        // Skip one frame so any Inspector/menu focus deltaTime spike doesn't skip the start of the slide.
        yield return null;

        // 1. Backdrop slides & fades in from right -> center
        yield return AnimateBackdrop(fromColor, slideDistance, 0f, true, cycleSlideInDuration);

        // 2. StatusCard (fromPhase) slides & fades in from right -> center
        yield return AnimateContent(contentSlideOffset, 0f, 0f, 1f, contentInDuration);

        // 3. Hold on fromPhase
        if (cyclePreSwapHold > 0f)
            yield return new WaitForSecondsRealtime(cyclePreSwapHold);

        // 4. Bouncy press-in -> swap to toPhase + start gradual backdrop color shift -> pop & settle
        yield return AnimateScale(1f, pressMinScale, pressDownDuration);

        ApplyPhaseStatus(toPhase, loopNumber);
        whileCovered?.Invoke();
        Coroutine colorShift = StartCoroutine(AnimateBackdropColor(fromColor, toColor, backdropColorLerpDuration));

        yield return AnimateScale(pressMinScale, popMaxScale, popUpDuration);
        yield return AnimateScale(popMaxScale, 1f, settleDuration);

        // 5. Wait for the gradual backdrop color shift to finish
        yield return colorShift;

        // 6. StatusCard slides & fades out from center -> left
        yield return AnimateContent(0f, -contentSlideOffset, 1f, 0f, contentOutDuration);

        // 7. Backdrop slides & fades out from center -> left
        yield return AnimateBackdrop(toColor, 0f, -slideDistance, false, cycleSlideOutDuration);

        ResetIdleState();
        transitioning = false;
    }

    IEnumerator LoopResetRoutine(int newLoopNumber, Action whileCovered, string timeLabel)
    {
        CacheBasePosition();
        transitioning = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 0f;

        Color inkColor = theme != null ? theme.Ink : FallbackInk;

        backdropImage.color = inkColor;
        backdropRect.anchoredPosition = Vector2.zero;

        ApplyStatusVisuals(GetPhaseSprite(TimePhase.Morning), string.IsNullOrEmpty(timeLabel) ? "Morning" : timeLabel, newLoopNumber);
        SetContentOffsetX(0f);
        SetContentAlpha(1f);
        SetContentScale(1.12f);

        yield return null;

        yield return AnimateFadeAndStamp(0f, 1f, 1.12f, 1f, resetFadeInDuration);

        whileCovered?.Invoke();

        if (resetHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(resetHoldDuration);

        yield return AnimateFadeAndStamp(1f, 0f, 1f, 0.96f, resetFadeOutDuration);

        ResetIdleState();
        transitioning = false;
    }

    IEnumerator AnimateBackdrop(
        Color baseColor,
        float startX,
        float endX,
        bool fadingIn,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float rawT = Mathf.Clamp01(elapsed / duration);
            float moveT = Mathf.SmoothStep(0f, 1f, rawT);

            float alpha = fadingIn
                ? Mathf.Clamp01(rawT / 0.4f)
                : 1f - Mathf.Clamp01((rawT - 0.5f) / 0.5f);

            backdropRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, moveT), 0f);
            Color c = baseColor;
            c.a = alpha;
            backdropImage.color = c;

            yield return null;
        }

        backdropRect.anchoredPosition = new Vector2(endX, 0f);
        Color finalColor = baseColor;
        finalColor.a = fadingIn ? 1f : 0f;
        backdropImage.color = finalColor;
    }

    IEnumerator AnimateBackdropColor(Color fromColor, Color toColor, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Color c = Color.Lerp(fromColor, toColor, t);
            c.a = 1f;
            backdropImage.color = c;
            yield return null;
        }

        Color finalColor = toColor;
        finalColor.a = 1f;
        backdropImage.color = finalColor;
    }

    IEnumerator AnimateContent(
        float startOffsetX,
        float endOffsetX,
        float startAlpha,
        float endAlpha,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            SetContentOffsetX(Mathf.Lerp(startOffsetX, endOffsetX, t));
            SetContentAlpha(Mathf.Lerp(startAlpha, endAlpha, t));

            yield return null;
        }

        SetContentOffsetX(endOffsetX);
        SetContentAlpha(endAlpha);
    }

    IEnumerator AnimateScale(float startScale, float endScale, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetContentScale(Mathf.Lerp(startScale, endScale, t));
            yield return null;
        }

        SetContentScale(endScale);
    }

    IEnumerator AnimateFadeAndStamp(
        float startAlpha,
        float endAlpha,
        float startScale,
        float endScale,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            SetContentScale(Mathf.Lerp(startScale, endScale, t));

            yield return null;
        }

        canvasGroup.alpha = endAlpha;
        SetContentScale(endScale);
    }

    static TimePhase GetPreviousPhase(TimePhase targetPhase)
    {
        return targetPhase switch
        {
            TimePhase.Afternoon => TimePhase.Morning,
            TimePhase.Evening => TimePhase.Afternoon,
            TimePhase.Morning => TimePhase.Evening,
            _ => TimePhase.Morning
        };
    }

    Color GetPhaseBackdropColor(TimePhase phase)
    {
        return phase switch
        {
            TimePhase.Morning => morningBackdropColor,
            TimePhase.Afternoon => afternoonBackdropColor,
            TimePhase.Evening => eveningBackdropColor,
            _ => morningBackdropColor
        };
    }

    Sprite GetPhaseSprite(TimePhase phase)
    {
        return phase switch
        {
            TimePhase.Morning => morningSprite,
            TimePhase.Afternoon => afternoonSprite,
            TimePhase.Evening => eveningSprite,
            _ => morningSprite
        };
    }

    void ApplyPhaseStatus(TimePhase phase, int loopNumber)
    {
        ApplyStatusVisuals(GetPhaseSprite(phase), phase.ToString(), loopNumber);
    }

    void ApplyStatusVisuals(Sprite cardSprite, string timeText, int loopNumber)
    {
        if (statusCardImage != null && cardSprite != null)
            statusCardImage.sprite = cardSprite;

        if (timeTextLabel != null)
            timeTextLabel.text = timeText;

        if (loopNumberLabel != null && loopNumber >= 0)
            loopNumberLabel.text = loopNumber.ToString();
    }

    void CacheBasePosition()
    {
        if (cachedBasePosition)
            return;

        if (contentRect != null)
            contentBasePos = contentRect.anchoredPosition;

        cachedBasePosition = true;
    }

    void SetContentOffsetX(float offsetX)
    {
        if (contentRect != null)
            contentRect.anchoredPosition = contentBasePos + new Vector2(offsetX, 0f);
    }

    void SetContentAlpha(float alpha)
    {
        if (contentGroup != null)
            contentGroup.alpha = alpha;
    }

    void SetContentScale(float scale)
    {
        if (contentRect != null)
            contentRect.localScale = Vector3.one * scale;
    }

    void ResetIdleState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        if (backdropRect != null)
            backdropRect.anchoredPosition = Vector2.zero;

        SetContentOffsetX(0f);
        SetContentAlpha(1f);
        SetContentScale(1f);
    }

    float GetCanvasWidth()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            RectTransform rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.rect.width > 0f)
                return rt.rect.width;
        }

        return 586f;
    }

    bool HasRequiredReferences()
    {
        if (canvasGroup != null && backdropRect != null && backdropImage != null)
            return true;

        Debug.LogError("TimeTransition is missing required UI references in the Inspector.", this);
        return false;
    }
}
