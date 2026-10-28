using UnityEngine;

// Plays a footstep each time the walk animation reaches a step point,
// so steps stay in sync when the walk speeds up for running.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("Floor sound set for this scene. Left empty, the player walks silently.")]
    public FootstepSurface surface;
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Sync with walk animation")]
    [Tooltip("Animator playing the walk. Left empty, the Animator on the child named \"Visual\" is used.")]
    public Animator animator;
    [Tooltip("Name of the walk state in the Animator Controller.")]
    public string walkStateName = "anin-walk";
    [Tooltip("When a foot touches the floor, as a fraction of the walk loop (0 = first frame, 0.5 = halfway). " +
             "For an 8-frame walk, frame N is N / 8, e.g. frame 4 = 0.5.")]
    [Range(0f, 1f)] public float[] stepTimes = { 0f, 0.5f };
    [Tooltip("Shifts every step earlier (-) or later (+), as a fraction of the walk loop. " +
             "One frame of an 8-frame walk = 0.125.")]
    [Range(-0.5f, 0.5f)] public float timingOffset = 0f;

    AudioSource source;
    int walkStateHash;
    bool wasWalking;
    float lastTime;

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
        walkStateHash = Animator.StringToHash(walkStateName);
    }

    void OnValidate()
    {
        walkStateHash = Animator.StringToHash(walkStateName);
    }

    void LateUpdate()
    {
        if (animator == null || surface == null) return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool walking = state.shortNameHash == walkStateHash;
        if (!walking)
        {
            wasWalking = false;
            return;
        }

        // normalizedTime keeps counting up across loops (2.25 = third loop, a quarter in).
        float time = state.normalizedTime;
        if (!wasWalking)
        {
            // Just started walking: a step right at the start still plays.
            lastTime = Mathf.Floor(time) - 0.0001f;
            wasWalking = true;
        }

        if (HasStepBetween(lastTime, time)) PlayStep();
        lastTime = time;
    }

    bool HasStepBetween(float from, float to)
    {
        if (to <= from) return false;
        for (int loop = Mathf.FloorToInt(from) - 1; loop <= Mathf.FloorToInt(to) + 1; loop++)
        {
            foreach (float t in stepTimes)
            {
                float stepAt = loop + t + timingOffset;
                if (stepAt > from && stepAt <= to) return true;
            }
        }
        return false;
    }

    void PlayStep()
    {
        AudioClip clip = surface.PickClip();
        if (clip == null) return;

        source.pitch = Random.Range(surface.minPitch, surface.maxPitch);
        source.PlayOneShot(clip, volume * Random.Range(surface.minVolume, surface.maxVolume));
    }
}
