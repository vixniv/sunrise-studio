using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

// Swaps a character's idle/walk/run art between outfits, e.g. Anin's casual clothes in the dorm before she gets dressed.
// Put it on the "Visual" child next to the Animator (the same object as SitPose and WakeUpPose).
// The Animator's own controller is the "uniform" outfit; Casual Outfit is an Animator Override Controller
// built on that controller with the casual clips swapped in.
//
// When the scene opens, the character starts in casual clothes if a Casual Start Rule matches:
// this object's scene is Room Scene, $chapter_scene equals the rule's Chapter Scene,
// and the rule's Dressed Variable is false.
// Yarn: <<outfit "Anin" "casual">> / <<outfit "Anin" "uniform">> swap right away,
// <<fade_outfit "Anin" "uniform">> swaps while the circle transition covers the screen.
[RequireComponent(typeof(Animator))]
public class CharacterOutfit : MonoBehaviour
{
    public const string Uniform = "uniform";
    public const string Casual = "casual";

    [Serializable]
    public class CasualStartRule
    {
        [Tooltip("Value of $chapter_scene this rule is for, e.g. 1 or 3.")]
        public int chapterScene = 1;
        [Tooltip("Yarn bool that the desk sets to true after getting dressed in this chapter scene, e.g. $anin_dressed or $d2_dressed.")]
        public string dressedVariable = "$anin_dressed";
    }

    [Tooltip("Name used in <<outfit>> and <<fade_outfit>>. Case is ignored.")]
    [SerializeField] private string characterName = "Anin";
    [Tooltip("Animator Override Controller of the Animator's controller, with the casual idle and walk clips swapped in.")]
    [SerializeField] private AnimatorOverrideController casualOutfit;

    [Header("Casual at scene start")]
    [Tooltip("Scene name the Casual Start Rules apply in. In any other scene the character starts in uniform.")]
    [SerializeField] private string roomScene = "Dorm";
    [Tooltip("Yarn number variable that holds the current chapter scene.")]
    [SerializeField] private string chapterSceneVariable = "$chapter_scene";
    [SerializeField] private List<CasualStartRule> casualStartRules = new List<CasualStartRule>
    {
        new CasualStartRule { chapterScene = 1, dressedVariable = "$anin_dressed" },
        new CasualStartRule { chapterScene = 3, dressedVariable = "$d2_dressed" },
    };
    [Tooltip("Dialogue Runner whose Variable Storage holds the Yarn variables. Left empty, the first one in the scene is used.")]
    [SerializeField] private DialogueRunner dialogueRunner;

    private static readonly List<CharacterOutfit> active = new List<CharacterOutfit>();

    private Animator animator;
    private RuntimeAnimatorController uniformController;
    private string current = Uniform;

    public string CharacterName => characterName;
    public string Current => current;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (animator != null) return;
        animator = GetComponent<Animator>();
        uniformController = animator.runtimeAnimatorController;
        // The Inspector might hold the override itself; its base controller is the uniform.
        if (uniformController is AnimatorOverrideController overrideController && overrideController.runtimeAnimatorController != null)
            uniformController = overrideController.runtimeAnimatorController;
    }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    // Start, not Awake: the Dialogue Runner and its Variable Storage are set up by then.
    // The wake-up pose has the Animator disabled at this point, so the swap only shows once Anin stands up.
    private void Start()
    {
        Wear(ShouldStartCasual() ? Casual : Uniform);
    }

    private bool ShouldStartCasual()
    {
        if (casualOutfit == null || casualStartRules == null || casualStartRules.Count == 0) return false;
        if (!string.Equals(gameObject.scene.name, roomScene, StringComparison.OrdinalIgnoreCase)) return false;

        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<DialogueRunner>();
        VariableStorageBehaviour storage = dialogueRunner != null ? dialogueRunner.VariableStorage : null;
        if (storage == null)
        {
            Debug.LogWarning($"CharacterOutfit on \"{name}\": no Dialogue Runner found, so it starts in uniform.", this);
            return false;
        }

        if (!TryGet(storage, chapterSceneVariable, out float chapterScene)) return false;
        foreach (CasualStartRule rule in casualStartRules)
        {
            if (rule == null || Mathf.RoundToInt(chapterScene) != rule.chapterScene) continue;
            // A dressed variable that was never set or declared counts as not dressed.
            bool dressed = !string.IsNullOrWhiteSpace(rule.dressedVariable)
                && TryGet(storage, rule.dressedVariable.Trim(), out bool value) && value;
            return !dressed;
        }
        return false;
    }

    private bool TryGet<T>(VariableStorageBehaviour storage, string variable, out T value)
    {
        try
        {
            return storage.TryGetValue(variable, out value);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"CharacterOutfit on \"{name}\": can't read {variable}: {exception.Message}", this);
            value = default;
            return false;
        }
    }

    // outfit is "casual" or "uniform" (case and spaces ignored). Returns false for an unknown name.
    public bool Wear(string outfit)
    {
        CacheComponents();
        string key = (outfit ?? string.Empty).Trim().ToLowerInvariant();
        RuntimeAnimatorController controller;
        if (key == Uniform)
        {
            controller = uniformController;
        }
        else if (key == Casual)
        {
            if (casualOutfit == null)
            {
                Debug.LogError($"CharacterOutfit on \"{name}\" has no Casual Outfit assigned.", this);
                return false;
            }
            controller = casualOutfit;
        }
        else
        {
            Debug.LogError($"CharacterOutfit: unknown outfit \"{outfit}\". Use \"{Casual}\" or \"{Uniform}\".", this);
            return false;
        }

        current = key;
        if (animator.runtimeAnimatorController == controller) return true;

        // Changing the controller resets the Animator to its default (idle) state; PlayerMove sets
        // IsWalking/IsRunning again every frame, so walking picks up on the next frame.
        animator.runtimeAnimatorController = controller;
        return true;
    }

    // Finds the enabled outfit of the character named in Yarn, e.g. "Anin".
    public static CharacterOutfit Find(string characterName)
    {
        string key = (characterName ?? string.Empty).Trim();
        foreach (CharacterOutfit outfit in active)
        {
            if (outfit != null && string.Equals(outfit.characterName.Trim(), key, StringComparison.OrdinalIgnoreCase))
                return outfit;
        }
        return null;
    }

    // Clears the list when Play Mode starts, even with Domain Reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => active.Clear();
}
