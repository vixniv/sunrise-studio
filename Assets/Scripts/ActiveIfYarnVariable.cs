using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Yarn.Unity;

// Shows the targets only when a Yarn variable has the given value when the scene starts; hides them otherwise.
// Example: the Observant Student is in both the cafeteria and the music room, and only appears in the one
// the player picked in the classroom ($clue_location is "cafeteria" or "music_room").
// Story stages: a room's "Story_3" object with $chapter_scene and "3" only shows during Scene 3 of the story.
// With Follow Changes on, it also shows or hides the targets as soon as dialogue changes the variable,
// such as Fal's locker contents appearing once $gap1_locker_open becomes true.
// Needs the Dialogue Runner to use a PersistentVariableStorage, so the value set in an earlier scene is still there.
// Keep this object active in the scene: an inactive object never runs the check.
public class ActiveIfYarnVariable : MonoBehaviour
{
    [Tooltip("Leave empty to use this scene's Dialogue Runner.")]
    [SerializeField] private DialogueRunner runner;
    [SerializeField] private string variable = "$clue_location";
    [Tooltip("Matched as text, ignoring case: \"cafeteria\", \"true\", \"3\".")]
    [SerializeField] private string value = "cafeteria";
    [Tooltip("Objects to show or hide. Leave empty to show or hide this object.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();
    [Tooltip("On: also shows or hides the targets when dialogue changes the variable in this scene. Off: only checks when the scene starts.")]
    [SerializeField] private bool followChanges;

    private string variableName;
    private IDisposable changeListener;

    private void Awake()
    {
        if (runner == null) runner = FindAnyObjectByType<DialogueRunner>();
        variableName = variable.Trim();
        if (!variableName.StartsWith("$")) variableName = "$" + variableName;

        if (followChanges && runner != null && runner.VariableStorage != null)
            changeListener = runner.VariableStorage.AddChangeListener(OnVariableChanged);
        Apply(Matches());
    }

    private void OnDestroy() => changeListener?.Dispose();

    private void OnVariableChanged(string changedName, object current)
    {
        if (changedName == variableName) Apply(Matches(current));
    }

    private void Apply(bool matches)
    {
        if (targets.Count == 0)
        {
            gameObject.SetActive(matches);
            return;
        }
        foreach (GameObject target in targets)
            if (target != null) target.SetActive(matches);
    }

    private bool Matches()
    {
        if (runner == null)
        {
            Debug.LogError($"{name}: no Dialogue Runner in the scene; hiding the targets.", this);
            return false;
        }

        object current;
        try
        {
            if (!runner.VariableStorage.TryGetValue(variableName, out current) || current == null)
            {
                Debug.LogWarning($"{name}: Yarn variable {variableName} is not declared; hiding the targets.", this);
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{name}: could not read {variableName}: {e.Message}", this);
            return false;
        }
        return Matches(current);
    }

    private bool Matches(object current)
    {
        string text = Convert.ToString(current, CultureInfo.InvariantCulture);
        bool matches = string.Equals(text, value.Trim(), StringComparison.OrdinalIgnoreCase);
        Debug.Log($"{name}: {variableName} is \"{text}\", needs \"{value.Trim()}\"; {(matches ? "showing" : "hiding")}.", this);
        return matches;
    }
}
