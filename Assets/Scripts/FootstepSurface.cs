using UnityEngine;

// One floor type (Wood, Concrete...). Each step plays a random clip from the set.
[CreateAssetMenu(fileName = "FootstepSurface", menuName = "Sunrise/Footstep Surface")]
public class FootstepSurface : ScriptableObject
{
    public AudioClip[] clips;

    [Header("Variation")]
    [Range(0f, 1f)] public float minVolume = 0.8f;
    [Range(0f, 1f)] public float maxVolume = 1f;
    [Range(0.5f, 1.5f)] public float minPitch = 0.92f;
    [Range(0.5f, 1.5f)] public float maxPitch = 1.08f;

    int lastIndex = -1;

    // Random clip, never the same one twice in a row.
    public AudioClip PickClip()
    {
        if (clips == null || clips.Length == 0) return null;
        if (clips.Length == 1) return clips[0];

        int index;
        if (lastIndex < 0 || lastIndex >= clips.Length)
            index = Random.Range(0, clips.Length);
        else
        {
            index = Random.Range(0, clips.Length - 1);
            if (index >= lastIndex) index++;
        }
        lastIndex = index;
        return clips[index];
    }
}
