using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class ActionCarouselController : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Carousel")]
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] RectTransform content;
        [SerializeField] ActionCardView cardPrefab;
        [Min(0.01f)] [SerializeField] float snapSpeed = 14f;
        [Min(0)] [SerializeField] int initialIndex;

        [Header("Circular Orbit")]
        [SerializeField] bool loop = true;
        [Tooltip("Horizontal and vertical radius of the card orbit, in Canvas units.")]
        [SerializeField] Vector2 orbitRadius = new(78f, 32f);
        [Range(10f, 90f)] [SerializeField] float degreesPerCard = 55f;
        [Min(10f)] [SerializeField] float dragPixelsPerCard = 64f;
        [Range(0.4f, 1f)] [SerializeField] float farCardScale = 0.72f;
        [Range(1f, 4f)] [SerializeField] float visibleCardRange = 2.2f;

        [Header("Selection")]
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text descriptionLabel;
        [SerializeField] TMP_Text timeCostLabel;
        [SerializeField] Button confirmButton;
        [SerializeField] Button closeButton;
        [SerializeField] string timeCostFormat = "{0} min";

        [Header("Events")]
        [SerializeField] StringEvent onActionConfirmed = new();
        [SerializeField] UnityEvent onCloseRequested = new();

        readonly List<InteractionAction> actions = new();
        readonly List<ActionCardView> cards = new();

        int selectedIndex = -1;
        float orbitPosition;
        float targetOrbitPosition;
        bool dragging;
        bool snapping;

        public InteractionAction SelectedAction =>
            selectedIndex >= 0 && selectedIndex < actions.Count
                ? actions[selectedIndex]
                : null;

        public event Action<InteractionAction> SelectionChanged;
        public event Action<InteractionAction> ActionConfirmed;

        void OnEnable()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(ConfirmSelection);
            if (closeButton != null)
                closeButton.onClick.AddListener(HandleCloseClicked);
        }

        void OnDisable()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(ConfirmSelection);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(HandleCloseClicked);
        }

        void Update()
        {
            if (!snapping || dragging || actions.Count == 0)
                return;

            orbitPosition = Mathf.Lerp(
                orbitPosition,
                targetOrbitPosition,
                1f - Mathf.Exp(-snapSpeed * Time.unscaledDeltaTime));

            if (Mathf.Abs(orbitPosition - targetOrbitPosition) <= 0.001f)
            {
                orbitPosition = targetOrbitPosition;
                snapping = false;
            }

            UpdateSelectionFromOrbit();
            RefreshOrbit();
        }

        public void SetActions(IEnumerable<InteractionAction> newActions)
        {
            ClearCards();

            if (newActions != null)
                actions.AddRange(newActions);

            if (cardPrefab == null || content == null)
            {
                Debug.LogWarning("Action carousel needs a Content transform and card prefab.", this);
                SetSelectedIndex(actions.Count > 0 ? 0 : -1);
                return;
            }

            PrepareOrbitContainer();

            for (int i = 0; i < actions.Count; i++)
            {
                ActionCardView card = Instantiate(cardPrefab, content);
                RectTransform rect = card.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(.5f, .5f);
                    rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                }

                card.Bind(actions[i], i, HandleCardClicked);
                cards.Add(card);
            }

            int startIndex = actions.Count > 0
                ? Mathf.Min(initialIndex, actions.Count - 1)
                : -1;
            orbitPosition = startIndex < 0 ? 0f : startIndex;
            targetOrbitPosition = orbitPosition;
            SetSelectedIndex(startIndex);
            RefreshOrbit();
        }

        public void SelectIndex(int index)
        {
            if (actions.Count == 0)
                return;

            index = loop ? Mod(index, actions.Count) : Mathf.Clamp(index, 0, actions.Count - 1);
            targetOrbitPosition = ClosestEquivalentPosition(index);
            SetSelectedIndex(index);
            snapping = true;
        }

        // Center card confirms. A side card rotates into the center first.
        void HandleCardClicked(int index)
        {
            float distanceFromCenter = Mathf.Abs(WrappedCardDelta(index, orbitPosition));
            if (index == selectedIndex && distanceFromCenter <= 0.08f && !dragging)
                ConfirmSelection();
            else
                SelectIndex(index);
        }

        public void SelectNext()
        {
            if (actions.Count > 0)
                SelectIndex(selectedIndex + 1);
        }

        public void SelectPrevious()
        {
            if (actions.Count > 0)
                SelectIndex(selectedIndex - 1);
        }

        public void ConfirmSelection()
        {
            InteractionAction action = SelectedAction;
            if (action == null || action.Locked)
                return;

            ActionConfirmed?.Invoke(action);
            onActionConfirmed.Invoke(action.Id);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            snapping = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (actions.Count <= 1)
                return;

            orbitPosition -= eventData.delta.x / dragPixelsPerCard;
            if (!loop)
                orbitPosition = Mathf.Clamp(orbitPosition, 0f, actions.Count - 1f);

            UpdateSelectionFromOrbit();
            RefreshOrbit();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
            if (actions.Count == 0)
                return;

            targetOrbitPosition = Mathf.Round(orbitPosition);
            if (!loop)
                targetOrbitPosition = Mathf.Clamp(targetOrbitPosition, 0f, actions.Count - 1f);
            snapping = true;
            UpdateSelectionFromOrbit();
        }

        void PrepareOrbitContainer()
        {
            if (scrollRect != null)
            {
                scrollRect.StopMovement();
                scrollRect.enabled = false;
            }

            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;

            content.anchorMin = new Vector2(.5f, .5f);
            content.anchorMax = new Vector2(.5f, .5f);
            content.pivot = new Vector2(.5f, .5f);
            content.anchoredPosition = Vector2.zero;
            RectTransform viewport = transform as RectTransform;
            if (viewport != null)
                content.sizeDelta = viewport.rect.size;
        }

        void UpdateSelectionFromOrbit()
        {
            if (actions.Count == 0)
            {
                SetSelectedIndex(-1);
                return;
            }

            int nearest = Mathf.RoundToInt(orbitPosition);
            SetSelectedIndex(loop ? Mod(nearest, actions.Count) : Mathf.Clamp(nearest, 0, actions.Count - 1));
        }

        void SetSelectedIndex(int index)
        {
            if (selectedIndex == index)
                return;

            selectedIndex = index;
            for (int i = 0; i < cards.Count; i++)
            {
                float delta = selectedIndex < 0 ? 0f : WrappedCardDelta(i, selectedIndex);
                cards[i].SetSelected(i == selectedIndex, delta < 0f ? -1 : 1);
            }

            InteractionAction selected = SelectedAction;
            if (titleLabel != null)
                titleLabel.text = selected == null ? string.Empty : selected.DisplayName;

            if (descriptionLabel != null)
            {
                descriptionLabel.text = selected == null
                    ? string.Empty
                    : selected.Locked && !string.IsNullOrWhiteSpace(selected.LockedReason)
                        ? selected.LockedReason
                        : selected.Description;
            }

            if (timeCostLabel != null)
            {
                timeCostLabel.text = selected == null
                    ? string.Empty
                    : string.Format(timeCostFormat, selected.TimeCostMinutes);
            }

            if (confirmButton != null)
                confirmButton.interactable = selected != null && !selected.Locked;

            SelectionChanged?.Invoke(selected);
        }

        void RefreshOrbit()
        {
            if (cards.Count == 0)
                return;

            for (int i = 0; i < cards.Count; i++)
            {
                RectTransform rect = cards[i].transform as RectTransform;
                if (rect == null)
                    continue;

                float delta = WrappedCardDelta(i, orbitPosition);
                float angle = delta * degreesPerCard * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(
                    Mathf.Sin(angle) * orbitRadius.x,
                    (Mathf.Cos(angle) - 1f) * orbitRadius.y);

                float closeness = 1f - Mathf.Clamp01(Mathf.Abs(delta) / 2f);
                float scale = Mathf.Lerp(farCardScale, 1f, closeness);
                rect.localScale = Vector3.one * scale;

                CanvasGroup group = cards[i].GetComponent<CanvasGroup>();
                if (group != null)
                {
                    float visibility = Mathf.Clamp01(visibleCardRange - Mathf.Abs(delta));
                    group.alpha = visibility;
                    group.blocksRaycasts = visibility > 0.25f;
                    group.interactable = visibility > 0.25f;
                }
            }

            if (selectedIndex >= 0 && selectedIndex < cards.Count)
                cards[selectedIndex].transform.SetAsLastSibling();
        }

        float ClosestEquivalentPosition(int index)
        {
            if (!loop || actions.Count <= 1)
                return index;

            float cycles = Mathf.Round((orbitPosition - index) / actions.Count);
            float candidate = index + cycles * actions.Count;
            float half = actions.Count * .5f;
            if (candidate - orbitPosition > half)
                candidate -= actions.Count;
            else if (candidate - orbitPosition < -half)
                candidate += actions.Count;
            return candidate;
        }

        float WrappedCardDelta(int cardIndex, float centerPosition)
        {
            float delta = cardIndex - centerPosition;
            if (!loop || actions.Count <= 1)
                return delta;

            float half = actions.Count * .5f;
            return Mathf.Repeat(delta + half, actions.Count) - half;
        }

        static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        void ClearCards()
        {
            foreach (ActionCardView card in cards)
            {
                if (card != null)
                {
                    card.gameObject.SetActive(false);
                    Destroy(card.gameObject);
                }
            }

            cards.Clear();
            actions.Clear();
            selectedIndex = -1;
            orbitPosition = 0f;
            targetOrbitPosition = 0f;
            snapping = false;
            dragging = false;
        }

        void HandleCloseClicked() => onCloseRequested.Invoke();
    }
}
