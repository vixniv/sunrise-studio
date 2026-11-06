using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class MapMarkerView : MonoBehaviour
    {
        [SerializeField] RectTransform rectTransform;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;
        [SerializeField] Button button;

        string markerId;
        StringEvent selectedEvent;

        void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(HandleClicked);
        }

        void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(
            string id,
            string displayName,
            Sprite sprite,
            Vector2 normalizedPosition,
            StringEvent onSelected)
        {
            markerId = id;
            selectedEvent = onSelected;
            SetNormalizedPosition(normalizedPosition);

            if (label != null)
                label.text = displayName;
            if (icon != null && sprite != null)
                icon.sprite = sprite;
        }

        public void SetNormalizedPosition(Vector2 normalizedPosition)
        {
            if (rectTransform == null)
                rectTransform = transform as RectTransform;

            if (rectTransform == null)
                return;

            Vector2 clamped = new(
                Mathf.Clamp01(normalizedPosition.x),
                Mathf.Clamp01(normalizedPosition.y));
            rectTransform.anchorMin = clamped;
            rectTransform.anchorMax = clamped;
            rectTransform.anchoredPosition = Vector2.zero;
        }

        void HandleClicked()
        {
            if (!string.IsNullOrWhiteSpace(markerId))
                selectedEvent?.Invoke(markerId);
        }
    }
}
