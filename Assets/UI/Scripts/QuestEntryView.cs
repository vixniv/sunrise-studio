using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class QuestEntryView : MonoBehaviour
    {
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text descriptionLabel;
        [SerializeField] GameObject completedIndicator;
        [SerializeField] GameObject trackedIndicator;
        [SerializeField] Button button;

        string questId;
        StringEvent selectedEvent;

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

        public void Bind(QuestState state, StringEvent onSelected)
        {
            selectedEvent = onSelected;
            QuestData quest = state?.Quest;
            questId = quest != null ? quest.Id : string.Empty;

            if (titleLabel != null)
                titleLabel.text = quest != null ? quest.Title : string.Empty;
            if (descriptionLabel != null)
                descriptionLabel.text = quest != null ? quest.Description : string.Empty;
            if (completedIndicator != null)
                completedIndicator.SetActive(state != null && state.Completed);
            if (trackedIndicator != null)
                trackedIndicator.SetActive(state != null && state.Tracked);
            if (button != null)
                button.interactable = quest != null;
        }

        void HandleClicked()
        {
            if (!string.IsNullOrWhiteSpace(questId))
                selectedEvent?.Invoke(questId);
        }
    }
}
