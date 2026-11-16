using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

// Swaps a SpriteRenderer to another sprite during certain story moments, and back to its own sprite otherwise.
// Example: the Dorm Foreground shows dorm-foreground-kirana-sleep while Kirana sleeps in her bed:
// Scene 3 morning, Scene 5 morning, and Scene 7 at night (the Dorm is reached "after_school" that day).
//
// A rule matches when $chapter_scene equals its Chapter Scene and the time of day (set by <<advance_time>>)
// is one of its Times. Leave Times empty to match the whole chapter scene.
// The renderer keeps its material, sorting and lights; only the sprite changes.
// Put it on the object with the SpriteRenderer (or assign Target), and keep that object active.
public class StorySpriteSwap : MonoBehaviour
{
    [Serializable]
    public class Rule
    {
        [Tooltip("Value of $chapter_scene this rule is for, e.g. 3.")]
        public int chapterScene = 3;
        [Tooltip("Times of day it applies: morning, lunch, after_school, midnight. Empty: any time.")]
        public List<string> times = new List<string> { "morning" };
    }

    [Tooltip("Renderer to swap. Left empty, the SpriteRenderer on this object is used.")]
    [SerializeField] private SpriteRenderer target;
    [Tooltip("Sprite shown while a rule matches, e.g. dorm-foreground-kirana-sleep.")]
    [SerializeField] private Sprite swapSprite;
    [Tooltip("Yarn number variable that holds the current chapter scene.")]
    [SerializeField] private string chapterSceneVariable = "$chapter_scene";
    [SerializeField] private List<Rule> rules = new List<Rule>
    {
        new Rule { chapterScene = 3, times = new List<string> { "morning" } },
        new Rule { chapterScene = 5, times = new List<string> { "morning" } },
        new Rule { chapterScene = 7, times = new List<string> { "after_school" } },
    };
    [Tooltip("On: also re-checks when <<advance_time>> runs or dialogue changes the chapter variable in this scene.")]
    [SerializeField] private bool followChanges = true;
    [Tooltip("Dialogue Runner whose Variable Storage holds the Yarn variables. Left empty, the first one in the scene is used.")]
    [SerializeField] private DialogueRunner dialogueRunner;

    private Sprite defaultSprite;
    private string variableName;
    private IDisposable changeListener;
    private bool started;

    private void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (target != null) defaultSprite = target.sprite;

        variableName = (chapterSceneVariable ?? string.Empty).Trim();
        if (!variableName.StartsWith("$")) variableName = "$" + variableName;
    }

    // Start, not Awake: the Dialogue Runner and its Variable Storage are set up by then.
    private void Start()
    {
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
        if (followChanges)
        {
            YarnCommands.TimeOfDayChanged += Apply;
            if (dialogueRunner != null && dialogueRunner.VariableStorage != null)
                changeListener = dialogueRunner.VariableStorage.AddChangeListener(OnVariableChanged);
        }
        started = true;
        Apply();
    }

    private void OnDestroy()
    {
        YarnCommands.TimeOfDayChanged -= Apply;
        changeListener?.Dispose();
    }

    private void OnVariableChanged(string changedName, object value)
    {
        if (changedName == variableName) Apply();
    }

    private void Apply()
    {
        if (!started) return;
        if (target == null)
        {
            Debug.LogError($"StorySpriteSwap on \"{name}\": no SpriteRenderer to swap.", this);
            return;
        }
        if (swapSprite == null)
        {
            Debug.LogError($"StorySpriteSwap on \"{name}\": no Swap Sprite assigned.", this);
            return;
        }
        target.sprite = Matches() ? swapSprite : defaultSprite;
    }

    private bool Matches()
    {
        if (!TryGetChapterScene(out int chapterScene)) return false;
        foreach (Rule rule in rules)
        {
            if (rule == null || rule.chapterScene != chapterScene) continue;
            if (rule.times == null || rule.times.Count == 0 || rule.times.Exists(YarnCommands.IsTimeOfDay))
                return true;
        }
        return false;
    }

    private bool TryGetChapterScene(out int chapterScene)
    {
        chapterScene = 0;
        VariableStorageBehaviour storage = dialogueRunner != null ? dialogueRunner.VariableStorage : null;
        if (storage == null)
        {
            Debug.LogWarning($"StorySpriteSwap on \"{name}\": no Dialogue Runner found, so it keeps its own sprite.", this);
            return false;
        }
        try
        {
            if (!storage.TryGetValue(variableName, out float value)) return false;
            chapterScene = Mathf.RoundToInt(value);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StorySpriteSwap on \"{name}\": can't read {variableName}: {exception.Message}", this);
            return false;
        }
    }
}
