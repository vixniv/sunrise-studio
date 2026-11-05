using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class PauseStripButtonVisual : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ICancelHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] Selectable selectable;
        [SerializeField] Image background;
        [SerializeField] GameObject defaultPaper;
        [SerializeField] TMP_Text label;
        [SerializeField] Sprite defaultShadowSprite;
        [SerializeField] Sprite selectedSprite;
        [SerializeField] Vector2 defaultSize = new(275f, 40.5f);
        [SerializeField] Vector2 selectedSize = new(280f, 45f);
        [SerializeField] Color defaultTextColor = new(.027f, .051f, .169f, 1f);
        [SerializeField] Color selectedTextColor = Color.white;

        bool pointerInside;
        bool pressed;

        void OnEnable()
        {
            pointerInside = false;
            pressed = false;
            Refresh();
        }

        void OnDisable()
        {
            pointerInside = false;
            pressed = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pressed = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            pressed = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            Refresh();
        }

        public void OnCancel(BaseEventData eventData)
        {
            pressed = false;
            Refresh();
        }

        public void OnSelect(BaseEventData eventData) => Refresh();

        public void OnDeselect(BaseEventData eventData)
        {
            pressed = false;
            Refresh();
        }

        void Refresh()
        {
            bool active = pressed && pointerInside &&
                          (selectable == null || selectable.IsInteractable());
            if (background != null)
            {
                background.sprite = active && selectedSprite != null
                    ? selectedSprite
                    : defaultShadowSprite;
                background.rectTransform.sizeDelta = active ? selectedSize : defaultSize;
            }
            if (defaultPaper != null)
                defaultPaper.SetActive(!active);
            if (label != null)
                label.color = active ? selectedTextColor : defaultTextColor;
        }
    }
}
