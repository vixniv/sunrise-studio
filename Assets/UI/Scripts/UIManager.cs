using System;
using System.Collections.Generic;
using UnityEngine;

namespace SunriseStudio.UI
{
    public enum UIScreenId
    {
        None,
        Title,
        Config,
        Map,
        QuestLog
    }

    public sealed class UIManager : MonoBehaviour
    {
        [Serializable]
        sealed class ScreenBinding
        {
            public UIScreenId id;
            public GameObject root;
        }

        [Header("Canvas Layers")]
        [SerializeField] Transform screenRoot;
        [SerializeField] GameObject gameplayHUD;
        [SerializeField] Transform modalRoot;
        [SerializeField] Transform transitionRoot;

        [Header("Screens")]
        [SerializeField] UIScreenId initialScreen = UIScreenId.Title;
        [SerializeField] List<ScreenBinding> screens = new();

        [Header("Events")]
        [SerializeField] ScreenIdEvent onScreenChanged = new();

        public UIScreenId CurrentScreen { get; private set; }
        public Transform ScreenRoot => screenRoot;
        public Transform ModalRoot => modalRoot;
        public Transform TransitionRoot => transitionRoot;

        void Awake()
        {
            ShowScreen(initialScreen);
        }

        public void ShowScreen(UIScreenId screenId)
        {
            bool found = screenId == UIScreenId.None;

            if (!found)
            {
                foreach (ScreenBinding binding in screens)
                {
                    if (binding.id == screenId && binding.root != null)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                Debug.LogWarning($"No screen is registered for {screenId}.", this);
                return;
            }

            foreach (ScreenBinding binding in screens)
            {
                if (binding.root == null)
                    continue;

                bool shouldShow = binding.id == screenId;
                binding.root.SetActive(shouldShow);
            }

            CurrentScreen = screenId;
            onScreenChanged.Invoke(CurrentScreen);
        }

        public void HideScreens()
        {
            ShowScreen(UIScreenId.None);
        }

        public void SetGameplayHUDVisible(bool visible)
        {
            if (gameplayHUD != null)
                gameplayHUD.SetActive(visible);
        }
    }
}
