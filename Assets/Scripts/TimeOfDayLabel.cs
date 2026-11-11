using System.Globalization;
using TMPro;
using UnityEngine;

// Shows the current time of day ("Morning", "Lunch", "After School") in a TextMeshPro text, such as the HUD's loop badge.
// Updates when the scene starts and whenever <<advance_time>> or <<hard_reset>> changes the time.
public class TimeOfDayLabel : MonoBehaviour
{
    [Tooltip("Leave empty to use the text on this object.")]
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) Debug.LogError($"{name} has no TextMeshPro text assigned.", this);
    }

    private void OnEnable()
    {
        YarnCommands.TimeOfDayChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => YarnCommands.TimeOfDayChanged -= Refresh;

    private void Refresh()
    {
        if (label != null) label.text = DisplayName(YarnCommands.TimeOfDay);
    }

    // "after_school" becomes "After School".
    private static string DisplayName(string slot)
    {
        string words = slot.Replace("_", " ").Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
    }
}
