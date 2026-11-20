using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    /// <summary>
    /// Slowly pans an oversized UI background left and right (ping-pong),
    /// staying inside the image edges so no gap ever shows.
    /// Works with an AspectRatioFitter set to Envelope Parent: the overflow
    /// is measured every frame, so any iPhone aspect ratio is handled.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TitleBackgroundPan : MonoBehaviour
    {
        [Tooltip("Seconds for one sweep from one edge to the other.")]
        [SerializeField, Min(0.1f)] float cycleDuration = 20f;

        [Tooltip("Slow down at each edge before turning around.")]
        [SerializeField] bool easing = true;

        [Tooltip("Round the position to whole art pixels so pixel art doesn't shimmer.")]
        [SerializeField] bool pixelSnap = true;

        [Tooltip("Where the sweep starts: 0 = left part of the art visible (moves right-to-left first), 1 = right part visible.")]
        [SerializeField, Range(0f, 1f)] float startPosition = 0f;

        RectTransform rectTransform;
        RectTransform parentRect;
        Image image;
        float elapsed;

        void Awake()
        {
            rectTransform = (RectTransform)transform;
            parentRect = rectTransform.parent as RectTransform;
            image = GetComponent<Image>();
        }

        void OnEnable()
        {
            // Start the ping-pong at the chosen point.
            elapsed = startPosition * cycleDuration;
        }

        void OnDisable()
        {
            if (rectTransform != null)
                rectTransform.anchoredPosition = new Vector2(0f, rectTransform.anchoredPosition.y);
        }

        void LateUpdate()
        {
            if (parentRect == null)
                return;

            float overflow = (rectTransform.rect.width - parentRect.rect.width) * 0.5f;
            if (overflow <= 0f)
            {
                rectTransform.anchoredPosition = new Vector2(0f, rectTransform.anchoredPosition.y);
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            // 0..1..0 over two sweeps.
            float t = Mathf.PingPong(elapsed / cycleDuration, 1f);
            if (easing)
                t = Mathf.SmoothStep(0f, 1f, t);

            // t = 0: image shifted right (left part of art visible).
            // t = 1: image shifted left (right part of art visible).
            float x = Mathf.Lerp(overflow, -overflow, t);

            if (pixelSnap && image != null && image.sprite != null)
            {
                float artPixel = rectTransform.rect.width / image.sprite.rect.width;
                if (artPixel > 0f)
                    x = Mathf.Clamp(Mathf.Round(x / artPixel) * artPixel, -overflow, overflow);
            }

            rectTransform.anchoredPosition = new Vector2(x, rectTransform.anchoredPosition.y);
        }
    }
}
