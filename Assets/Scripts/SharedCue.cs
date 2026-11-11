using System.Collections.Generic;
using UnityEngine;

// Lets several interactables share one cue object, such as a single cue above the player's head.
// The cue shows while at least one of them wants it shown, so an object the player is not near cannot hide it.
public static class SharedCue
{
    private static readonly Dictionary<GameObject, int> requests = new Dictionary<GameObject, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => requests.Clear();

    // Hides the cue at startup unless another interactable is already showing it.
    public static void Init(GameObject cue)
    {
        if (cue != null && !requests.ContainsKey(cue)) cue.SetActive(false);
    }

    // shown is the caller's own state; only changes to it count.
    public static void Set(GameObject cue, ref bool shown, bool show)
    {
        if (cue == null || shown == show) return;
        shown = show;
        requests.TryGetValue(cue, out int count);
        count = Mathf.Max(0, count + (show ? 1 : -1));
        if (count == 0) requests.Remove(cue);
        else requests[cue] = count;
        cue.SetActive(count > 0);
    }
}
