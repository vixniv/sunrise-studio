using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class PressedTextVisual : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
    {
        [SerializeField] Selectable selectable;
        [SerializeField] TMP_Text label;
        [SerializeField] Vector2 pressedOffset = new(0f, -2f);
        [SerializeField] Color pressedColor = new Color32(0x35, 0xE0, 0xFF, 0xFF);

        Vector2 restPosition;
        Color restColor;

        void Awake()
        {
            if (label == null)
                return;

            restPosition = label.rectTransform.anchoredPosition;
            restColor = label.color;
        }

        void OnEnable() => SetPressed(false);
        void OnDisable() => SetPressed(false);

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left ||
                selectable != null && !selectable.IsInteractable())
                return;

            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);
        public void OnPointerExit(PointerEventData eventData) => SetPressed(false);
        public void OnCancel(BaseEventData eventData) => SetPressed(false);

        void SetPressed(bool pressed)
        {
            if (label == null)
                return;

            label.rectTransform.anchoredPosition =
                restPosition + (pressed ? pressedOffset : Vector2.zero);
            label.color = pressed ? pressedColor : restColor;
        }
    }
}
