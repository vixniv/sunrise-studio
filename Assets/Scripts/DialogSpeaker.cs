using System.Collections.Generic;
using UnityEngine;

// Marks a character that can talk in Yarn dialogue. A line written as "Kirana: Hello." shows its bubble
// above the enabled DialogSpeaker whose Character Name is "Kirana".
public class DialogSpeaker : MonoBehaviour
{
    private static readonly Dictionary<string, DialogSpeaker> active = new Dictionary<string, DialogSpeaker>();

    [Tooltip("Must match the name before the colon in the Yarn lines exactly (case-sensitive).")]
    [SerializeField] private string characterName = "Kirana";
    [Tooltip("World offset from the top of this object's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);

    public Vector2 DialogOffset => dialogOffset;

    public static bool TryFind(string name, out DialogSpeaker speaker)
    {
        speaker = null;
        return !string.IsNullOrEmpty(name) && active.TryGetValue(name, out speaker) && speaker != null;
    }

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(characterName)) return;
        if (active.TryGetValue(characterName, out DialogSpeaker other) && other != null && other != this)
            Debug.LogWarning($"Two DialogSpeakers are named \"{characterName}\"; lines go to the last one enabled.", this);
        active[characterName] = this;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(characterName) && active.TryGetValue(characterName, out DialogSpeaker current) && current == this)
            active.Remove(characterName);
    }
}
