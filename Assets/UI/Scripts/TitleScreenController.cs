using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class TitleScreenController : MonoBehaviour
    {
        [Tooltip("With a save: continues it. Without one: starts a new game.")]
        [SerializeField] Button playButton;
        [Tooltip("Shown only when a save exists. Starts the story from the beginning.")]
        [SerializeField] Button newGameButton;
        [SerializeField] Button configButton;
        [SerializeField] GameObject configRoot;
        [SerializeField] SettingsController configController;
        [SerializeField] string gameplayScene = "Dorm";

        [Header("Events")]
        [SerializeField] UnityEvent onPlayRequested = new();
        [SerializeField] UnityEvent onNewGameRequested = new();
        [SerializeField] UnityEvent onConfigRequested = new();

        void Awake()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (CircleTransition.Instance != null)
                CircleTransition.Instance.ResetForTitleScreen();
            if (TimeTransition.Instance != null)
                TimeTransition.Instance.ResetForTitleScreen();
            if (TimeLoopResetTransition.Instance != null)
                TimeLoopResetTransition.Instance.ResetForTitleScreen();
            if (configRoot != null)
                configRoot.SetActive(false);
            if (newGameButton != null)
                newGameButton.gameObject.SetActive(SaveSystem.HasSave);
        }

        void Start()
        {
            Haptics.Prepare();
        }

        void OnEnable()
        {
            if (playButton != null)
                playButton.onClick.AddListener(HandlePlayClicked);
            if (newGameButton != null)
                newGameButton.onClick.AddListener(HandleNewGameClicked);
            if (configButton != null)
                configButton.onClick.AddListener(HandleConfigClicked);
            if (configController != null)
                configController.CloseRequested += HandleConfigClosed;
        }

        void OnDisable()
        {
            if (playButton != null)
                playButton.onClick.RemoveListener(HandlePlayClicked);
            if (newGameButton != null)
                newGameButton.onClick.RemoveListener(HandleNewGameClicked);
            if (configButton != null)
                configButton.onClick.RemoveListener(HandleConfigClicked);
            if (configController != null)
                configController.CloseRequested -= HandleConfigClosed;
        }

        public void SetInteractable(bool interactable)
        {
            if (playButton != null)
                playButton.interactable = interactable;
            if (newGameButton != null)
                newGameButton.interactable = interactable;
            if (configButton != null)
                configButton.interactable = interactable;
        }

        void HandlePlayClicked()
        {
            Haptics.ButtonTap();

            if (SaveSystem.HasSave)
            {
                // Puts the saved story state back; the save picks the scene. A broken save starts a new game instead.
                if (SaveSystem.TryRestore(out string savedScene))
                {
                    onPlayRequested.Invoke();
                    LoadGameplay(savedScene);
                    return;
                }
                Debug.LogError("The save could not be loaded, so Play starts a new game.", this);
            }

            onPlayRequested.Invoke();
            StartNewGame();
        }

        void HandleNewGameClicked()
        {
            Haptics.ButtonTap();
            onNewGameRequested.Invoke();
            // The old save stays on the device until the new game saves over it.
            StartNewGame();
        }

        void StartNewGame()
        {
            if (string.IsNullOrWhiteSpace(gameplayScene))
            {
                Debug.LogError("The title screen has no gameplay scene configured.", this);
                return;
            }

            PersistentVariableStorage.ClearForNewGame();
            YarnCutscene.ForgetPlayed();
            YarnCommands.ResetForNewGame();
            QuestTracker.ClearQuest();
            LoadGameplay(gameplayScene.Trim());
        }

        void LoadGameplay(string sceneName)
        {
            SaveSystem.MarkGameStarted();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetInteractable(false);

            if (CircleTransition.Instance != null)
                CircleTransition.Instance.LoadSceneWithBlackFade(sceneName);
            else
                SceneManager.LoadScene(sceneName);
        }

        void HandleConfigClicked()
        {
            Haptics.ButtonTap();
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
