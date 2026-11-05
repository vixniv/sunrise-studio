using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SunriseStudio.UI
{
    public sealed class QuestLogController : MonoBehaviour
    {
        [SerializeField] RectTransform entriesRoot;
        [SerializeField] QuestEntryView entryPrefab;
        [SerializeField] GameObject emptyState;
        [SerializeField] Button backButton;
        [SerializeField] StringEvent onQuestSelected = new();
        [SerializeField] UnityEvent onBackRequested = new();

        readonly List<QuestEntryView> entries = new();

        void OnEnable()
        {
            if (backButton != null)
                backButton.onClick.AddListener(HandleBackClicked);
        }

        void OnDisable()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(HandleBackClicked);
        }

        public void SetQuests(IEnumerable<QuestState> quests)
        {
            ClearEntries();

            if (quests != null && entriesRoot != null && entryPrefab != null)
            {
                foreach (QuestState quest in quests)
                {
                    if (quest?.Quest == null)
                        continue;

                    QuestEntryView entry = Instantiate(entryPrefab, entriesRoot);
                    entry.Bind(quest, onQuestSelected);
                    entries.Add(entry);
                }
            }

            if (emptyState != null)
                emptyState.SetActive(entries.Count == 0);
        }

        void ClearEntries()
        {
            foreach (QuestEntryView entry in entries)
            {
                if (entry != null)
                {
                    entry.gameObject.SetActive(false);
                    Destroy(entry.gameObject);
                }
            }

            entries.Clear();
        }

        void HandleBackClicked() => onBackRequested.Invoke();
    }
}
