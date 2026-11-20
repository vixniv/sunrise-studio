using System.Collections;
using UnityEngine;

namespace SunriseStudio.UI
{
    // Movie-style black bars for in-game cutscenes. Yarn drives it:
    //   <<cinematic_start>>  bars slide in from the top and bottom, the gameplay HUD fades out
    //   <<cinematic_end>>    bars slide out, the gameplay HUD fades back in
    // When a dialogue ends with the bars still on, YarnCommands hides them, so the HUD always comes back.
    // Put it on a full-screen child of the HUD canvas, outside SafeArea (so the bars reach the screen edges),
    // as the canvas's first child so dialogue lines draw above the bars.
    public sealed class CinematicBarsView : MonoBehaviour
    {
        [Tooltip("Black Image for the top bar. Its anchors are set in code: top edge, full width.")]
        [SerializeField] RectTransform topBar;
        [Tooltip("Black Image for the bottom bar. Its anchors are set in code: bottom edge, full width.")]
        [SerializeField] RectTransform bottomBar;
        [Tooltip("CanvasGroup on the parent of the HUD pieces to hide (pause button, quest tracker, interact cue, action carousel). " +
                 "Keep the DialogueLayer outside it, or lines would hide too.")]
        [SerializeField] CanvasGroup hudToHide;
        [Tooltip("Height of each bar in canvas pixels (the HUD canvas is 270 tall).")]
        [SerializeField, Min(0f)] float barHeight = 28f;
        [Tooltip("Seconds the bars take to slide in or out.")]
        [SerializeField, Min(0f)] float slideSeconds = 0.35f;
        [Tooltip("Seconds the HUD takes to fade out or back in.")]
        [SerializeField, Min(0f)] float hudFadeSeconds = 0.25f;

        static CinematicBarsView active;

        Coroutine routine;
        bool shown;
        // 0 = no bars, 1 = full bars.
        float barAmount;

        // The bars of the loaded scene, or null when the scene has none.
        public static CinematicBarsView Active
        {
            get
            {
                if (active == null) active = FindAnyObjectByType<CinematicBarsView>(FindObjectsInactive.Include);
                return active;
            }
        }

        public bool IsShown => shown;
        // True while the bars slide or the HUD fades; Yarn waits for it before going on.
        public bool IsAnimating => routine != null;

        void Awake()
        {
            active = this;
            SetUpBar(topBar, 1f);
            SetUpBar(bottomBar, 0f);
            Apply(0f, 1f);
            SetHudInteractive(true);
        }

        void OnDisable()
        {
            // A stopped coroutine would leave Yarn waiting forever, so jump to the end state.
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            Snap();
        }

        void OnDestroy()
        {
            if (active == this) active = null;
        }

        // <<cinematic_start>>
        public void Show() => SetShown(true);

        // <<cinematic_end>>, and when a dialogue ends with the bars still on.
        public void Hide() => SetShown(false);

        void SetShown(bool show)
        {
            if (shown == show) return;
            shown = show;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (isActiveAndEnabled)
                routine = StartCoroutine(Animate(show));
            else
                Snap();
        }

        IEnumerator Animate(bool show)
        {
            // Hidden HUD buttons must not take taps, so turn input off at once on the way in.
            if (show) SetHudInteractive(false);

            float barFrom = barAmount;
            float barTo = show ? 1f : 0f;
            float hudFrom = hudToHide != null ? hudToHide.alpha : 1f;
            float hudTo = show ? 0f : 1f;
            float duration = Mathf.Max(slideSeconds, hudFadeSeconds);

            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float slide = Progress(t, slideSeconds);
                // Ease out on the way in (fast start, soft landing), ease in on the way out.
                slide = show ? 1f - Cube(1f - slide) : Cube(slide);
                Apply(Mathf.Lerp(barFrom, barTo, slide), Mathf.Lerp(hudFrom, hudTo, Progress(t, hudFadeSeconds)));
                yield return null;
            }

            Apply(barTo, hudTo);
            if (!show) SetHudInteractive(true);
            routine = null;
        }

        void Snap()
        {
            Apply(shown ? 1f : 0f, shown ? 0f : 1f);
            SetHudInteractive(!shown);
        }

        void Apply(float bars, float hudAlpha)
        {
            barAmount = bars;
            // Whole canvas pixels keep the bar edges crisp in pixel art.
            float height = Mathf.Round(barHeight * bars);
            SetHeight(topBar, height);
            SetHeight(bottomBar, height);
            if (hudToHide != null) hudToHide.alpha = hudAlpha;
        }

        void SetHudInteractive(bool interactive)
        {
            if (hudToHide == null) return;
            hudToHide.blocksRaycasts = interactive;
            hudToHide.interactable = interactive;
        }

        static void SetUpBar(RectTransform bar, float edgeY)
        {
            if (bar == null) return;
            bar.anchorMin = new Vector2(0f, edgeY);
            bar.anchorMax = new Vector2(1f, edgeY);
            bar.pivot = new Vector2(0.5f, edgeY);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, 0f);
        }

        static void SetHeight(RectTransform bar, float height)
        {
            if (bar == null) return;
            bar.sizeDelta = new Vector2(0f, height);
        }

        static float Progress(float t, float seconds) => seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);

        static float Cube(float x) => x * x * x;
    }
}
