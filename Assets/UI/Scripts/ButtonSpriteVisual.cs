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
        [Tooltip("Scales the default and pressed artwork sizes. 1 = authored size; 0.5 = half size. Does not change label font size.")]
        [SerializeField, Min(0.01f)] float visualScale = 1f;

        void OnEnable() => SetPressed(false);
        void OnDisable() => SetPressed(false);

        void OnValidate()
        {
            visualScale = Mathf.Max(0.01f, visualScale);
            SetPressed(false);
        }

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
            Vector2 size = pressed && pressedSize != Vector2.zero ? pressedSize : defaultSize;
            visual.rectTransform.sizeDelta = size * visualScale;
        }
    }
}
