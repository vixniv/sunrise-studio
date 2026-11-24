using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Yarn.Unity;

// Fills a room with background students only at certain times of day and story scenes, so rooms feel lived in.
// Put it on an empty group object (e.g. "Crowd_Lunch") whose children are the NPCs for that moment.
// The group shows its NPCs only when BOTH match:
//   Times           - time slots set by <<advance_time>>: morning, lunch, after_school, midnight. Empty = any time.
//   Chapter Scenes  - values of $chapter_scene, the story scene. Empty = any story scene.
// Example: "Crowd_Morning" with Times [morning] and Chapter Scenes [1, 2, 3] fills the classroom
// every morning of the first three story scenes, then the room is empty from scene 4 on.
// This object stays active and only shows or hides its targets, so it keeps hearing time and story changes.
// Needs the Dialogue Runner to use a PersistentVariableStorage when Chapter Scenes is filled.
public class NpcSchedule : MonoBehaviour
{
    private const string ChapterSceneVariable = "$chapter_scene";

    [Tooltip("Time slots the NPCs are here: morning, lunch, after_school, midnight. Case, spaces and underscores are ignored. Empty = any time.")]
    [SerializeField] private List<string> times = new List<string> { "morning" };
    [Tooltip("Story scenes ($chapter_scene values) the NPCs are here. Empty = every story scene.")]
    [SerializeField] private List<int> chapterScenes = new List<int>();
    [Tooltip("NPCs to show or hide. Leave empty to use every direct child of this object.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();
    [Tooltip("On: NPCs appear or leave when <<advance_time>> runs in this room (while the screen is covered) or when dialogue changes $chapter_scene. Off: only checked when the room loads.")]
    [SerializeField] private bool followChanges = true;
    [Tooltip("Leave empty to use this scene's Dialogue Runner. Only needed when Chapter Scenes is filled.")]
    [SerializeField] private DialogueRunner runner;

    private IDisposable changeListener;

    private void Awake()
    {
        if (targets.Count == 0)
            foreach (Transform child in transform)
                targets.Add(child.gameObject);

        if (chapterScenes.Count > 0 && runner == null)
            runner = FindAnyObjectByType<DialogueRunner>();

        if (followChanges)
        {
            YarnCommands.TimeOfDayChanged += Apply;
            if (chapterScenes.Count > 0 && runner != null && runner.VariableStorage != null)
                changeListener = runner.VariableStorage.AddChangeListener(OnVariableChanged);
        }

        Apply();
    }

    private void OnDestroy()
    {
        YarnCommands.TimeOfDayChanged -= Apply;
        changeListener?.Dispose();
    }

    private void OnVariableChanged(string changedName, object current)
    {
        if (changedName == ChapterSceneVariable) Apply();
    }

    private void Apply()
    {
        bool here = TimeMatches() && ChapterSceneMatches();
        foreach (GameObject target in targets)
            if (target != null && target != gameObject) target.SetActive(here);
    }

    private bool TimeMatches() => times.Count == 0 || times.Exists(YarnCommands.IsTimeOfDay);

    private bool ChapterSceneMatches()
    {
        if (chapterScenes.Count == 0) return true;
        if (runner == null || runner.VariableStorage == null)
        {
            Debug.LogWarning($"{name}: no Dialogue Runner to read {ChapterSceneVariable}; hiding the NPCs.", this);
            return false;
        }

        try
        {
            if (!runner.VariableStorage.TryGetValue(ChapterSceneVariable, out object current) || current == null)
                return false;
            int scene = (int)Math.Round(Convert.ToDouble(current, CultureInfo.InvariantCulture));
            return chapterScenes.Contains(scene);
        }
        catch (Exception e)
        {
            Debug.LogError($"{name}: could not read {ChapterSceneVariable}: {e.Message}", this);
            return false;
        }
    }
}
