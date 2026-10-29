using System.Collections.Generic;
using UnityEngine;

// Shows the targets only during the given times of day (set by <<advance_time>>); hides them otherwise.
// Example: the friends at the Warmus stall only stand in the Cafeteria at "lunch";
// the Locker Room's YarnCutscene only plays "after_school".
// Time names ignore case, spaces, and underscores. The day starts at "morning".
// Keep this object active in the scene when Targets is filled: an inactive object never runs the check.
public class ActiveDuringTime : MonoBehaviour
{
    [Tooltip("Times of day the targets are shown: morning, lunch, after_school.")]
    [SerializeField] private List<string> times = new List<string> { "morning" };
    [Tooltip("Objects to show or hide. Leave empty to show or hide this object.")]
    [SerializeField] private List<GameObject> targets = new List<GameObject>();
    [Tooltip("On: also shows or hides the targets when <<advance_time>> runs in this scene. Off: only checks when the scene starts, so <<show>> and <<hide>> decide after that.")]
    [SerializeField] private bool followTimeChanges;

    private void Awake()
    {
        if (followTimeChanges) YarnCommands.TimeOfDayChanged += Apply;
        Apply();
    }

    private void OnDestroy() => YarnCommands.TimeOfDayChanged -= Apply;

    private void Apply()
    {
        bool matches = times.Exists(YarnCommands.IsTimeOfDay);
        if (targets.Count == 0)
        {
            gameObject.SetActive(matches);
            return;
        }
        foreach (GameObject target in targets)
            if (target != null) target.SetActive(matches);
    }
}
