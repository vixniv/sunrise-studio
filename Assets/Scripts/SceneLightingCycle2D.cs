using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Applies the current Yarn time slot to this scene's lighting Volumes.
/// Add one instance to each scene and assign that scene's Volumes.
/// </summary>
public class SceneLightingCycle2D : MonoBehaviour
{
    [Header("Volume references")]
    [Tooltip("Assign one Global Volume per look. Each Volume should have its own Profile. The camera must have Render Post-processing enabled.")]
    [SerializeField] private Volume dayVolume;
    [SerializeField] private Volume afternoonVolume;
    [SerializeField] private Volume nightVolume;
    [SerializeField] private Volume dreamVolume;

    [Header("Dream effect")]
    [Tooltip("The DreamWiggle material used by the 2D Renderer Full Screen Pass.")]
    [SerializeField] private Material dreamWiggleMaterial;
    [SerializeField, Min(0f)] private float dreamWiggleStrength = 0.003f;

    private static readonly int WiggleStrengthProperty = Shader.PropertyToID("_Strength");

    private void OnEnable()
    {
        YarnCommands.TimeOfDayChanged += ApplyCurrentTime;
        ApplyCurrentTime();
    }

    private void OnDisable()
    {
        YarnCommands.TimeOfDayChanged -= ApplyCurrentTime;
    }

    private void ApplyCurrentTime()
    {
        string slot = Normalize(YarnCommands.TimeOfDay);
        bool isDreamSequence = slot == "dream" || slot == "dreamsequence";
        Volume activeVolume;

        switch (slot)
        {
            case "day":
            case "morning":
                activeVolume = dayVolume;
                break;

            case "afternoon":
            case "lunch":
                activeVolume = afternoonVolume;
                break;

            case "night":
            case "afterschool":
                activeVolume = nightVolume;
                break;

            case "dream":
            case "dreamsequence":
                activeVolume = dreamVolume;
                break;

            default:
                Debug.LogWarning($"{name}: unknown time slot '{YarnCommands.TimeOfDay}'. Using the Day look.", this);
                activeVolume = dayVolume;
                break;
        }

        SetWeight(dayVolume, activeVolume == dayVolume ? 1f : 0f);
        SetWeight(afternoonVolume, activeVolume == afternoonVolume ? 1f : 0f);
        SetWeight(nightVolume, activeVolume == nightVolume ? 1f : 0f);
        SetWeight(dreamVolume, activeVolume == dreamVolume ? 1f : 0f);

        if (dreamWiggleMaterial != null)
            dreamWiggleMaterial.SetFloat(WiggleStrengthProperty, isDreamSequence ? dreamWiggleStrength : 0f);
    }

    private static void SetWeight(Volume volume, float weight)
    {
        if (volume != null)
            volume.weight = weight;
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }
}
