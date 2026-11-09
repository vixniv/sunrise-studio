using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    /// <summary>
    /// Native-resolution HUD replacement for the world-space InteractionCue visual.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class InteractionCueHudView : MonoBehaviour
    {
        [SerializeField] RectTransform root;
        [SerializeField] Image background;
        [SerializeField] TMP_Text label;
        [SerializeField] CanvasGroup group;

        [Header("Placement")]
        [SerializeField, Min(0f)] float worldGap = 0.25f;
        [SerializeField] Vector2 canvasOffset = new Vector2(0f, 6f);
        [SerializeField, Min(0f)] float screenMargin = 6f;
        [SerializeField] RectTransform avoidRect;
        [SerializeField, Min(0f)] float avoidanceGap = 4f;

        [Header("Size")]
        [SerializeField, Min(1f)] float minimumWidth = 48f;
        [SerializeField, Min(1f)] float maximumWidth = 180f;
        [SerializeField, Min(1f)] float minimumHeight = 22f;
        [SerializeField, Min(0f)] float horizontalPadding = 10f;
        [SerializeField, Min(0f)] float verticalPadding = 3f;

        Canvas parentCanvas;
        RectTransform parentRect;
        string shownText = string.Empty;

        void Awake()
        {
            ResolveReferences();
            ConfigureLabel();
            SetVisible(false);
        }

        void OnEnable()
        {
            ResolveReferences();
            InteractionCue.SetHudPresenterActive(true);
        }

        void OnDisable()
        {
            InteractionCue.SetHudPresenterActive(false);
        }

        void LateUpdate()
        {
            if (!InteractionCue.TryGetPresentation(out string objectLabel, out Bounds bounds))
            {
                SetVisible(false);
                return;
            }

            Camera worldCamera = Camera.main;
            if (worldCamera == null || parentRect == null || root == null)
            {
                SetVisible(false);
                return;
            }

            if (shownText != objectLabel)
            {
                shownText = objectLabel;
                label.text = objectLabel;
                ResizeToText();
            }

            Vector3 worldPoint = new Vector3(bounds.center.x, bounds.max.y + worldGap, bounds.center.z);
            Vector2 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);
            Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? parentCanvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                SetVisible(false);
                return;
            }

            localPoint += canvasOffset;
            localPoint.x = Mathf.Round(localPoint.x);
            localPoint.y = Mathf.Round(localPoint.y);
            localPoint = ClampToParent(localPoint);
            root.anchoredPosition = localPoint;
            root.anchoredPosition = AvoidInteractButton(localPoint);
            SetVisible(true);
        }

        void ResizeToText()
        {
            if (label == null || root == null)
                return;

            Vector2 preferred = label.GetPreferredValues(label.text, 10000f, 0f);
            float width = Mathf.Clamp(
                preferred.x + horizontalPadding * 2f,
                minimumWidth,
                maximumWidth);
            float height = Mathf.Max(minimumHeight, preferred.y + verticalPadding * 2f);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(width));
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(height));
        }

        Vector2 ClampToParent(Vector2 position)
        {
            Rect area = parentRect.rect;
            float halfWidth = root.rect.width * 0.5f;
            float height = root.rect.height;
            position.x = Mathf.Clamp(position.x, area.xMin + halfWidth + screenMargin, area.xMax - halfWidth - screenMargin);
            position.y = Mathf.Clamp(position.y, area.yMin + screenMargin, area.yMax - height - screenMargin);
            return position;
        }

        Vector2 AvoidInteractButton(Vector2 position)
        {
            if (avoidRect == null || !avoidRect.gameObject.activeInHierarchy)
                return position;

            Bounds labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, root);
            Bounds buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, avoidRect);

            bool overlapsHorizontally = labelBounds.max.x + avoidanceGap > buttonBounds.min.x
                && labelBounds.min.x - avoidanceGap < buttonBounds.max.x;
            bool overlapsVertically = labelBounds.max.y + avoidanceGap > buttonBounds.min.y
                && labelBounds.min.y - avoidanceGap < buttonBounds.max.y;

            if (!overlapsHorizontally || !overlapsVertically)
                return position;

            // Keep the cue beneath the interact button, including a guaranteed clear gap.
            position.y += buttonBounds.min.y - avoidanceGap - labelBounds.max.y;
            position.y = Mathf.Round(position.y);
            return ClampToParent(position);
        }

        void ResolveReferences()
        {
            if (root == null) root = transform as RectTransform;
            if (background == null) background = GetComponent<Image>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (group == null) group = GetComponent<CanvasGroup>();
            if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
            if (parentRect == null && root != null) parentRect = root.parent as RectTransform;
            if (avoidRect == null && parentRect != null)
                avoidRect = parentRect.Find("GameplayControls/InteractButton") as RectTransform;
        }

        void ConfigureLabel()
        {
            if (label == null)
                return;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.raycastTarget = false;
            if (background != null) background.raycastTarget = false;
        }

        void SetVisible(bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}
