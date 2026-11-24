using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Turns the window and lamp lights on for the matching Yarn time slots.
/// Place this on an always-active object in each scene that has these lights.
/// </summary>
public class CycleLightActivation2D : MonoBehaviour
{
    [System.Serializable]
    private class LightEntry
    {
        public Light2D light;
    }

    [Header("Lights to switch")]
    [Tooltip("Their GameObjects are active during morning, afternoon, and dream, and inactive otherwise.")]
    [SerializeField] private List<LightEntry> windowLights = new List<LightEntry>();
    [Tooltip("Their GameObjects are active during night / after school, and inactive otherwise.")]
    [SerializeField] private List<LightEntry> lampLights = new List<LightEntry>();

    [Tooltip("Use the afternoon window lights instead of lamps during Night in this scene only (for example, Rooftop's sunset story scene).")]
    [SerializeField] private bool useWindowLightsAtNight;

    [Tooltip("Turn off both legacy light groups during Night so separate sunset lights can light this scene. Takes precedence over Use Window Lights At Night.")]
    [SerializeField] private bool useCustomNightLights;

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
        bool useWindowLight;
        bool useLampLight;

        switch (slot)
        {
            case "morning":
            case "day":
            case "afternoon":
            case "lunch":
            case "afterschool":
            case "dream":
            case "dreamsequence":
            case "midnight":
                useWindowLight = true;
                useLampLight = false;
                break;

            case "night":
                useWindowLight = !useCustomNightLights && useWindowLightsAtNight;
                useLampLight = !useCustomNightLights && !useWindowLightsAtNight;
                break;

            default:
                Debug.LogWarning($"{name}: unknown time slot '{YarnCommands.TimeOfDay}'. Using the window light.", this);
                useWindowLight = true;
                useLampLight = false;
                break;
        }

        SetLightsActive(windowLights, useWindowLight);
        SetLightsActive(lampLights, useLampLight);
    }

    private static void SetLightsActive(List<LightEntry> lights, bool active)
    {
        foreach (LightEntry entry in lights)
        {
            if (entry == null || entry.light == null)
                continue;

            entry.light.gameObject.SetActive(active);
        }
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
