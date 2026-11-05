using System;
using UnityEngine;

namespace SunriseStudio.UI
{
    [CreateAssetMenu(
        fileName = "Quest",
        menuName = "Sunrise Studio/UI Data/Quest")]
    public sealed class QuestData : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string title;
        [TextArea(2, 6)] [SerializeField] string description;
        public string Id => id;
        public string Title => title;
        public string Description => description;
    }

    [Serializable]
    public sealed class QuestState
    {
        [SerializeField] QuestData quest;
        [SerializeField] bool completed;
        [SerializeField] bool tracked;

        public QuestData Quest => quest;
        public bool Completed => completed;
        public bool Tracked => tracked;

        public QuestState(QuestData quest, bool completed, bool tracked)
        {
            this.quest = quest;
            this.completed = completed;
            this.tracked = tracked;
        }
    }
}
