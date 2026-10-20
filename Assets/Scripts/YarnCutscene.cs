using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays a Yarn node as soon as the scene starts, then loads the next scene when the node ends.
// With no Next Scene the player stays in this scene and gets control back when the node ends.
// Lines type out in the DialogBubble; a tap shows the whole line, the next tap goes to the next line.
// Uses CircleTransition when one exists (it persists across scenes); otherwise loads the scene directly.
public class YarnCutscene : MonoBehaviour
{
    [SerializeField] private YarnDialogPresenter yarnDialog;
    [SerializeField] private string startNode = "Ch01_Prologue";
    [Tooltip("Lines without a character name show above this. Leave empty to use this object.")]
    [SerializeField] private Transform narrator;
    [Tooltip("World offset from the top of the narrator's sprite to the bottom of the dialog box.")]
    [SerializeField] private Vector2 dialogOffset = new Vector2(0f, 0.25f);
    [Tooltip("Seconds before the first line, so the scene can fade in first.")]
    [SerializeField, Min(0f)] private float startDelay = 0.5f;
    [Tooltip("Optional. Frozen until the node ends. Needed when the node has options: the ChoiceWheel shows above this player.")]
    [SerializeField] private PlayerMove player;
    [Tooltip("Scene to load when the cutscene ends. Must be in the Build Profile scene list. Leave empty to stay in this scene.")]
    [SerializeField] private string nextScene = "Dorm";

    private bool leaving;

    private IEnumerator Start()
    {
        if (yarnDialog == null)
        {
            Debug.LogError($"{name} has no Yarn Dialog assigned.", this);
            yield break;
        }
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
        yarnDialog.Play(startNode.Trim(), narrator != null ? narrator : transform, dialogOffset, player, LoadNextScene);
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
