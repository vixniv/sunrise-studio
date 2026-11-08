using UnityEngine;

/// <summary>
/// Switches one room background sprite to match the current Yarn time slot.
/// </summary>
public class BackgroundCycleSwitcher2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Sprite dayBackground;
    [SerializeField] private Sprite nightBackground;

    private void OnEnable()
    {
        YarnCommands.TimeOfDayChanged += ApplyCurrentTime;
        ApplyCurrentTime();
    }

    private void OnDisable()
    {
        YarnCommands.TimeOfDayChanged -= ApplyCurrentTime;
    }

    private void ApplyCurrentTime()
    {
        if (backgroundRenderer == null)
            return;

        string timeSlot = Normalize(YarnCommands.TimeOfDay);
        bool isNight = timeSlot == "night";
        Sprite selectedBackground = isNight ? nightBackground : dayBackground;

        if (selectedBackground != null)
            backgroundRenderer.sprite = selectedBackground;
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }
}
