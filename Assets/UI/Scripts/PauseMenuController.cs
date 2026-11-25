using System.Collections;
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

        [Header("Save feedback (optional)")]
        [Tooltip("Text shown for a moment after tapping Save. Leave empty for no message.")]
        [SerializeField] TMP_Text saveStatusLabel;
        [SerializeField] string savedText = "SAVED";
        [SerializeField] string saveFailedText = "CAN'T SAVE NOW";
        [SerializeField, Min(0.1f)] float saveStatusSeconds = 1.5f;

        [Header("Events")]
        [SerializeField] UnityEvent onPaused = new();
        [SerializeField] UnityEvent onResumed = new();
        [SerializeField] UnityEvent onSaveRequested = new();
        [SerializeField] UnityEvent onSettingsRequested = new();
        [SerializeField] UnityEvent onQuestLogRequested = new();
        [SerializeField] UnityEvent onQuitRequested = new();

        float previousTimeScale = 1f;
        Coroutine saveStatusRoutine;

        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (pauseRoot != null)
                pauseRoot.SetActive(false);
            if (configRoot != null)
                configRoot.SetActive(false);
            if (questLogButton != null)
                questLogButton.gameObject.SetActive(false);
            HideSaveStatus();
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
            HideSaveStatus();

            onResumed.Invoke();
        }

        void HandleSaveClicked()
        {
            onSaveRequested.Invoke();
            bool saved = SaveSystem.Save(out string reason);
            if (!saved)
                Debug.Log($"Pause menu: could not save because {reason}.", this);
            ShowSaveStatus(saved ? savedText : saveFailedText);
        }

        void ShowSaveStatus(string message)
        {
            if (saveStatusLabel == null)
                return;
            if (saveStatusRoutine != null)
                StopCoroutine(saveStatusRoutine);
            saveStatusLabel.text = message;
            saveStatusLabel.gameObject.SetActive(true);
            saveStatusRoutine = StartCoroutine(HideSaveStatusLater());
        }

        // Realtime, because the game is paused (Time.timeScale is 0) while this shows.
        IEnumerator HideSaveStatusLater()
        {
            yield return new WaitForSecondsRealtime(saveStatusSeconds);
            saveStatusRoutine = null;
            HideSaveStatus();
        }

        void HideSaveStatus()
        {
            if (saveStatusRoutine != null)
            {
                StopCoroutine(saveStatusRoutine);
                saveStatusRoutine = null;
            }
            if (saveStatusLabel != null)
                saveStatusLabel.gameObject.SetActive(false);
        }

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
            if (CircleTransition.Instance != null)
                CircleTransition.Instance.ResetForTitleScreen();
            if (TimeTransition.Instance != null)
                TimeTransition.Instance.ResetForTitleScreen();
            if (TimeLoopResetTransition.Instance != null)
                TimeLoopResetTransition.Instance.ResetForTitleScreen();
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
