using System;
using System.Collections;
using SunriseStudio.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum StoryTimeSlot
{
    Morning = 0,
    Lunch = 1,
    AfterSchool = 2,
    Night = 3
}

/// <summary>
/// Persona-inspired Clock-Ticking Time Cycle transition:
/// - Respects hand-adjusted positions in the prefab by default (AccentSlash, WatermarkText, ClockDial, TypographyGroup).
/// - Null-safe if optional components like AccentBadgeBar or DigitalTimeText are removed.
/// - For each transition (Morning->Lunch at 12, Lunch->AfterSchool at 4, AfterSchool->Night at 9pm):
///   One hand starts almost at the target hour while the ticking hand ticks step-by-step toward it;
///   the exact moment both hands meet at the target hour, the phase switches (LUNCH / AFTER SCHOOL / NIGHT).
/// - ClockSweepArc fills smoothly from the previous slot's hour to the target slot's hour.
/// </summary>
public class TimeTransition : MonoBehaviour
{
    [Serializable]
    public struct ClockFraming
    {
        public Vector2 dialAnchoredPosition;
        public float dialScale;
        public Vector2 textAnchoredPosition;
        public Vector2 accentSlashPosition;
        public Vector2 watermarkPosition;
    }

    public static TimeTransition Instance { get; private set; }

    [Header("Canvas & Wipe Layers")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] RectTransform leadWipeRect;
    [SerializeField] Image leadWipeImage;
    [SerializeField] RectTransform mainWipeRect;
    [SerializeField] Image mainWipeImage;
    [SerializeField] RectTransform accentSlashRect;
    [SerializeField] Image accentSlashImage;
    [SerializeField] Image halftoneImage;

    [Header("Clock Dial & Hands")]
    [SerializeField] CanvasGroup contentGroup;
    [SerializeField] RectTransform contentRect;
    [SerializeField] RectTransform clockDialRect;
    [SerializeField] Image clockOuterRing;
    [SerializeField] Image clockSweepArc;
    [SerializeField] RectTransform hourHandRect;
    [SerializeField] Image hourHandImage;
    [SerializeField] RectTransform minuteHandRect;
    [SerializeField] Image minuteHandImage;
    [SerializeField] Image centerPinImage;
    [SerializeField] Image[] hourTickImages;
    [SerializeField] TMP_Text[] hourNumberTexts;

    [Header("Typography (Optional fields are safe if deleted)")]
    [SerializeField] RectTransform typographyGroupRect;
    [SerializeField] TMP_Text watermarkText;
    [SerializeField] Image accentBadgeBar;
    [SerializeField] TMP_Text digitalTimeText;
    [SerializeField] TMP_Text phaseTitleText;

    [Header("Framing Control")]
    [Tooltip("Applies each transition's saved framing preset (use the 'Save Current Layout' menu items after positioning in Scene view).")]
    [SerializeField] bool overridePositionsWithPresets = true;
    [SerializeField] ClockFraming morningToLunchFraming = new ClockFraming
    {
        dialAnchoredPosition = new Vector2(165f, -108f),
        dialScale = 1f,
        textAnchoredPosition = new Vector2(-135f, 8f),
        accentSlashPosition = new Vector2(-120f, 0f),
        watermarkPosition = Vector2.zero
    };
    [SerializeField] ClockFraming lunchToAfterSchoolFraming = new ClockFraming
    {
        dialAnchoredPosition = new Vector2(-175f, -20f),
        dialScale = 0.96f,
        textAnchoredPosition = new Vector2(138f, 8f),
        accentSlashPosition = new Vector2(-120f, 0f),
        watermarkPosition = Vector2.zero
    };
    [SerializeField] ClockFraming afterSchoolToNightFraming = new ClockFraming
    {
        dialAnchoredPosition = new Vector2(94f, 136f),
        dialScale = 1f,
        textAnchoredPosition = new Vector2(-155f, -59f),
        accentSlashPosition = new Vector2(-138f, -215f),
        watermarkPosition = new Vector2(-15f, -65f)
    };

    [Header("Base & Phase Accent Colors")]
    [SerializeField] Color inkColor = new Color32(0x07, 0x0D, 0x2B, 0xFF);
    [SerializeField] Color paperColor = new Color32(0xF7, 0xF3, 0xEE, 0xFF);
    [SerializeField] Color morningAccent = new Color32(0x6F, 0xB1, 0xFF, 0xFF);     // Blue
    [SerializeField] Color lunchAccent = new Color32(0x38, 0xCE, 0xFF, 0xFF);       // Bright Noon Blue
    [SerializeField] Color afterSchoolAccent = new Color32(0xFF, 0xB3, 0x47, 0xFF); // Warm Yellowish / Gold
    [SerializeField] Color nightAccent = new Color32(0xB2, 0x8D, 0xFF, 0xFF);       // Evening Lavender

    [Header("Clock Meeting-Tick Settings")]
    [Tooltip("Number of real-world 1-second ticks before the hands meet at the target hour.")]
    [Range(2, 10)]
    [SerializeField] int meetTickCount = 3;
    [Tooltip("Degrees per tick for the ticking hand (6 deg = 1 real clock second tick).")]
    [SerializeField] float degreesPerTick = 6f;
    [Tooltip("How close (in degrees) the slower hand starts before the target hour.")]
    [SerializeField] float nearTargetStartOffsetDeg = 4f;
    [Tooltip("Real-world interval between ticks (1.0 = exact 1-second wall clock rhythm).")]
    [Min(0.15f)]
    [SerializeField] float secondsPerTick = 1.0f;
    [Tooltip("How fast the hand snaps to the next second mark before holding still (0.08s = crisp quartz tick).")]
    [Range(0.02f, 0.25f)]
    [SerializeField] float tickSnapDuration = 0.08f;

    [Header("Timing")]
    [Min(0.05f)]
    [SerializeField] float wipeInDuration = 0.28f;
    [Min(0f)]
    [SerializeField] float layerStagger = 0.05f;
    [Min(0.05f)]
    [SerializeField] float clockIntroDuration = 0.24f;
    [Min(0f)]
    [SerializeField] float preTickHold = 0.50f;
    [Min(0f)]
    [SerializeField] float postTickHold = 0.85f;
    [Min(0.05f)]
    [SerializeField] float clockOutroDuration = 0.18f;
    [Min(0.05f)]
    [SerializeField] float wipeOutDuration = 0.28f;

    [Header("Audio (Optional)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clockTickSfx;
    [SerializeField] AudioClip finalChimeSfx;
    [Range(0f, 1f)]
    [SerializeField] float sfxVolume = 1f;

    Vector2 accentSlashBasePos;
    Vector2 watermarkBasePos;
    bool cachedBasePositions;
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

        CacheBasePositions();
        ResetIdleState();
    }

    public bool PlayCycleTransition(
        StoryTimeSlot fromSlot,
        StoryTimeSlot toSlot,
        Action whileCovered = null)
    {
        if (transitioning || !HasRequiredReferences())
            return false;

        StartCoroutine(ClockCycleRoutine(fromSlot, toSlot, whileCovered));
        return true;
    }

    public bool PlayCycleTransition(TimePhase targetPhase, Action whileCovered = null)
    {
        StoryTimeSlot to = targetPhase switch
        {
            TimePhase.Morning => StoryTimeSlot.Morning,
            TimePhase.Afternoon => StoryTimeSlot.AfterSchool,
            TimePhase.Evening => StoryTimeSlot.Night,
            _ => StoryTimeSlot.AfterSchool
        };
        StoryTimeSlot from = (StoryTimeSlot)(((int)to + 3) % 4);
        return PlayCycleTransition(from, to, whileCovered);
    }

    [ContextMenu("Test: Morning -> Lunch (meet at 12)")]
    void TestMorningToLunch()
    {
        RunTestTransition(StoryTimeSlot.Morning, StoryTimeSlot.Lunch);
    }

    [ContextMenu("Test: Lunch -> After School (meet at 4)")]
    void TestLunchToAfterSchool()
    {
        RunTestTransition(StoryTimeSlot.Lunch, StoryTimeSlot.AfterSchool);
    }

    [ContextMenu("Test: After School -> Night (meet at 9pm)")]
    void TestAfterSchoolToNight()
    {
        RunTestTransition(StoryTimeSlot.AfterSchool, StoryTimeSlot.Night);
    }

    void RunTestTransition(StoryTimeSlot fromSlot, StoryTimeSlot toSlot)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play Mode first to test the transition.", this);
            return;
        }

        TimeTransition target = Instance != null ? Instance : this;
        if (!target.gameObject.scene.IsValid())
        {
            Debug.LogWarning("No active TimeTransitionCanvas found in the scene. Drag TimeTransitionCanvas into your scene and press Play.", this);
            return;
        }

        target.PlayCycleTransition(fromSlot, toSlot);
    }

    [ContextMenu("Editor Preview: Morning -> Lunch")]
    public void EditorPreviewMorningToLunch() => ApplyEditorPreview(StoryTimeSlot.Morning, StoryTimeSlot.Lunch);

    [ContextMenu("Editor Preview: Lunch -> After School")]
    public void EditorPreviewLunchToAfterSchool() => ApplyEditorPreview(StoryTimeSlot.Lunch, StoryTimeSlot.AfterSchool);

    [ContextMenu("Editor Preview: After School -> Night (9pm)")]
    public void EditorPreviewAfterSchoolToNight() => ApplyEditorPreview(StoryTimeSlot.AfterSchool, StoryTimeSlot.Night);

    [ContextMenu("Save Current Layout -> Morning to Lunch")]
    void SaveCurrentLayoutMorningToLunch() => SaveCurrentLayoutTo(ref morningToLunchFraming, "Morning -> Lunch");

    [ContextMenu("Save Current Layout -> Lunch to After School")]
    void SaveCurrentLayoutLunchToAfterSchool() => SaveCurrentLayoutTo(ref lunchToAfterSchoolFraming, "Lunch -> After School");

    [ContextMenu("Save Current Layout -> After School to Night")]
    void SaveCurrentLayoutAfterSchoolToNight() => SaveCurrentLayoutTo(ref afterSchoolToNightFraming, "After School -> Night");

    [ContextMenu("Editor Preview: Hide Canvas (Alpha 0)")]
    public void EditorHideCanvas() => ResetIdleState();

    void SaveCurrentLayoutTo(ref ClockFraming target, string label)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, "Save TimeTransition Layout: " + label);
#endif
        if (clockDialRect != null)
        {
            target.dialAnchoredPosition = clockDialRect.anchoredPosition;
            target.dialScale = clockDialRect.localScale.x;
        }
        if (typographyGroupRect != null)
            target.textAnchoredPosition = typographyGroupRect.anchoredPosition;
        if (accentSlashRect != null)
            target.accentSlashPosition = accentSlashRect.anchoredPosition;
        if (watermarkText != null)
            target.watermarkPosition = watermarkText.rectTransform.anchoredPosition;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"Saved current Scene layout into '{label}' framing preset!", this);
    }

    void ApplyEditorPreview(StoryTimeSlot fromSlot, StoryTimeSlot toSlot)
    {
        CacheBasePositions();
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (leadWipeRect != null) leadWipeRect.anchoredPosition = Vector2.zero;
        if (mainWipeRect != null) mainWipeRect.anchoredPosition = Vector2.zero;
        if (accentSlashRect != null) accentSlashRect.anchoredPosition = accentSlashBasePos;
        if (contentGroup != null) contentGroup.alpha = 1f;
        if (contentRect != null) contentRect.anchoredPosition = Vector2.zero;

        if (overridePositionsWithPresets)
            ApplyClockFraming(GetFraming(toSlot));

        Color accent = GetSlotAccentColor(toSlot);
        if (leadWipeImage != null) leadWipeImage.color = accent;
        if (mainWipeImage != null) mainWipeImage.color = inkColor;
        if (accentSlashImage != null)
        {
            Color sc = accent;
            sc.a = 0.22f;
            accentSlashImage.color = sc;
        }

        int startHour = GetSlotHour24(fromSlot);
        int endHour = GetSlotHour24(toSlot);
        int hoursDiff = Mathf.Max(1, endHour >= startHour ? endHour - startHour : (24 - startHour) + endHour);
        float targetAngle = GetHourAngleDeg(endHour);

        ApplyHandAngles(targetAngle, targetAngle);
        ApplyAccentVisuals(accent, toSlot, endHour);
        SetupSweepArc(startHour, hoursDiff / 12f, accent);
    }

    IEnumerator ClockCycleRoutine(
        StoryTimeSlot fromSlot,
        StoryTimeSlot toSlot,
        Action whileCovered)
    {
        CacheBasePositions();
        transitioning = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.alpha = 1f;

        float offscreenX = GetOffscreenDistance();
        Color fromAccent = GetSlotAccentColor(fromSlot);
        Color toAccent = GetSlotAccentColor(toSlot);

        if (overridePositionsWithPresets)
            ApplyClockFraming(GetFraming(toSlot));

        int startHour = GetSlotHour24(fromSlot);
        int endHour = GetSlotHour24(toSlot);
        int arcHours = Mathf.Max(1, endHour >= startHour ? endHour - startHour : (24 - startHour) + endHour);
        float fullArcFill = Mathf.Clamp01(arcHours / 12f);

        // Both hands will meet at targetAngleDeg (e.g. 12 for Lunch, 4 for AfterSchool, 9 for Night)
        float targetAngleDeg = GetHourAngleDeg(endHour);
        float nearHandStartDeg = targetAngleDeg + nearTargetStartOffsetDeg;
        float tickingHandStartDeg = targetAngleDeg + (meetTickCount * degreesPerTick);

        if (leadWipeImage != null) leadWipeImage.color = fromAccent;
        if (mainWipeImage != null) mainWipeImage.color = inkColor;
        if (accentSlashImage != null)
        {
            Color slashColor = toAccent;
            slashColor.a = 0.22f;
            accentSlashImage.color = slashColor;
        }

        if (leadWipeRect != null) leadWipeRect.anchoredPosition = new Vector2(offscreenX, 0f);
        if (mainWipeRect != null) mainWipeRect.anchoredPosition = new Vector2(offscreenX, 0f);
        if (accentSlashRect != null) accentSlashRect.anchoredPosition = new Vector2(offscreenX, accentSlashBasePos.y);

        // Start with the hands just before the target hour, showing the old phase label
        ApplyHandAngles(nearHandStartDeg, tickingHandStartDeg);
        ApplyAccentVisuals(fromAccent, fromSlot, startHour);
        SetupSweepArc(startHour, 0f, fromAccent);

        if (contentGroup != null) contentGroup.alpha = 0f;
        if (contentRect != null)
        {
            contentRect.anchoredPosition = new Vector2(36f, 0f);
            contentRect.localScale = Vector3.one;
        }

        // Skip 1 frame to avoid Inspector menu deltaTime spikes
        yield return null;

        // 1. Staggered 2-layer diagonal wipe IN
        Coroutine leadIn = leadWipeRect != null
            ? StartCoroutine(MoveRectX(leadWipeRect, offscreenX, 0f, wipeInDuration, EaseOutExpo))
            : null;
        if (layerStagger > 0f)
            yield return new WaitForSecondsRealtime(layerStagger);
        Coroutine mainIn = mainWipeRect != null
            ? StartCoroutine(MoveRectX(mainWipeRect, offscreenX, 0f, wipeInDuration, EaseOutExpo))
            : null;

        if (leadIn != null) yield return leadIn;
        if (mainIn != null) yield return mainIn;

        float totalHoldTime = clockIntroDuration + preTickHold + (meetTickCount * secondsPerTick) + postTickHold + clockOutroDuration;
        Coroutine watermarkDrift = watermarkText != null
            ? StartCoroutine(DriftWatermark(totalHoldTime))
            : null;

        // 2. Clock Dial & Typography whip IN
        yield return AnimateContentWhip(36f, 0f, 0f, 1f, clockIntroDuration, EaseOutExpo);

        if (preTickHold > 0f)
            yield return new WaitForSecondsRealtime(preTickHold);

        // 3. Real-world 1-second quartz clock ticking (crisp 0.08s snap + ~0.92s stillness per tick)
        float snapDur = Mathf.Min(tickSnapDuration, secondsPerTick);
        float restDur = Mathf.Max(0f, secondsPerTick - snapDur);

        for (int tick = 1; tick <= meetTickCount; tick++)
        {
            bool isMeetingTick = tick == meetTickCount;
            float prevFrac = (tick - 1) / (float)meetTickCount;
            float nextFrac = tick / (float)meetTickCount;

            float fromNearAngle = Mathf.Lerp(nearHandStartDeg, targetAngleDeg, prevFrac);
            float toNearAngle = Mathf.Lerp(nearHandStartDeg, targetAngleDeg, nextFrac);

            float fromTickAngle = targetAngleDeg + ((meetTickCount - (tick - 1)) * degreesPerTick);
            float toTickAngle = targetAngleDeg + ((meetTickCount - tick) * degreesPerTick);

            PlayClip(isMeetingTick && finalChimeSfx != null ? finalChimeSfx : clockTickSfx);

            // Crisp mechanical snap to the next second mark
            yield return AnimateMeetingTick(
                fromNearAngle,
                toNearAngle,
                fromTickAngle,
                toTickAngle,
                Mathf.Lerp(0f, fullArcFill, prevFrac),
                Mathf.Lerp(0f, fullArcFill, nextFrac),
                Color.Lerp(fromAccent, toAccent, prevFrac),
                Color.Lerp(fromAccent, toAccent, nextFrac),
                isMeetingTick ? endHour : startHour,
                snapDur);

            if (isMeetingTick)
            {
                // The exact moment both hands meet at the target hour, flip to the new phase!
                whileCovered?.Invoke();
                ApplyAccentVisuals(toAccent, toSlot, endHour);

                if (accentSlashRect != null)
                    StartCoroutine(MoveRectX(accentSlashRect, offscreenX, accentSlashBasePos.x, 0.28f, EaseOutExpo));
            }
            else if (restDur > 0f)
            {
                // Hold dead still until the next real-world second strikes
                yield return new WaitForSecondsRealtime(restDur);
            }
        }

        if (leadWipeImage != null) leadWipeImage.color = toAccent;

        // 4. Post-meet hold so player reads LUNCH / AFTER SCHOOL / NIGHT
        if (postTickHold > 0f)
            yield return new WaitForSecondsRealtime(postTickHold);

        // 5. Clock rig whips OUT
        yield return AnimateContentWhip(0f, -42f, 1f, 0f, clockOutroDuration, EaseInQuart);

        if (watermarkDrift != null)
            StopCoroutine(watermarkDrift);

        // 6. Staggered diagonal peel-off
        Coroutine slashOut = accentSlashRect != null
            ? StartCoroutine(MoveRectX(accentSlashRect, accentSlashRect.anchoredPosition.x, -offscreenX, wipeOutDuration, EaseInQuart))
            : null;
        Coroutine mainOut = mainWipeRect != null
            ? StartCoroutine(MoveRectX(mainWipeRect, 0f, -offscreenX, wipeOutDuration, EaseInQuart))
            : null;
        if (layerStagger > 0f)
            yield return new WaitForSecondsRealtime(layerStagger);
        Coroutine leadOut = leadWipeRect != null
            ? StartCoroutine(MoveRectX(leadWipeRect, 0f, -offscreenX, wipeOutDuration, EaseInQuart))
            : null;

        if (slashOut != null) yield return slashOut;
        if (mainOut != null) yield return mainOut;
        if (leadOut != null) yield return leadOut;

        ResetIdleState();
        transitioning = false;
    }

    IEnumerator AnimateMeetingTick(
        float fromNearAngle,
        float toNearAngle,
        float fromTickAngle,
        float toTickAngle,
        float fromSweepFill,
        float toSweepFill,
        Color fromColor,
        Color toColor,
        int highlightHour24,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float rawT = Mathf.Clamp01(elapsed / duration);
            float moveT = EaseOutCubic(rawT);

            float curNear = Mathf.Lerp(fromNearAngle, toNearAngle, moveT);
            float curTick = Mathf.Lerp(fromTickAngle, toTickAngle, moveT);
            ApplyHandAngles(curNear, curTick);

            Color curAccent = Color.Lerp(fromColor, toColor, rawT);
            ApplyAccentColorOnly(curAccent, highlightHour24);

            if (clockSweepArc != null)
            {
                clockSweepArc.fillAmount = Mathf.Lerp(fromSweepFill, toSweepFill, moveT);
                Color arcCol = curAccent;
                arcCol.a = 0.32f;
                clockSweepArc.color = arcCol;
            }

            yield return null;
        }

        ApplyHandAngles(toNearAngle, toTickAngle);
        ApplyAccentColorOnly(toColor, highlightHour24);
        if (clockSweepArc != null)
        {
            clockSweepArc.fillAmount = toSweepFill;
            Color arcCol = toColor;
            arcCol.a = 0.32f;
            clockSweepArc.color = arcCol;
        }
    }

    ClockFraming GetFraming(StoryTimeSlot targetSlot)
    {
        return targetSlot switch
        {
            StoryTimeSlot.Lunch => morningToLunchFraming,
            StoryTimeSlot.AfterSchool => lunchToAfterSchoolFraming,
            StoryTimeSlot.Night => afterSchoolToNightFraming,
            _ => morningToLunchFraming
        };
    }

    void ApplyClockFraming(ClockFraming framing)
    {
        if (clockDialRect != null)
        {
            clockDialRect.anchoredPosition = framing.dialAnchoredPosition;
            clockDialRect.localScale = Vector3.one * framing.dialScale;
        }

        if (typographyGroupRect != null)
            typographyGroupRect.anchoredPosition = framing.textAnchoredPosition;

        accentSlashBasePos = framing.accentSlashPosition;
        if (accentSlashRect != null)
            accentSlashRect.anchoredPosition = accentSlashBasePos;

        watermarkBasePos = framing.watermarkPosition;
        if (watermarkText != null)
            watermarkText.rectTransform.anchoredPosition = watermarkBasePos;
    }

    void ApplyHandAngles(float hourAngleDeg, float minuteAngleDeg)
    {
        if (hourHandRect != null)
            hourHandRect.localRotation = Quaternion.Euler(0f, 0f, hourAngleDeg);
        if (minuteHandRect != null)
            minuteHandRect.localRotation = Quaternion.Euler(0f, 0f, minuteAngleDeg);
    }

    static float GetHourAngleDeg(int hour24)
    {
        float hour12 = hour24 % 12f;
        return -(hour12 / 12f) * 360f;
    }

    void SetupSweepArc(int startHour24, float fillAmount, Color accent)
    {
        if (clockSweepArc == null) return;
        clockSweepArc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, GetHourAngleDeg(startHour24));
        clockSweepArc.fillAmount = fillAmount;
        Color c = accent;
        c.a = 0.32f;
        clockSweepArc.color = c;
    }

    void ApplyAccentVisuals(Color accent, StoryTimeSlot slot, int hour24)
    {
        if (digitalTimeText != null)
            digitalTimeText.text = $"{hour24:00}:00";
        if (phaseTitleText != null)
            phaseTitleText.text = GetSlotLabel(slot);
        if (watermarkText != null)
            watermarkText.text = GetSlotLabel(slot);

        ApplyAccentColorOnly(accent, hour24);
    }

    void ApplyAccentColorOnly(Color accent, int activeHour24)
    {
        if (clockOuterRing != null) clockOuterRing.color = accent;
        if (hourHandImage != null) hourHandImage.color = accent;
        if (minuteHandImage != null) minuteHandImage.color = paperColor;
        if (centerPinImage != null) centerPinImage.color = accent;
        if (accentBadgeBar != null) accentBadgeBar.color = accent;
        if (digitalTimeText != null) digitalTimeText.color = inkColor;
        if (phaseTitleText != null) phaseTitleText.color = paperColor;

        if (halftoneImage != null)
        {
            Color hc = accent;
            hc.a = 0.08f;
            halftoneImage.color = hc;
        }

        int activeTick = activeHour24 % 12;

        if (hourTickImages != null && hourTickImages.Length == 12)
        {
            for (int i = 0; i < 12; i++)
            {
                if (hourTickImages[i] == null) continue;
                bool isActive = i == activeTick;
                bool isCardinal = (i % 3) == 0;
                hourTickImages[i].color = isActive
                    ? accent
                    : new Color(paperColor.r, paperColor.g, paperColor.b, isCardinal ? 0.85f : 0.35f);
                hourTickImages[i].rectTransform.localScale = isActive ? Vector3.one * 1.35f : Vector3.one;
            }
        }

        if (hourNumberTexts != null && hourNumberTexts.Length == 12)
        {
            for (int i = 0; i < 12; i++)
            {
                if (hourNumberTexts[i] == null) continue;
                bool isActive = i == activeTick;
                hourNumberTexts[i].color = isActive
                    ? accent
                    : new Color(paperColor.r, paperColor.g, paperColor.b, 0.55f);
                hourNumberTexts[i].rectTransform.localScale = isActive ? Vector3.one * 1.2f : Vector3.one;
            }
        }
    }

    IEnumerator MoveRectX(RectTransform rt, float startX, float endX, float duration, Func<float, float> ease)
    {
        if (rt == null) yield break;
        float elapsed = 0f;
        Vector2 pos = rt.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = ease(Mathf.Clamp01(elapsed / duration));
            pos.x = Mathf.LerpUnclamped(startX, endX, t);
            rt.anchoredPosition = pos;
            yield return null;
        }

        pos.x = endX;
        rt.anchoredPosition = pos;
    }

    IEnumerator AnimateContentWhip(
        float startX,
        float endX,
        float startAlpha,
        float endAlpha,
        float duration,
        Func<float, float> ease)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = ease(Mathf.Clamp01(elapsed / duration));

            if (contentRect != null)
                contentRect.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0f);
            if (contentGroup != null)
                contentGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);

            yield return null;
        }

        if (contentRect != null)
            contentRect.anchoredPosition = new Vector2(endX, 0f);
        if (contentGroup != null)
            contentGroup.alpha = endAlpha;
    }

    IEnumerator DriftWatermark(float totalDuration)
    {
        float elapsed = 0f;
        RectTransform rt = watermarkText.rectTransform;

        while (elapsed < totalDuration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, totalDuration));
            rt.anchoredPosition = new Vector2(watermarkBasePos.x + Mathf.Lerp(40f, -40f, t), watermarkBasePos.y);

            float fadeIn = Mathf.Clamp01(elapsed / 0.25f);
            Color c = paperColor;
            c.a = 0.07f * fadeIn;
            watermarkText.color = c;
            yield return null;
        }
    }

    void CacheBasePositions()
    {
        if (cachedBasePositions)
            return;

        if (accentSlashRect != null)
            accentSlashBasePos = accentSlashRect.anchoredPosition;
        if (watermarkText != null)
            watermarkBasePos = watermarkText.rectTransform.anchoredPosition;

        cachedBasePositions = true;
    }

    void PlayClip(AudioClip clip)
    {
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

        audioSource.PlayOneShot(clip, sfxVolume);
    }

    Color GetSlotAccentColor(StoryTimeSlot slot)
    {
        return slot switch
        {
            StoryTimeSlot.Morning => morningAccent,
            StoryTimeSlot.Lunch => lunchAccent,
            StoryTimeSlot.AfterSchool => afterSchoolAccent,
            StoryTimeSlot.Night => nightAccent,
            _ => morningAccent
        };
    }

    static int GetSlotHour24(StoryTimeSlot slot)
    {
        return slot switch
        {
            StoryTimeSlot.Morning => 9,      // 9:00 AM
            StoryTimeSlot.Lunch => 12,       // 12:00 PM
            StoryTimeSlot.AfterSchool => 16, // 4:00 PM
            StoryTimeSlot.Night => 21,       // 9:00 PM (extended from 8pm to 9pm)
            _ => 9
        };
    }

    static string GetSlotLabel(StoryTimeSlot slot)
    {
        return slot switch
        {
            StoryTimeSlot.Morning => "MORNING",
            StoryTimeSlot.Lunch => "LUNCH",
            StoryTimeSlot.AfterSchool => "AFTER SCHOOL",
            StoryTimeSlot.Night => "NIGHT",
            _ => "MORNING"
        };
    }

    static float EaseOutExpo(float x) => x >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseInQuart(float x) => x * x * x * x;

    float GetOffscreenDistance()
    {
        Canvas canvas = GetComponent<Canvas>();
        float width = 586f;
        if (canvas != null)
        {
            RectTransform rt = canvas.GetComponent<RectTransform>();
            if (rt != null && rt.rect.width > 0f)
                width = rt.rect.width;
        }

        return width * 1.45f;
    }

    void ResetIdleState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        float off = GetOffscreenDistance();
        if (leadWipeRect != null) leadWipeRect.anchoredPosition = new Vector2(off, 0f);
        if (mainWipeRect != null) mainWipeRect.anchoredPosition = new Vector2(off, 0f);
        if (accentSlashRect != null) accentSlashRect.anchoredPosition = new Vector2(off, accentSlashBasePos.y);
        if (contentGroup != null) contentGroup.alpha = 0f;
    }

    bool HasRequiredReferences()
    {
        if (canvasGroup != null)
            return true;

        Debug.LogError("TimeTransition is missing a CanvasGroup reference.", this);
        return false;
    }
}
