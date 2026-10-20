using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Yarn.Unity;

// Shows the targets only when a Yarn variable has the given value when the scene starts; hides them otherwise.
// Example: the Observant Student is in both the cafeteria and the music room, and only appears in the one
// the player picked in the classroom ($clue_location is "cafetaria" or "music_room").
// Needs the Dialogue Runner to use a PersistentVariableStorage, so the value set in an earlier scene is still there.
// Keep this object active in the scene: an inactive object never runs the check.
public class ActiveIfYarnVariable : MonoBehaviour
{
    [Tooltip("Leave empty to use this scene's Dialogue Runner.")]
    [SerializeField] private DialogueRunner runner;
    [SerializeField] private string variable = "$clue_location";
    [Tooltip("Matched as text, ignoring case: \"cafetaria\", \"true\", \"3\".")]
    [SerializeField] private string value = "cafetaria";
    [Tooltip("Objects to show or hide. Leave empty to show or hide this object.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();

    private void Awake()
    {
        if (runner == null) runner = FindAnyObjectByType<DialogueRunner>();
        bool matches = Matches();
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
        string variableName = variable.Trim();
        if (!variableName.StartsWith("$")) variableName = "$" + variableName;

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

        string text = Convert.ToString(current, CultureInfo.InvariantCulture);
        bool matches = string.Equals(text, value.Trim(), StringComparison.OrdinalIgnoreCase);
        Debug.Log($"{name}: {variableName} is \"{text}\", needs \"{value.Trim()}\"; {(matches ? "showing" : "hiding")}.", this);
        return matches;
    }
}
