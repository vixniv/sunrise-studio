using System;
using UnityEngine;

namespace SunriseStudio.UI
{
    /// <summary>
    /// Keeps the one objective shown in the gameplay HUD.
    /// YarnCommands updates this state; QuestTrackerView displays it.
    /// </summary>
    public static class QuestTracker
    {
        public static event Action Changed;

        public static string QuestId { get; private set; } = string.Empty;
        public static string Objective { get; private set; } = string.Empty;

        public static bool HasActiveQuest =>
            !string.IsNullOrWhiteSpace(QuestId) &&
            !string.IsNullOrWhiteSpace(Objective);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            QuestId = string.Empty;
            Objective = string.Empty;
            Changed = null;
        }

        public static void StartQuest(string questId, string objective)
        {
            SetCurrent(questId, objective);
        }

        public static void UpdateQuest(string questId, string objective)
        {
            SetCurrent(questId, objective);
        }

        public static void CompleteQuest(string questId)
        {
            if (!string.Equals(QuestId, Clean(questId), StringComparison.OrdinalIgnoreCase))
                return;

            ClearQuest();
        }

        public static void ClearQuest()
        {
            if (QuestId.Length == 0 && Objective.Length == 0)
                return;

            QuestId = string.Empty;
            Objective = string.Empty;
            Changed?.Invoke();
        }

        private static void SetCurrent(string questId, string objective)
        {
            QuestId = Clean(questId);
            Objective = Clean(objective);
            Changed?.Invoke();
        }

        private static string Clean(string value) => value?.Trim() ?? string.Empty;
    }
}
