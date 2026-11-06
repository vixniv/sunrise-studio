using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class PressedVisualOffset : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Tooltip("Move only the paper visual. Keep this component on the stationary hitbox root.")]
        [SerializeField] RectTransform visual;
        [SerializeField] Selectable selectable;
        [SerializeField] Vector2 pressedOffset = new(2f, -2f);

        Vector2 restPosition;

        void Awake()
        {
            if (visual != null)
                restPosition = visual.anchoredPosition;
        }

        void OnEnable()
        {
            ResetVisual();
        }

        void OnDisable()
        {
            ResetVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (visual == null || selectable != null && !selectable.IsInteractable())
                return;

            visual.anchoredPosition = restPosition + pressedOffset;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ResetVisual();
        }

        void ResetVisual()
        {
            if (visual != null)
                visual.anchoredPosition = restPosition;
        }
    }
}
