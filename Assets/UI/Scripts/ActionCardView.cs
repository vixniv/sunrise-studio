using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class ActionCardView : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] RectTransform visual;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;
        [SerializeField] GameObject lockedIndicator;
        [SerializeField] Button button;

        [Header("State Skins")]
        [SerializeField] Sprite defaultSkin;
        [SerializeField] Sprite selectedSkin;
        [SerializeField] Sprite smallDefaultSkin;
        [SerializeField] Sprite smallSelectedSkin;

        [Header("Presentation")]
        [SerializeField] Vector2 selectedSize = new(116f, 94f);
        [SerializeField] Vector2 adjacentSize = new(110f, 88f);
        [SerializeField] Vector2 selectedOffset = new(0f, 62f);
        [SerializeField] Vector2 adjacentOffset = Vector2.zero;
        [Range(0.5f, 1f)] [SerializeField] float adjacentScale = 0.82f;
        [Range(0f, 1f)] [SerializeField] float adjacentAlpha = 0.55f;

        [Header("Tilt (degrees, same sign as Figma's rotation field: positive = counter-clockwise)")]
        [Range(-45f, 45f)] [SerializeField] float selectedTilt;
        [Tooltip("Tilt of every card to the left of the selected one.")]
        [Range(-45f, 45f)] [SerializeField] float leftTilt;
        [Tooltip("Tilt of every card to the right of the selected one.")]
        [Range(-45f, 45f)] [SerializeField] float rightTilt;

        int index;
        InteractionAction action;
        Action<int> selectedCallback;
        bool isSelected;
        int side; // -1 left of the selected card, 0 selected, 1 right
        bool pressed;

        public InteractionAction Action => action;

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
            InteractionAction newAction,
            int newIndex,
            Action<int> onSelected)
        {
            action = newAction;
            index = newIndex;
            selectedCallback = onSelected;

            if (label != null)
                label.text = action != null ? action.DisplayName : string.Empty;

            if (icon != null)
            {
                icon.sprite = action != null ? action.Icon : null;
                icon.enabled = icon.sprite != null;
            }

            bool locked = action != null && action.Locked;
            if (lockedIndicator != null)
                lockedIndicator.SetActive(locked);
            if (button != null)
                button.interactable = action != null;
        }

        public void SetSelected(bool selected)
        {
            SetSelected(selected, 0);
        }

        // sideOfSelected: negative when this card is left of the selected card, positive when right.
        public void SetSelected(bool selected, int sideOfSelected)
        {
            isSelected = selected;
            side = selected ? 0 : Math.Sign(sideOfSelected);
            RefreshState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            RefreshState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            RefreshState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pressed = false;
            RefreshState();
        }

        void RefreshState()
        {
            bool hasSmallSkin = smallDefaultSkin != null;
            if (background != null)
            {
                if (isSelected)
                    background.sprite = pressed && defaultSkin != null
                        ? defaultSkin
                        : selectedSkin != null ? selectedSkin : defaultSkin;
                else
                    background.sprite = pressed && smallSelectedSkin != null
                        ? smallSelectedSkin
                        : hasSmallSkin ? smallDefaultSkin : defaultSkin;
            }

            if (visual != null)
            {
                visual.sizeDelta = isSelected || !hasSmallSkin ? selectedSize : adjacentSize;
                visual.anchoredPosition = isSelected ? selectedOffset : adjacentOffset;
                visual.localScale = hasSmallSkin
                    ? Vector3.one
                    : Vector3.one * (isSelected ? 1f : adjacentScale);
                float tilt = isSelected ? selectedTilt : side < 0 ? leftTilt : rightTilt;
                visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
            }

            if (canvasGroup != null)
                canvasGroup.alpha = isSelected ? 1f : adjacentAlpha;
        }

        void HandleClicked()
        {
            if (action != null)
                selectedCallback?.Invoke(index);
        }
    }
}
