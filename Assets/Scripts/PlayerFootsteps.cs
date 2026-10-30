using UnityEngine;

// Plays a footstep each time the walk or run animation reaches a step point,
// so the step tempo always follows the animation playing.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("Floor sound set for this scene. Left empty, the player walks silently.")]
    public FootstepSurface surface;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Sync with walk animation")]
    [Tooltip("Animator playing the walk and run. Left empty, the Animator on the child named \"Visual\" is used.")]
    public Animator animator;
    [Tooltip("Name of the walk state in the Animator Controller.")]
    public string walkStateName = "anin-walk";
    [Tooltip("When a foot touches the floor, as a fraction of the walk loop (0 = first frame, 0.5 = halfway). " +
             "For an 8-frame walk, frame N is N / 8, e.g. frame 4 = 0.5.")]
    [Range(0f, 1f)] public float[] stepTimes = { 0f, 0.5f };
    [Tooltip("Shifts every step earlier (-) or later (+), as a fraction of the walk loop. " +
             "One frame of an 8-frame walk = 0.125.")]
    [Range(-0.5f, 0.5f)] public float timingOffset = 0f;

    [Header("Sync with run animation")]
    [Tooltip("Name of the run state in the Animator Controller.")]
    public string runStateName = "anin-run";
    [Tooltip("When a foot touches the floor, as a fraction of the run loop. " +
             "For a 10-frame run, frame N is N / 10, e.g. frame 5 = 0.5.")]
    [Range(0f, 1f)] public float[] runStepTimes = { 0f, 0.5f };
    [Tooltip("Shifts every run step earlier (-) or later (+), as a fraction of the run loop. " +
             "One frame of a 10-frame run = 0.1.")]
    [Range(-0.5f, 0.5f)] public float runTimingOffset = 0f;
    [Range(0.5f, 1.5f)] public float runVolumeScale = 1f;

    [Header("Walk / run switch")]
    [Tooltip("Shortest time between two steps, in seconds. Stops a double step when switching between walk and run.")]
    [Min(0f)] public float minStepGap = 0.15f;

    AudioSource source;
    int walkStateHash;
    int runStateHash;
    int lastStateHash; // 0 = not walking or running
    float lastTime;
    float lastStepAt = -1f;

    void Reset()
    {
        var src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D
    }

    void Awake()
    {
        source = GetComponent<AudioSource>();
        if (animator == null)
        {
            Transform child = transform.Find("Visual");
            if (child != null) animator = child.GetComponent<Animator>();
        }
        HashStateNames();
    }

    void OnValidate()
    {
        HashStateNames();
    }

    void HashStateNames()
    {
        walkStateHash = Animator.StringToHash(walkStateName);
        runStateHash = Animator.StringToHash(runStateName);
    }

    void LateUpdate()
    {
        if (animator == null || surface == null) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = state.shortNameHash;
        bool running = stateHash == runStateHash;
        if (!running && stateHash != walkStateHash)
        {
            lastStateHash = 0;
            return;
        }

        // normalizedTime keeps counting up across loops (2.25 = third loop, a quarter in).
        float time = state.normalizedTime;
        if (stateHash != lastStateHash)
        {
            // Just started walking or running: a step right at the start still plays.
            lastTime = Mathf.Floor(time) - 0.0001f;
            lastStateHash = stateHash;
        }

        float[] times = running ? runStepTimes : stepTimes;
        float offset = running ? runTimingOffset : timingOffset;
        if (HasStepBetween(lastTime, time, times, offset)) PlayStep(running ? runVolumeScale : 1f);
        lastTime = time;
    }

    static bool HasStepBetween(float from, float to, float[] times, float offset)
    {
        if (to <= from) return false;
        for (int loop = Mathf.FloorToInt(from) - 1; loop <= Mathf.FloorToInt(to) + 1; loop++)
        {
            foreach (float t in times)
            {
                float stepAt = loop + t + offset;
                if (stepAt > from && stepAt <= to) return true;
            }
        }
        return false;
    }

    void PlayStep(float volumeScale)
    {
        if (Time.time - lastStepAt < minStepGap) return;

        AudioClip clip = surface.PickClip();
        if (clip == null) return;

        lastStepAt = Time.time;
        source.pitch = Random.Range(surface.minPitch, surface.maxPitch);
        source.PlayOneShot(clip, volume * volumeScale * Random.Range(surface.minVolume, surface.maxVolume));
    }
}
