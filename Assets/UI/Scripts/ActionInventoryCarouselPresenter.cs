using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class ActionInventoryCarouselPresenter : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] GameObject carouselRoot;
        [SerializeField] ActionCarouselController carousel;
        [SerializeField] Button dismissButton;

        [Header("Follow Character")]
        [Tooltip("Found automatically from the carousel when left empty.")]
        [SerializeField] RectTransform carouselViewport;
        [Tooltip("Found automatically under Carousel Root when left empty.")]
        [SerializeField] RectTransform titlePill;
        [Tooltip("Canvas-space distance from the top of the character to the orbit center.")]
        [SerializeField] Vector2 characterScreenOffset = new(0f, 42f);
        [Min(0f)] [SerializeField] float characterWorldPadding = 0.15f;

        [Header("Temporary Inspector Data")]
        [Tooltip("Useful while gameplay systems are not connected yet.")]
        [SerializeField] List<InteractionAction> actions = new();
        [SerializeField] List<InventoryItemStack> inventory = new();

        readonly List<InteractionAction> inventoryActions = new();
        Action<InteractionAction> confirmedCallback;
        Action closedCallback;
        Transform worldAnchor;
        RectTransform positioningRoot;
        Canvas parentCanvas;
        Vector2 defaultViewportPosition;
        Vector2 defaultTitlePosition;
        Vector2 titleOffsetFromViewport;
        bool positioningInitialized;

        public bool IsOpen => carouselRoot != null && carouselRoot.activeSelf;

        void Awake()
        {
            ResolvePositioningReferences();
            if (carouselRoot != null)
                carouselRoot.SetActive(false);
        }

        void LateUpdate()
        {
            if (IsOpen && worldAnchor != null)
                PositionAboveWorldAnchor();
        }

        void OnEnable()
        {
            if (carousel != null)
                carousel.ActionConfirmed += HandleConfirmed;
            if (dismissButton != null)
                dismissButton.onClick.AddListener(Close);
        }

        void OnDisable()
        {
            if (carousel != null)
                carousel.ActionConfirmed -= HandleConfirmed;
            if (dismissButton != null)
                dismissButton.onClick.RemoveListener(Close);
            confirmedCallback = null;
            closedCallback = null;
        }

        public void SetActions(IEnumerable<InteractionAction> availableActions)
        {
            actions.Clear();
            if (availableActions != null)
                actions.AddRange(availableActions);
        }

        public void SetInventory(IEnumerable<InventoryItemStack> itemStacks)
        {
            inventory.Clear();
            if (itemStacks != null)
                inventory.AddRange(itemStacks);
        }

        public void OpenActions()
        {
            confirmedCallback = null;
            closedCallback = null;
            Open(actions);
        }

        public void OpenActions(IEnumerable<InteractionAction> availableActions, Action<InteractionAction> onConfirmed)
        {
            confirmedCallback = onConfirmed;
            closedCallback = null;
            Open(availableActions);
        }

        public void OpenActions(
            IEnumerable<InteractionAction> availableActions,
            Action<InteractionAction> onConfirmed,
            Action onClosed)
        {
            confirmedCallback = onConfirmed;
            closedCallback = onClosed;
            Open(availableActions);
        }

        public void OpenActions(
            IEnumerable<InteractionAction> availableActions,
            Action<InteractionAction> onConfirmed,
            Action onClosed,
            Transform characterAnchor)
        {
            confirmedCallback = onConfirmed;
            closedCallback = onClosed;
            Open(availableActions, characterAnchor);
        }

        public void OpenInventory()
        {
            confirmedCallback = null;
            closedCallback = null;
            BuildInventoryActions();
            Open(inventoryActions);
        }

        public void OpenInventory(Transform characterAnchor)
        {
            confirmedCallback = null;
            closedCallback = null;
            BuildInventoryActions();
            Open(inventoryActions, characterAnchor);
        }

        void BuildInventoryActions()
        {
            inventoryActions.Clear();
            foreach (InventoryItemStack stack in inventory)
            {
                if (stack == null || stack.Item == null || stack.Quantity <= 0)
                    continue;

                InventoryItemData item = stack.Item;
                InteractionAction use = item.UseAction;
                string displayName = stack.Quantity > 1
                    ? $"{item.DisplayName} ×{stack.Quantity}"
                    : item.DisplayName;

                inventoryActions.Add(use == null
                    ? new InteractionAction(
                        $"inventory:{item.Id}", displayName, item.Description, 0, item.Icon,
                        locked: true, lockedReason: "This item cannot be used here.")
                    : new InteractionAction(
                        use.Id, displayName, item.Description, use.TimeCostMinutes, item.Icon,
                        use.Locked, use.LockedReason));
            }
        }

        public void Close()
        {
            confirmedCallback = null;
            Action callback = closedCallback;
            closedCallback = null;
            worldAnchor = null;
            RestoreDefaultPosition();
            if (carouselRoot != null)
                carouselRoot.SetActive(false);
            callback?.Invoke();
        }

        void Open(IEnumerable<InteractionAction> entries)
        {
            Open(entries, null);
        }

        void Open(IEnumerable<InteractionAction> entries, Transform characterAnchor)
        {
            ResolvePositioningReferences();
            worldAnchor = characterAnchor;
            if (carouselRoot != null)
                carouselRoot.SetActive(true);
            if (worldAnchor == null)
                RestoreDefaultPosition();
            else
                PositionAboveWorldAnchor();
            if (carousel != null)
                carousel.SetActions(entries);
        }

        void ResolvePositioningReferences()
        {
            if (carouselViewport == null && carousel != null)
                carouselViewport = carousel.transform as RectTransform;
            if (positioningRoot == null && carouselRoot != null)
                positioningRoot = carouselRoot.transform as RectTransform;
            if (titlePill == null && carouselRoot != null)
            {
                Transform found = carouselRoot.transform.Find("TitlePill");
                if (found != null)
                    titlePill = found as RectTransform;
            }
            if (parentCanvas == null && carouselRoot != null)
                parentCanvas = carouselRoot.GetComponentInParent<Canvas>();

            if (!positioningInitialized && carouselViewport != null)
            {
                defaultViewportPosition = carouselViewport.anchoredPosition;
                if (titlePill != null)
                {
                    defaultTitlePosition = titlePill.anchoredPosition;
                    titleOffsetFromViewport = defaultTitlePosition - defaultViewportPosition;
                }
                positioningInitialized = true;
            }
        }

        void RestoreDefaultPosition()
        {
            if (carouselViewport != null)
                carouselViewport.anchoredPosition = defaultViewportPosition;
            if (titlePill != null)
                titlePill.anchoredPosition = defaultTitlePosition;
        }

        void PositionAboveWorldAnchor()
        {
            if (worldAnchor == null || positioningRoot == null || carouselViewport == null)
                return;

            Camera worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            Vector3 worldPoint = worldAnchor.position;
            SpriteRenderer sprite = worldAnchor.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
            {
                worldPoint.x = sprite.bounds.center.x;
                worldPoint.y = sprite.bounds.max.y;
            }
            worldPoint.y += characterWorldPadding;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPoint);
            Camera uiCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? parentCanvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    positioningRoot, screenPoint, uiCamera, out Vector2 localPoint))
                return;

            Vector2 desired = localPoint + characterScreenOffset;
            Rect rootRect = positioningRoot.rect;
            Vector2 viewportHalf = carouselViewport.rect.size * .5f;
            float titleTop = titlePill == null
                ? viewportHalf.y
                : titleOffsetFromViewport.y + titlePill.rect.height * .5f;

            desired.x = Mathf.Clamp(
                desired.x,
                rootRect.xMin + viewportHalf.x,
                rootRect.xMax - viewportHalf.x);
            desired.y = Mathf.Clamp(
                desired.y,
                rootRect.yMin + viewportHalf.y,
                rootRect.yMax - titleTop);

            carouselViewport.anchoredPosition = desired;
            if (titlePill != null)
                titlePill.anchoredPosition = desired + titleOffsetFromViewport;
        }

        void HandleConfirmed(InteractionAction action)
        {
            Action<InteractionAction> callback = confirmedCallback;
            Close();
            callback?.Invoke(action);
        }
    }
}
