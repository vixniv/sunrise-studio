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
        [Tooltip("Optional Button on this label. Assign it to make the label itself interactable.")]
        [SerializeField] Button interactionButton;
        [SerializeField, Range(-10f, 10f)] float labelTiltDegrees = 2f;

        [Header("Placement")]
        [SerializeField, Min(0f)] float worldGap = 0.25f;
        [SerializeField] Vector2 canvasOffset = new Vector2(0f, 6f);
        [SerializeField, Min(0f)] float screenMargin = 6f;
        [SerializeField] RectTransform avoidRect;
        [SerializeField, Min(0f)] float avoidanceGap = 4f;
        [Tooltip("Raises the label above the player's sprite when they would overlap on screen.")]
        [SerializeField] bool avoidPlayer = true;
        [SerializeField, Min(0f)] float playerGap = 4f;

        [Header("Size")]
        [SerializeField, Min(1f)] float minimumWidth = 48f;
        [SerializeField, Min(1f)] float maximumWidth = 180f;
        [SerializeField, Min(1f)] float minimumHeight = 22f;
        [SerializeField, Min(0f)] float horizontalPadding = 10f;
        [SerializeField, Min(0f)] float verticalPadding = 3f;
        [Tooltip("Text-safe part of the full sprite, in normalized coordinates. Excludes the transparent frame and shadow.")]
        [SerializeField] Rect labelArea = new Rect(0.14f, 0.19f, 0.68f, 0.61f);
        [SerializeField, Min(1f)] float minimumFontSize = 8f;

        Canvas parentCanvas;
        RectTransform parentRect;
        string shownText = string.Empty;
        float authoredFontSize;
        float lastParentWidth = -1f;
        SpriteRenderer[] playerSprites = System.Array.Empty<SpriteRenderer>();

        void Awake()
        {
            ResolveReferences();
            ConfigureLabel();
            SetVisible(false);
        }

        void OnEnable()
        {
            ResolveReferences();
            ConfigureLabel();
            if (interactionButton != null)
                interactionButton.onClick.AddListener(HandleInteractionClicked);
            InteractionCue.SetHudPresenterActive(true);
        }

        void OnDisable()
        {
            if (interactionButton != null)
                interactionButton.onClick.RemoveListener(HandleInteractionClicked);
            InteractionCue.SetHudPresenterActive(false);
        }

        void HandleInteractionClicked()
        {
            if (group == null || group.alpha <= 0f || !InteractionCue.HasCurrentTarget)
                return;

            GameplayHUDController hud = GetComponentInParent<GameplayHUDController>();
            if (hud != null)
                hud.HandleInteractClicked();
            else
                InteractionCue.TryInteractCurrent();
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

            if (shownText != objectLabel || !Mathf.Approximately(lastParentWidth, parentRect.rect.width))
            {
                shownText = objectLabel;
                lastParentWidth = parentRect.rect.width;
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
            localPoint = AvoidPlayer(localPoint, worldCamera, uiCamera);
            root.anchoredPosition = localPoint;
            root.anchoredPosition = AvoidInteractButton(localPoint);
            SetVisible(true);
        }

        void ResizeToText()
        {
            if (label == null || root == null)
                return;

            // Measure at the authored size, not the smaller size from a previous long label.
            label.enableAutoSizing = false;
            label.fontSize = authoredFontSize;
            Vector2 preferred = label.GetPreferredValues(label.text, 10000f, 0f);
            Rect area = SafeLabelArea();
            float angle = labelTiltDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Abs(Mathf.Cos(angle));
            float sine = Mathf.Abs(Mathf.Sin(angle));
            float rotatedWidth = preferred.x * cosine + preferred.y * sine;
            float rotatedHeight = preferred.x * sine + preferred.y * cosine;
            float availableWidth = parentRect != null && parentRect.rect.width > 0f
                ? Mathf.Max(1f, parentRect.rect.width - screenMargin * 2f)
                : maximumWidth;
            float widthLimit = Mathf.Max(1f, Mathf.Min(maximumWidth, availableWidth));
            float width = Mathf.Clamp((rotatedWidth + horizontalPadding * 2f) / area.width,
                Mathf.Min(minimumWidth, widthLimit), widthLimit);
            float height = Mathf.Max(minimumHeight,
                (rotatedHeight + verticalPadding * 2f) / area.height);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(width));
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(height));

            // Fit the entire rotated text rectangle inside the face. TMP only knows
            // about its unrotated rectangle, so reserve the rotation clearance here.
            float faceWidth = Mathf.Max(1f, root.rect.width * area.width - horizontalPadding * 2f);
            float faceHeight = Mathf.Max(1f, root.rect.height * area.height - verticalPadding * 2f);
            float determinant = Mathf.Max(0.01f, cosine * cosine - sine * sine);
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = area.center;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.Euler(0f, 0f, labelTiltDegrees);
            textRect.sizeDelta = new Vector2(
                Mathf.Max(1f, (faceWidth * cosine - faceHeight * sine) / determinant),
                Mathf.Max(1f, (faceHeight * cosine - faceWidth * sine) / determinant));
            label.fontSizeMax = authoredFontSize;
            label.fontSizeMin = Mathf.Min(minimumFontSize, authoredFontSize);
            label.enableAutoSizing = true;
        }

        [ContextMenu("Refresh Label Layout")]
        public void RefreshLayout()
        {
            ResolveReferences();
            ConfigureLabel();
            ResizeToText();
        }

        Rect SafeLabelArea()
        {
            float left = Mathf.Clamp(labelArea.xMin, 0f, 0.95f);
            float bottom = Mathf.Clamp(labelArea.yMin, 0f, 0.95f);
            return Rect.MinMaxRect(left, bottom,
                Mathf.Clamp(labelArea.xMax, left + 0.05f, 1f),
                Mathf.Clamp(labelArea.yMax, bottom + 0.05f, 1f));
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

        // Moves the label up until its bottom edge clears the player's sprite (Anin), so the two never overlap.
        // root must already be at `position` when this runs.
        Vector2 AvoidPlayer(Vector2 position, Camera worldCamera, Camera uiCamera)
        {
            if (!avoidPlayer || !TryGetPlayerRect(worldCamera, uiCamera, out Rect playerRect))
                return position;

            Bounds labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, root);
            bool overlapsHorizontally = labelBounds.max.x + playerGap > playerRect.xMin
                && labelBounds.min.x - playerGap < playerRect.xMax;
            bool overlapsVertically = labelBounds.max.y + playerGap > playerRect.yMin
                && labelBounds.min.y - playerGap < playerRect.yMax;

            if (!overlapsHorizontally || !overlapsVertically)
                return position;

            position.y += playerRect.yMax + playerGap - labelBounds.min.y;
            position.y = Mathf.Round(position.y);
            return ClampToParent(position);
        }

        // The player's visible sprites as one rectangle in the label's parent space.
        bool TryGetPlayerRect(Camera worldCamera, Camera uiCamera, out Rect rect)
        {
            rect = default;

            if (playerSprites.Length == 0 || playerSprites[0] == null)
            {
                PlayerMove player = FindAnyObjectByType<PlayerMove>();
                if (player == null)
                    return false;
                playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
            }

            bool found = false;
            Bounds combined = default;
            foreach (SpriteRenderer sprite in playerSprites)
            {
                // Skip the shared world-space cue, which is a child of the player.
                if (sprite == null || !sprite.enabled || sprite.sprite == null
                    || sprite.GetComponentInParent<InteractionCue>() != null)
                    continue;
                if (!found) { combined = sprite.bounds; found = true; }
                else combined.Encapsulate(sprite.bounds);
            }
            if (!found)
                return false;

            if (!TryToLocal(worldCamera, uiCamera, combined.min, out Vector2 a)
                || !TryToLocal(worldCamera, uiCamera, combined.max, out Vector2 b))
                return false;

            rect = Rect.MinMaxRect(
                Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return true;
        }

        bool TryToLocal(Camera worldCamera, Camera uiCamera, Vector3 worldPoint, out Vector2 local)
        {
            Vector2 screen = worldCamera.WorldToScreenPoint(worldPoint);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out local);
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
            if (interactionButton == null) interactionButton = GetComponent<Button>();
            if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
            if (parentRect == null && root != null) parentRect = root.parent as RectTransform;
            if (avoidRect == null && parentRect != null)
                avoidRect = parentRect.Find("GameplayControls/InteractButton") as RectTransform;
        }

        void ConfigureLabel()
        {
            if (label == null)
                return;
            if (authoredFontSize <= 0f)
                authoredFontSize = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            label.fontSizeMax = authoredFontSize;
            label.fontSizeMin = Mathf.Min(minimumFontSize, authoredFontSize);
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = Vector4.zero;
            label.horizontalAlignment = HorizontalAlignmentOptions.Center;
            label.verticalAlignment = VerticalAlignmentOptions.Middle;
            label.raycastTarget = false;
            label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, labelTiltDegrees);
            if (background != null) background.raycastTarget = interactionButton != null;
        }

        void SetVisible(bool visible)
        {
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible && interactionButton != null;
            group.blocksRaycasts = visible && interactionButton != null;
        }
    }
}
