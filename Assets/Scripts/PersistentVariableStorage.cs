using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Yarn;
using Yarn.Unity;

// Yarn variable storage that survives scene loads. Every scene's Dialogue Runner gets its own copy of this component,
// but they all share one set of values, so a choice made in one scene (such as $clue_location) is still there in the next.
// Values last until the game quits. Assign it to the Dialogue Runner's Variable Storage field in every scene;
// otherwise the runner makes its own In Memory Variable Storage, which starts over in each scene.
public class PersistentVariableStorage : VariableStorageBehaviour
{
    private static readonly Dictionary<string, object> variables = new Dictionary<string, object>();

    // Clears the values when Play Mode starts, even with Domain Reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => variables.Clear();

    public override void SetValue(string variableName, string stringValue) => Store(variableName, stringValue);
    public override void SetValue(string variableName, float floatValue) => Store(variableName, floatValue);
    public override void SetValue(string variableName, bool boolValue) => Store(variableName, boolValue);

    private void Store<T>(string variableName, T value)
    {
        Validate(variableName);
        variables[variableName] = value;
        NotifyVariableChanged(variableName, value);
    }

    public override bool TryGetValue<T>(string variableName, [NotNullWhen(true)] out T result)
    {
        Validate(variableName);
        switch (GetVariableKind(variableName))
        {
            case VariableKind.Stored:
                if (variables.TryGetValue(variableName, out object stored) && stored is T typed)
                {
                    result = typed;
                    return true;
                }
                // Never set yet: use the value from its <<declare>>.
                if (Program == null)
                    throw new InvalidOperationException($"Can't get the initial value of {variableName}: {nameof(Program)} is not set.");
                return Program.TryGetInitialValue(variableName, out result);
            case VariableKind.Smart:
                if (SmartVariableEvaluator == null)
                    throw new InvalidOperationException($"Can't get smart variable {variableName}: {nameof(SmartVariableEvaluator)} is not set.");
                return SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);
            default:
                result = default;
                return false;
        }
    }

    public override bool Contains(string variableName) => variables.ContainsKey(variableName);

    public override void Clear() => variables.Clear();

    public override (Dictionary<string, float>, Dictionary<string, string>, Dictionary<string, bool>) GetAllVariables()
    {
        var floats = new Dictionary<string, float>();
        var strings = new Dictionary<string, string>();
        var bools = new Dictionary<string, bool>();
        foreach (KeyValuePair<string, object> variable in variables)
        {
            switch (variable.Value)
            {
                case float f: floats[variable.Key] = f; break;
                case string s: strings[variable.Key] = s; break;
                case bool b: bools[variable.Key] = b; break;
            }
        }
        return (floats, strings, bools);
    }

    public override void SetAllVariables(Dictionary<string, float> floats, Dictionary<string, string> strings, Dictionary<string, bool> bools, bool clear = true)
    {
        if (clear) variables.Clear();
        foreach (KeyValuePair<string, float> variable in floats) SetValue(variable.Key, variable.Value);
        foreach (KeyValuePair<string, string> variable in strings) SetValue(variable.Key, variable.Value);
        foreach (KeyValuePair<string, bool> variable in bools) SetValue(variable.Key, variable.Value);
    }

    private static void Validate(string variableName)
    {
        if (!variableName.StartsWith("$"))
            throw new ArgumentException($"{variableName} is not a valid variable name: variable names start with a '$'.");
    }
}
