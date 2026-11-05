using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    public sealed class ToggleSpriteVisual : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] Toggle toggle;
        [SerializeField] Image visual;
        [SerializeField] Sprite offSprite;
        [SerializeField] Sprite onSprite;
        [SerializeField] Sprite pressedSprite;

        bool isPressed;
        bool lastIsOn;

        void Reset()
        {
            toggle = GetComponent<Toggle>();
        }

        void Awake()
        {
            if (toggle == null)
                toggle = GetComponent<Toggle>();
        }

        void OnEnable()
        {
            if (toggle == null)
                toggle = GetComponent<Toggle>();

            if (toggle != null)
                toggle.onValueChanged.AddListener(HandleValueChanged);

            Refresh();
        }

        void OnDisable()
        {
            if (toggle != null)
                toggle.onValueChanged.RemoveListener(HandleValueChanged);

            isPressed = false;
        }

        void LateUpdate()
        {
            if (toggle != null && toggle.isOn != lastIsOn)
                Refresh();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (toggle == null || !toggle.IsInteractable())
                return;

            isPressed = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPressed = false;
            Refresh();
        }

        void HandleValueChanged(bool _)
        {
            Refresh();
        }

        void Refresh()
        {
            if (visual == null)
                return;

            Sprite nextSprite = isPressed && pressedSprite != null
                ? pressedSprite
                : toggle != null && toggle.isOn
                    ? onSprite
                    : offSprite;

            if (nextSprite != null)
                visual.sprite = nextSprite;

            visual.enabled = nextSprite != null;
            lastIsOn = toggle != null && toggle.isOn;
        }
    }
}
