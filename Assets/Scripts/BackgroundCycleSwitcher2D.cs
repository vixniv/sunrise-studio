using UnityEngine;

/// <summary>
/// Switches one room background sprite to match the current Yarn time slot,
/// and shows objects that only belong to the day art (e.g. open counters and staff).
/// </summary>
public class BackgroundCycleSwitcher2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Sprite dayBackground;
    [SerializeField] private Sprite nightBackground;

    [Tooltip("Active during the day, hidden at night (e.g. counter overlays and the staff behind them, which the night art covers with shutters).")]
    [SerializeField] private GameObject[] dayOnlyObjects;

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
        string timeSlot = Normalize(YarnCommands.TimeOfDay);
        bool isNight = timeSlot == "night";

        if (dayOnlyObjects != null)
        {
            foreach (GameObject dayOnlyObject in dayOnlyObjects)
            {
                if (dayOnlyObject != null)
                    dayOnlyObject.SetActive(!isNight);
            }
        }

        if (backgroundRenderer == null)
            return;

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
