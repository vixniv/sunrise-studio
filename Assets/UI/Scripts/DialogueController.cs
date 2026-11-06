using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class DialogueController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] GameObject dialogueRoot;
        [SerializeField] TMP_Text speakerLabel;
        [SerializeField] TMP_Text bodyLabel;
        [SerializeField] Button continueButton;
        [SerializeField] GameObject continueIndicator;
        [SerializeField] RectTransform choicesRoot;
        [SerializeField] DialogueChoiceView choicePrefab;

        [Header("Events")]
        [SerializeField] StringEvent onChoiceSelected = new();
        [SerializeField] UnityEvent onDialogueCompleted = new();

        readonly List<DialogueChoiceView> choiceViews = new();
        DialogueData dialogue;
        DialogueNode currentNode;

        void OnEnable()
        {
            if (continueButton != null)
                continueButton.onClick.AddListener(Continue);
        }

        void OnDisable()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(Continue);
        }

        public void Begin(DialogueData newDialogue)
        {
            dialogue = newDialogue;

            if (dialogue == null ||
                !dialogue.TryGetNode(dialogue.FirstNodeId, out DialogueNode firstNode))
            {
                Debug.LogWarning("Dialogue has no valid first node.", this);
                Complete();
                return;
            }

            if (dialogueRoot != null)
                dialogueRoot.SetActive(true);

            ShowNode(firstNode);
        }

        public void Continue()
        {
            if (currentNode == null || currentNode.Choices.Count > 0)
                return;

            if (string.IsNullOrWhiteSpace(currentNode.NextNodeId))
            {
                Complete();
                return;
            }

            NavigateTo(currentNode.NextNodeId);
        }

        public void Complete()
        {
            ClearChoices();
            dialogue = null;
            currentNode = null;

            if (dialogueRoot != null)
                dialogueRoot.SetActive(false);

            onDialogueCompleted.Invoke();
        }

        void NavigateTo(string nodeId)
        {
            if (dialogue != null && dialogue.TryGetNode(nodeId, out DialogueNode node))
            {
                ShowNode(node);
                return;
            }

            Debug.LogWarning($"Dialogue node '{nodeId}' was not found.", this);
            Complete();
        }

        void ShowNode(DialogueNode node)
        {
            currentNode = node;

            if (speakerLabel != null)
                speakerLabel.text = node.Speaker;
            if (bodyLabel != null)
                bodyLabel.text = node.Body;

            ClearChoices();
            bool hasChoices = node.Choices.Count > 0;

            if (continueButton != null)
                continueButton.gameObject.SetActive(!hasChoices);
            if (continueIndicator != null)
                continueIndicator.SetActive(!hasChoices);

            if (!hasChoices)
                return;

            if (choicePrefab == null || choicesRoot == null)
            {
                Debug.LogWarning("Dialogue choices need a root and choice prefab.", this);
                return;
            }

            foreach (DialogueChoice choice in node.Choices)
            {
                DialogueChoiceView view = Instantiate(choicePrefab, choicesRoot);
                view.Bind(choice, HandleChoiceSelected);
                choiceViews.Add(view);
            }
        }

        void HandleChoiceSelected(DialogueChoice choice)
        {
            onChoiceSelected.Invoke(choice.Id);

            if (string.IsNullOrWhiteSpace(choice.NextNodeId))
                Complete();
            else
                NavigateTo(choice.NextNodeId);
        }

        void ClearChoices()
        {
            foreach (DialogueChoiceView view in choiceViews)
            {
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                }
            }

            choiceViews.Clear();
        }
    }
}
