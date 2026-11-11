using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    /// <summary>
    /// Displays the current quest on TAB_Journal_Default in the HUD.
    /// Put this component on the same RectTransform as the journal-tab Image.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class QuestTrackerView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] RectTransform root;
        [SerializeField] Image background;
        [SerializeField] TMP_Text objectiveLabel;

        [Header("Top-left placement")]
        [Tooltip("X moves right. A negative Y moves down from the safe area's top-left corner.")]
        [SerializeField] Vector2 topLeftOffset = new Vector2(12f, -12f);

        [Header("Dynamic size")]
        [Tooltip("Smallest allowed card. Normal cards still follow the rendered text size.")]
        [SerializeField] Vector2 minimumSize = new Vector2(90f, 34f);
        [SerializeField, Min(1f)] float maximumWidth = 220f;

        [Header("Text padding")]
        [Tooltip("Extra space for the blue tape baked into the left side of TAB_Journal_Default.")]
        [SerializeField, Min(0f)] float leftPadding = 30f;
        [SerializeField, Min(0f)] float rightPadding = 10f;
        [SerializeField, Min(0f)] float topPadding = 7f;
        [SerializeField, Min(0f)] float bottomPadding = 7f;

        CanvasGroup canvasGroup;

        void Reset()
        {
            ResolveReferences();
            ApplyTopLeftPlacement();
        }

        void Awake()
        {
            ResolveReferences();
            ApplyTopLeftPlacement();
        }

        void OnEnable()
        {
            QuestTracker.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            QuestTracker.Changed -= Refresh;
        }

        void OnValidate()
        {
            minimumSize.x = Mathf.Max(1f, minimumSize.x);
            minimumSize.y = Mathf.Max(1f, minimumSize.y);
            maximumWidth = Mathf.Max(minimumSize.x, maximumWidth);

            ResolveReferences();
            ApplyTopLeftPlacement();

            if (objectiveLabel != null && !string.IsNullOrWhiteSpace(objectiveLabel.text))
                ResizeToText(objectiveLabel.text);
        }

        public void Refresh()
        {
            ResolveReferences();

            bool visible = QuestTracker.HasActiveQuest;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (!visible || objectiveLabel == null)
                return;

            objectiveLabel.text = QuestTracker.Objective;
            ResizeToText(objectiveLabel.text);
        }

        void ResizeToText(string text)
        {
            if (root == null || objectiveLabel == null)
                return;

            float horizontalPadding = leftPadding + rightPadding;
            float usableMaximumWidth = Mathf.Max(1f, maximumWidth - horizontalPadding);

            // Measure one line first. Long objectives are capped, then wrap vertically.
            Vector2 oneLine = objectiveLabel.GetPreferredValues(text, 10000f, 0f);
            float width = Mathf.Clamp(
                oneLine.x + horizontalPadding,
                minimumSize.x,
                maximumWidth);

            float textWidth = Mathf.Max(1f, width - horizontalPadding);
            Vector2 wrapped = objectiveLabel.GetPreferredValues(text, textWidth, 0f);
            float height = Mathf.Max(
                minimumSize.y,
                wrapped.y + topPadding + bottomPadding);

            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            RectTransform labelRect = objectiveLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(leftPadding, -topPadding);
            labelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
            labelRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(1f, height - topPadding - bottomPadding));

            StretchBackgroundToRoot();
        }

        void ResolveReferences()
        {
            if (root == null)
                root = transform as RectTransform;
            if (background == null)
                background = GetComponent<Image>();
            if (objectiveLabel == null)
                objectiveLabel = GetComponentInChildren<TMP_Text>(true);
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        void ApplyTopLeftPlacement()
        {
            if (root == null)
                return;

            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = topLeftOffset;
        }

        void StretchBackgroundToRoot()
        {
            if (background == null || background.rectTransform == root)
                return;

            RectTransform backgroundRect = background.rectTransform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
        }
    }
}
