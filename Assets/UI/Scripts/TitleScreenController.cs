using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class TitleScreenController : MonoBehaviour
    {
        [SerializeField] Button playButton;
        [SerializeField] Button configButton;
        [SerializeField] GameObject configRoot;
        [SerializeField] SettingsController configController;
        [SerializeField] string gameplayScene = "Dorm";

        [Header("Events")]
        [SerializeField] UnityEvent onPlayRequested = new();
        [SerializeField] UnityEvent onConfigRequested = new();

        void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (configRoot != null)
                configRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (playButton != null)
                playButton.onClick.AddListener(HandlePlayClicked);
            if (configButton != null)
                configButton.onClick.AddListener(HandleConfigClicked);
            if (configController != null)
                configController.CloseRequested += HandleConfigClosed;
        }

        void OnDisable()
        {
            if (playButton != null)
                playButton.onClick.RemoveListener(HandlePlayClicked);
            if (configButton != null)
                configButton.onClick.RemoveListener(HandleConfigClicked);
            if (configController != null)
                configController.CloseRequested -= HandleConfigClosed;
        }

        public void SetInteractable(bool interactable)
        {
            if (playButton != null)
                playButton.interactable = interactable;
            if (configButton != null)
                configButton.interactable = interactable;
        }

        void HandlePlayClicked()
        {
            if (string.IsNullOrWhiteSpace(gameplayScene))
            {
                Debug.LogError("The title screen has no gameplay scene configured.", this);
                return;
            }

            onPlayRequested.Invoke();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetInteractable(false);
            SceneManager.LoadScene(gameplayScene.Trim());
        }

        void HandleConfigClicked()
        {
            onConfigRequested.Invoke();
            SetInteractable(false);
            if (configRoot != null)
                configRoot.SetActive(true);
            if (configController != null)
                configController.FocusCloseButton();
        }

        void HandleConfigClosed()
        {
            if (configRoot != null)
                configRoot.SetActive(false);
            SetInteractable(true);
            if (EventSystem.current != null && configButton != null)
                EventSystem.current.SetSelectedGameObject(configButton.gameObject);
        }
    }
}
