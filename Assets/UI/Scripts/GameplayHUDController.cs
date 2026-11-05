using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class GameplayHUDController : MonoBehaviour
    {
        [SerializeField] GameObject exploreHUD;
        [SerializeField] GameObject actionCarouselHUD;
        [SerializeField] Button pauseButton;
        [SerializeField] Button interactButton;
        [SerializeField] GameObject interactRoot;
        [SerializeField] UnityEvent onPauseRequested = new();
        [SerializeField] UnityEvent onInteractionRequested = new();

        PauseMenuController pauseMenu;

        void OnEnable()
        {
            if (pauseButton != null)
                pauseButton.onClick.AddListener(HandlePauseClicked);
            if (interactButton != null)
                interactButton.onClick.AddListener(HandleInteractClicked);
        }

        void OnDisable()
        {
            if (pauseButton != null)
                pauseButton.onClick.RemoveListener(HandlePauseClicked);
            if (interactButton != null)
                interactButton.onClick.RemoveListener(HandleInteractClicked);
        }

        void Update()
        {
            if (interactRoot != null)
                interactRoot.SetActive(InteractionCue.HasCurrentTarget);
        }

        public void ShowExploreMode()
        {
            SetMode(exploreVisible: true, carouselVisible: false);
        }

        public void ShowActionCarousel()
        {
            SetMode(exploreVisible: false, carouselVisible: true);
        }

        public void HideGameplayPanels()
        {
            SetMode(exploreVisible: false, carouselVisible: false);
        }

        void SetMode(bool exploreVisible, bool carouselVisible)
        {
            if (exploreHUD != null)
                exploreHUD.SetActive(exploreVisible);
            if (actionCarouselHUD != null)
                actionCarouselHUD.SetActive(carouselVisible);
        }

        void HandlePauseClicked()
        {
            if (pauseMenu == null)
                pauseMenu = FindAnyObjectByType<PauseMenuController>(FindObjectsInactive.Include);
            if (pauseMenu != null)
                pauseMenu.Toggle();
            onPauseRequested.Invoke();
        }

        void HandleInteractClicked()
        {
            if (InteractionCue.TryInteractCurrent())
                onInteractionRequested.Invoke();
        }
    }
}
