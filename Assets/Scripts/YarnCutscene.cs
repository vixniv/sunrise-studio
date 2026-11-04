using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays a Yarn node as soon as the scene starts, then loads the next scene when the node ends.
// With no Next Scene the player stays in this scene and gets control back when the node ends.
// Lines type out in the DialogBubble; a tap shows the whole line, the next tap goes to the next line.
// Uses CircleTransition when one exists (it persists across scenes); otherwise loads the scene directly.
// To play it only at one time of day, add ActiveDuringTime to this object.
public class YarnCutscene : MonoBehaviour
{
    // Start nodes already played this loop. <<hard_reset>> clears it.
    private static readonly HashSet<string> played = new HashSet<string>();

    [SerializeField] private YarnDialogPresenter yarnDialog;
    [SerializeField] private string startNode = "Ch01_Prologue";
    [Tooltip("Lines without a character name show above this. Leave empty to use this object.")]
    [SerializeField] private Transform narrator;
    [Tooltip("World offset from the top of the narrator's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);
    [Tooltip("Seconds before the first line, so the scene can fade in first.")]
    [SerializeField, Min(0f)] private float startDelay = 0.5f;
    [Tooltip("Frozen from the moment the scene opens until the node ends; the ChoiceWheel shows above this player. " +
             "Leave empty to use the PlayerMove in the scene.")]
    [SerializeField] private PlayerMove player;
    [Tooltip("Scene to load when the cutscene ends. Must be in the Build Profile scene list. Leave empty to stay in this scene.")]
    [SerializeField] private string nextScene = "Dorm";
    [Tooltip("Plays the node only the first time this scene opens, until <<hard_reset>> starts the day over.")]
    [SerializeField] private bool playOnce = true;
    [Tooltip("On: when this cutscene plays, the player stays where they are placed in the scene. " +
             "Off: the player still starts at the door (see DoorSpawn).")]
    [SerializeField] private bool keepPlayerPosition = true;

    private bool leaving;

    // True when this cutscene is about to play and keeps the player where the scene places them. Checked before Start.
    public bool KeepsPlayerInPlace =>
        keepPlayerPosition && isActiveAndEnabled && yarnDialog != null && !(playOnce && played.Contains(startNode.Trim()));

    // Clears the played nodes when Play Mode starts, even with Domain Reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => played.Clear();

    public static void ForgetPlayed() => played.Clear();

    private IEnumerator Start()
    {
        if (yarnDialog == null)
        {
            Debug.LogError($"{name} has no Yarn Dialog assigned.", this);
            yield break;
        }
        string node = startNode.Trim();
        if (playOnce && !played.Add(node)) yield break;

        if (player == null) player = FindAnyObjectByType<PlayerMove>();
        // Frozen during Start Delay too, so the player cannot walk before the first line shows.
        if (player != null) player.InteractionMenuOpen = true;
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
        yarnDialog.Play(node, narrator != null ? narrator : transform, dialogOffset, player, LoadNextScene);
        // Play refuses while other dialogue runs. When that is not the presenter's own dialogue (which unfreezes
        // the player when it ends), nothing else would unfreeze the player, so do it here.
        if (player != null && !yarnDialog.IsPlaying) player.InteractionMenuOpen = false;
    }

    private void LoadNextScene()
    {
        // Play also reports back when the scene is unloading; only the first call counts.
        if (leaving || !isActiveAndEnabled || string.IsNullOrWhiteSpace(nextScene)) return;

        leaving = true;
        if (CircleTransition.Instance != null)
            CircleTransition.Instance.LoadScene(nextScene.Trim());
        else
            SceneManager.LoadScene(nextScene.Trim());
    }
}
