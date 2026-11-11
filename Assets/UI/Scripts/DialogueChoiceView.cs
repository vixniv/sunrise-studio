using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class DialogueChoiceView : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Button button;

        DialogueChoice choice;
        Action<DialogueChoice> onChosen;

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

        public void Bind(DialogueChoice newChoice, Action<DialogueChoice> chosenCallback)
        {
            choice = newChoice;
            onChosen = chosenCallback;

            if (label != null)
                label.text = choice != null ? choice.Label : string.Empty;
            if (button != null)
                button.interactable = choice != null;
        }

        void HandleClicked()
        {
            if (choice != null)
                onChosen?.Invoke(choice);
        }
    }
}
