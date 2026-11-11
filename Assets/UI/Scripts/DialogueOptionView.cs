using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class DialogueOptionView : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] Button button;
        [SerializeField] RawImage visual;
        [SerializeField] TMP_Text label;
        [SerializeField] Texture defaultTexture;
        [SerializeField] Texture selectedTexture;
        [SerializeField] Rect defaultUv = new(0.008333f, 0.146341f, 0.5f, 0.780488f);
        [SerializeField] Rect selectedUv = new(0.079167f, 0.023256f, 0.533333f, 0.930233f);
        [SerializeField] Vector2 defaultSize = new(156f, 34f);
        [SerializeField] Vector2 selectedSize = new(164f, 44f);
        [SerializeField] Vector2 twoLineDefaultSize = new(156f, 50f);
        [SerializeField] Vector2 twoLineSelectedSize = new(164f, 60f);
        [SerializeField, Min(0f)] float horizontalTextPadding = 24f;
        [SerializeField, Min(0f)] float verticalTextPadding = 8f;
        [SerializeField] Color defaultTextColor = new(0.027f, 0.051f, 0.169f, 1f);
        [SerializeField] Color selectedTextColor = Color.white;
        [SerializeField, Range(0.2f, 1f)] float visualScale = 0.35f;
        [Tooltip("Rotates the complete choice card so its artwork, tape, and text stay aligned.")]
        [SerializeField, Range(-10f, 10f)] float tiltDegrees = 2f;

        Action chosen;
        RectTransform root;
        LayoutElement layoutElement;
        bool hovered;
        bool pressed;
        bool selected;
        int lineCount = 1;
        float lineHeight;
        float authoredFontSize;
        Quaternion authoredRotation = Quaternion.identity;
        bool rotationCaptured;

        float ScaledLeftPadding => horizontalTextPadding * 1.4f * visualScale;
        float ScaledRightPadding => horizontalTextPadding * 0.5f * visualScale;

        float GetTextWidth(float bubbleWidth) => Mathf.Max(
            1f,
            bubbleWidth - ScaledLeftPadding - ScaledRightPadding);

        public void Bind(string text, Action onChosen)
        {
            chosen = onChosen;
            ApplyTilt();
            if (label != null)
            {
                if (authoredFontSize <= 0f)
                    authoredFontSize = label.fontSize;
                label.fontSize = authoredFontSize * visualScale;
            }
            label.text = text;
            ConfigureHeight(text);
            button.onClick.RemoveListener(Choose);
            button.onClick.AddListener(Choose);
            SetState(false);
        }

        void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Choose);
            chosen = null;
        }

        void Choose() => chosen?.Invoke();

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData eventData) { pressed = true; Refresh(); }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }
        public void OnSelect(BaseEventData eventData) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData eventData) { selected = false; Refresh(); }

        void Refresh() => SetState(hovered || pressed || selected);

        void ApplyTilt()
        {
            if (root == null)
                root = transform as RectTransform;
            if (root == null)
                return;

            if (!rotationCaptured)
            {
                authoredRotation = root.localRotation;
                rotationCaptured = true;
            }

            root.localRotation = authoredRotation * Quaternion.Euler(0f, 0f, tiltDegrees);
        }

        void ConfigureHeight(string text)
        {
            if (label == null)
                return;

            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;

            Vector2 scaledDefaultSize = defaultSize * visualScale;
            Vector2 scaledSelectedSize = selectedSize * visualScale;
            Vector2 scaledTwoLineSelectedSize = twoLineSelectedSize * visualScale;
            float textWidth = GetTextWidth(scaledDefaultSize.x);
            lineHeight = Mathf.Max(1f, label.GetPreferredValues("Ag", textWidth, 0f).y);
            float preferredHeight = label.GetPreferredValues(text ?? string.Empty, textWidth, 0f).y;
            lineCount = Mathf.Max(1, Mathf.RoundToInt(preferredHeight / lineHeight));

            if (root == null)
                root = transform as RectTransform;
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();

            float extraHeight = Mathf.Max(0, lineCount - 2) * lineHeight;
            float fixedHeight = lineCount == 1
                ? scaledSelectedSize.y
                : scaledTwoLineSelectedSize.y + extraHeight;
            float fixedWidth = Mathf.Max(scaledSelectedSize.x, scaledTwoLineSelectedSize.x);

            if (root != null)
            {
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, fixedWidth);
                root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, fixedHeight);
            }
            if (layoutElement != null)
            {
                layoutElement.minWidth = fixedWidth;
                layoutElement.preferredWidth = fixedWidth;
                layoutElement.minHeight = fixedHeight;
                layoutElement.preferredHeight = fixedHeight;
            }

            if (transform.parent is RectTransform parent)
                LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        void SetState(bool highlighted)
        {
            Vector2 size;
            if (lineCount == 1)
            {
                size = (highlighted ? selectedSize : defaultSize) * visualScale;
            }
            else
            {
                size = (highlighted ? twoLineSelectedSize : twoLineDefaultSize) * visualScale;
                size.y += Mathf.Max(0, lineCount - 2) * lineHeight;
            }

            if (visual != null)
            {
                visual.texture = highlighted ? selectedTexture : defaultTexture;
                visual.uvRect = highlighted ? selectedUv : defaultUv;
                visual.rectTransform.sizeDelta = size;
            }

            if (label != null)
            {
                label.color = highlighted ? selectedTextColor : defaultTextColor;
                float textHeight = lineCount == 1
                    ? 30f * visualScale
                    : Mathf.Max(1f, size.y - verticalTextPadding * visualScale);
                RectTransform labelRect = label.rectTransform;
                labelRect.sizeDelta = new Vector2(
                    GetTextWidth(size.x),
                    textHeight);

                // The artwork is right-anchored and changes size for each state.
                // Keep the label centred on the artwork instead of leaving it at
                // the authored position for the original, much larger bubble.
                if (visual != null)
                {
                    RectTransform visualRect = visual.rectTransform;
                    labelRect.anchorMin = visualRect.anchorMin;
                    labelRect.anchorMax = visualRect.anchorMax;
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.anchoredPosition = visualRect.anchoredPosition + new Vector2(
                        (0.5f - visualRect.pivot.x) * size.x
                            + (ScaledLeftPadding - ScaledRightPadding) * 0.5f,
                        (0.5f - visualRect.pivot.y) * size.y);
                }
            }
        }
    }
}
