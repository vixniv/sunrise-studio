using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class InteractionPresenter : MonoBehaviour
    {
        [SerializeField] GameObject promptRoot;
        [SerializeField] TMP_Text label;
        [SerializeField] Image icon;
        [SerializeField] Button interactButton;
        [SerializeField] StringEvent onInteractionRequested = new();

        InteractionAction currentAction;

        void OnEnable()
        {
            if (interactButton != null)
                interactButton.onClick.AddListener(HandleClicked);
        }

        void OnDisable()
        {
            if (interactButton != null)
                interactButton.onClick.RemoveListener(HandleClicked);
        }

        public void Present(InteractionAction action)
        {
            currentAction = action;
            bool visible = action != null;

            if (promptRoot != null)
                promptRoot.SetActive(visible);

            if (!visible)
                return;

            if (label != null)
                label.text = action.DisplayName;

            if (icon != null)
            {
                icon.sprite = action.Icon;
                icon.enabled = action.Icon != null;
            }

            if (interactButton != null)
                interactButton.interactable = !action.Locked;
        }

        public void Clear()
        {
            Present(null);
        }

        void HandleClicked()
        {
            if (currentAction == null || currentAction.Locked)
                return;

            onInteractionRequested.Invoke(currentAction.Id);
        }
    }
}
