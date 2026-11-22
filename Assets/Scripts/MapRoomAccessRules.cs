using System;
using SunriseStudio.UI;
using UnityEngine;

public static class MapRoomAccessRules
{
    private static bool IsObjective(string objective) =>
        string.Equals(QuestTracker.QuestId, "loop1", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(QuestTracker.Objective, objective, StringComparison.OrdinalIgnoreCase);

    public static string BlockedLine(string destinationScene)
    {
        if (IsObjective("Find Kirana at the cafeteria") &&
            !string.Equals(destinationScene?.Trim(), "Cafeteria", StringComparison.OrdinalIgnoreCase))
            return "I should find Kirana at the cafeteria first.";

        if (IsObjective("Find Dira at the auditorium") &&
            !string.Equals(destinationScene?.Trim(), "Auditorium", StringComparison.OrdinalIgnoreCase))
            return "I should check the auditorium for Dira first.";

        return null;
    }

    public static bool TryShowBlockedLine(string destinationScene, Transform mapPlayer)
    {
        string line = BlockedLine(destinationScene);
        if (line == null) return false;

        YarnDialogPresenter dialogue = UnityEngine.Object.FindAnyObjectByType<YarnDialogPresenter>();
        if (dialogue != null && !dialogue.IsPlaying)
        {
            DialogueHudView hud = UnityEngine.Object.FindAnyObjectByType<DialogueHudView>();
            if (hud != null) hud.SetSplashPresentation(true);
            dialogue.PlayLines(new[] { line }, mapPlayer, new Vector2(0f, 0.25f), null,
                () => { if (hud != null) hud.SetSplashPresentation(false); });
        }
        else
            SunriseAudio.Play("ui_error");

        return true;
    }
}
