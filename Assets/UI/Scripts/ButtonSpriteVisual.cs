using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class ButtonSpriteVisual : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] Selectable selectable;
        [SerializeField] Image visual;
        [SerializeField] Sprite defaultSprite;
        [SerializeField] Sprite pressedSprite;
        [SerializeField] Vector2 defaultSize;
        [SerializeField] Vector2 pressedSize;

        void OnEnable() => SetPressed(false);
        void OnDisable() => SetPressed(false);

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable == null || selectable.IsInteractable())
                SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);
        public void OnPointerExit(PointerEventData eventData) => SetPressed(false);

        void SetPressed(bool pressed)
        {
            if (visual == null)
                return;
            visual.sprite = pressed && pressedSprite != null ? pressedSprite : defaultSprite;
            visual.rectTransform.sizeDelta = pressed && pressedSize != Vector2.zero ? pressedSize : defaultSize;
        }
    }
}
