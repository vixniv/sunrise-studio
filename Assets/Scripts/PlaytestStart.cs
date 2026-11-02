#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// Editor only: lets Play Mode start at another time of day, or with Yarn variables already set, so a later part
// of the story can be tested without playing up to it. Set it up in Tools > Playtest Start.
// The settings are saved on this computer only (EditorPrefs), never in the project or a build.
public static class PlaytestStart
{
    public const string EnabledKey = "SunriseStudio.PlaytestStart.Enabled";
    public const string TimeKey = "SunriseStudio.PlaytestStart.Time";
    public const string VariablesKey = "SunriseStudio.PlaytestStart.Variables";

    // Runs after YarnCommands and PersistentVariableStorage clear their values on Play, and before the first scene's Awake,
    // so ActiveDuringTime and ActiveIfYarnVariable already see the overrides.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnPlay()
    {
        if (!EditorPrefs.GetBool(EnabledKey, false)) return;
        Apply(EditorPrefs.GetString(TimeKey, ""), EditorPrefs.GetString(VariablesKey, ""));
    }

    // time: a slot such as "lunch", or empty to keep the current one.
    // variables: one "$name = value" per line; lines starting with // are skipped.
    public static void Apply(string time, string variables)
    {
        var applied = new List<string>();
        if (!string.IsNullOrWhiteSpace(time))
        {
            YarnCommands.SetTimeOfDayForTesting(time);
            applied.Add($"time of day {time.Trim()}");
        }

        foreach (string raw in (variables ?? "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            int equals = line.IndexOf('=');
            if (equals <= 0)
            {
                Debug.LogWarning($"Playtest Start: skipped \"{line}\". Write it as $name = value.");
                continue;
            }
            string name = line.Substring(0, equals).Trim();
            if (!name.StartsWith("$")) name = "$" + name;
            object value = ParseValue(line.Substring(equals + 1));
            PersistentVariableStorage.SetValueForTesting(name, value);
            applied.Add($"{name} = {value}");
        }

        if (applied.Count > 0)
            Debug.LogWarning($"Playtest Start is on: {string.Join(", ", applied)}. Turn it off in Tools > Playtest Start.");
    }

    // true/false become bools, numbers become floats (Yarn numbers), anything else a string; quotes are optional.
    private static object ParseValue(string text)
    {
        text = text.Trim();
        if (bool.TryParse(text, out bool flag)) return flag;
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)) return number;
        if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"') return text.Substring(1, text.Length - 2);
        return text;
    }
}
#endif
