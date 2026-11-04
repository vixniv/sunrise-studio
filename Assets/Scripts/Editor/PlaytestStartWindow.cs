using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Tools > Playtest Start: picks the time of day and Yarn variables that Play Mode starts with (see PlaytestStart).
// Open the room to test, such as Cafeteria, set the time to lunch, and press Play.
public class PlaytestStartWindow : EditorWindow
{
    private const string NoChange = "(no change)";
    private static readonly List<string> Times = new List<string>
    {
        NoChange, YarnCommands.DefaultTimeOfDay, "lunch", "after_school", YarnCommands.GapTimeOfDay
    };

    [MenuItem("Tools/Playtest Start")]
    private static void Open() => GetWindow<PlaytestStartWindow>("Playtest Start");

    private void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.style.paddingLeft = root.style.paddingRight = root.style.paddingTop = 6;

        var enabled = new Toggle("Use when Play starts") { value = EditorPrefs.GetBool(PlaytestStart.EnabledKey, false) };
        enabled.RegisterValueChangedCallback(e => EditorPrefs.SetBool(PlaytestStart.EnabledKey, e.newValue));

        string savedTime = EditorPrefs.GetString(PlaytestStart.TimeKey, "");
        var time = new DropdownField("Time of day", Times, Mathf.Max(0, Times.IndexOf(savedTime)));
        time.RegisterValueChangedCallback(e => EditorPrefs.SetString(PlaytestStart.TimeKey, e.newValue == NoChange ? "" : e.newValue));

        var variables = new TextField("Yarn variables") { multiline = true, value = EditorPrefs.GetString(PlaytestStart.VariablesKey, "") };
        variables.style.minHeight = 90;
        variables.RegisterValueChangedCallback(e => EditorPrefs.SetString(PlaytestStart.VariablesKey, e.newValue));

        var help = new HelpBox(
            "One variable per line, such as:\n$chapter_scene = 1\n$anin_dressed = true\n" +
            "Open the scene to test and press Play. Turn the toggle off for normal play.",
            HelpBoxMessageType.Info);

        var applyNow = new Button(() => ApplyNow(time.value, variables.value)) { text = "Apply Now and Reload Scene" };
        applyNow.tooltip = "Play Mode only: switches to these values and reloads the open scene.";
        root.schedule.Execute(() => applyNow.SetEnabled(EditorApplication.isPlaying)).Every(250);

        root.Add(enabled);
        root.Add(time);
        root.Add(variables);
        root.Add(help);
        root.Add(applyNow);
    }

    // Reloads by path, so it works even when the scene is not in the Build Profile scene list.
    private static void ApplyNow(string time, string variables)
    {
        if (!EditorApplication.isPlaying) return;
        PlaytestStart.Apply(time == NoChange ? "" : time, variables);
        EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
    }
}
