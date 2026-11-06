using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] GameObject pauseRoot;
        [SerializeField] Button resumeButton;
        [SerializeField] Button saveButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button questLogButton;
        [SerializeField] Button quitButton;
        [SerializeField] TMP_Text loopCountLabel;
        [SerializeField] TMP_Text timeOfDayLabel;
        [SerializeField] GameObject configRoot;
        [SerializeField] SettingsController configController;
        [SerializeField] string mainMenuScene = "TitleScreen";

        [Header("Events")]
        [SerializeField] UnityEvent onPaused = new();
        [SerializeField] UnityEvent onResumed = new();
        [SerializeField] UnityEvent onSaveRequested = new();
        [SerializeField] UnityEvent onSettingsRequested = new();
        [SerializeField] UnityEvent onQuestLogRequested = new();
        [SerializeField] UnityEvent onQuitRequested = new();

        float previousTimeScale = 1f;

        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (pauseRoot != null)
                pauseRoot.SetActive(false);
            if (configRoot != null)
                configRoot.SetActive(false);
            if (questLogButton != null)
                questLogButton.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (resumeButton != null)
                resumeButton.onClick.AddListener(Resume);
            if (saveButton != null)
                saveButton.onClick.AddListener(HandleSaveClicked);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(HandleSettingsClicked);
            if (questLogButton != null)
                questLogButton.onClick.AddListener(HandleQuestLogClicked);
            if (quitButton != null)
                quitButton.onClick.AddListener(HandleQuitClicked);
            if (configController != null)
                configController.CloseRequested += CloseSettings;
        }

        void OnDisable()
        {
            if (resumeButton != null)
                resumeButton.onClick.RemoveListener(Resume);
            if (saveButton != null)
                saveButton.onClick.RemoveListener(HandleSaveClicked);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(HandleSettingsClicked);
            if (questLogButton != null)
                questLogButton.onClick.RemoveListener(HandleQuestLogClicked);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(HandleQuitClicked);
            if (configController != null)
                configController.CloseRequested -= CloseSettings;
        }

        void OnDestroy()
        {
            if (IsPaused)
                RestoreRuntimeState();
        }

        public void Toggle()
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            IsPaused = true;

            if (configRoot != null)
                configRoot.SetActive(false);
            if (pauseRoot != null)
                pauseRoot.SetActive(true);

            RefreshStatus();
            if (EventSystem.current != null && resumeButton != null)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);

            onPaused.Invoke();
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            IsPaused = false;

            if (pauseRoot != null)
                pauseRoot.SetActive(false);
            if (configRoot != null)
                configRoot.SetActive(false);

            onResumed.Invoke();
        }

        void HandleSaveClicked() => onSaveRequested.Invoke();

        void HandleSettingsClicked()
        {
            if (!IsPaused)
                return;

            onSettingsRequested.Invoke();
            if (pauseRoot != null)
                pauseRoot.SetActive(false);
            if (configRoot != null)
                configRoot.SetActive(true);
            if (configController != null)
                configController.FocusCloseButton();
        }

        void CloseSettings()
        {
            if (configRoot != null)
                configRoot.SetActive(false);
            if (!IsPaused)
                return;

            if (pauseRoot != null)
                pauseRoot.SetActive(true);
            if (EventSystem.current != null && settingsButton != null)
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
        }

        void HandleQuestLogClicked() => onQuestLogRequested.Invoke();

        void HandleQuitClicked()
        {
            onQuitRequested.Invoke();
            RestoreRuntimeState();
            if (!string.IsNullOrWhiteSpace(mainMenuScene))
                SceneManager.LoadScene(mainMenuScene.Trim());
        }

        void RestoreRuntimeState()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsPaused = false;

            if (pauseRoot != null)
                pauseRoot.SetActive(false);
            if (configRoot != null)
                configRoot.SetActive(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            onResumed.Invoke();
        }

        void RefreshStatus()
        {
            if (loopCountLabel != null)
                loopCountLabel.text = YarnCommands.LoopsCompleted.ToString("00", CultureInfo.InvariantCulture);
            if (timeOfDayLabel != null)
            {
                string words = YarnCommands.TimeOfDay.Replace("_", " ").Trim().ToLowerInvariant();
                timeOfDayLabel.text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words).ToUpperInvariant();
            }
        }
    }
}
