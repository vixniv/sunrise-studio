using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

// Shows Yarn dialogue with the game's own UI: lines go to the DialogBubble above the speaking character,
// options go to the ChoiceWheel above the player. Add it to the Dialogue Runner's Dialogue Presenters list.
// A line with a character name ("Kirana: Hi.") shows above the matching DialogSpeaker;
// a line without one shows above the object the dialogue was started from.
// Cutscenes pass no player: nobody is frozen, and taps still advance the lines.
public class YarnDialogPresenter : DialoguePresenterBase
{
    [SerializeField] private DialogueRunner runner;
    [SerializeField] private DialogBubble bubble;
    [SerializeField] private ChoiceWheel wheel;

    // Empty references are found in the scene, so this works from a prefab without per-scene setup.
    private void Awake()
    {
        if (runner == null) runner = GetComponent<DialogueRunner>();
        if (bubble == null) bubble = FindAnyObjectByType<DialogBubble>();
        if (wheel == null) wheel = FindAnyObjectByType<ChoiceWheel>();
    }

    private bool playing;
    private PlayerMove listener;
    private Transform defaultTalker;
    private Vector2 defaultOffset;
    private Action onFinished;

    public bool IsPlaying => playing;

    // Runs the Yarn node. Lines without a character name show above defaultTalker (plus talkerOffset).
    // finished runs once the dialogue ends. player may be null (cutscenes).
    public void Play(string node, Transform talker, Vector2 talkerOffset, PlayerMove player, Action finished)
    {
        if (IsPlaying || runner == null || runner.IsDialogueRunning)
        {
            finished?.Invoke();
            return;
        }
        playing = true;
        listener = player;
        if (listener != null) listener.InteractionMenuOpen = true;
        defaultTalker = talker;
        defaultOffset = talkerOffset;
        onFinished = finished;
        RunNode(node);
    }

    private async void RunNode(string node)
    {
        try
        {
            await runner.StartDialogue(node);
        }
        catch (Exception e)
        {
            Debug.LogError($"Could not start Yarn node \"{node}\": {e.Message}", this);
        }
        // A missing project or node never starts, so nothing else would end it.
        if (!runner.IsDialogueRunning) Finish();
    }

    public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

    public override YarnTask OnDialogueCompleteAsync()
    {
        Finish();
        return YarnTask.CompletedTask;
    }

    public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (bubble == null || !IsPlaying) return;
        Transform talker = defaultTalker;
        Vector2 offset = defaultOffset;
        if (DialogSpeaker.TryFind(line.CharacterName, out DialogSpeaker speaker))
        {
            talker = speaker.transform;
            offset = speaker.DialogOffset;
        }
        else if (!string.IsNullOrEmpty(line.CharacterName))
        {
            Debug.LogWarning($"No enabled DialogSpeaker named \"{line.CharacterName}\"; showing the line above {(talker != null ? talker.name : "nothing")}.", this);
        }

        bool advanced = false;
        bubble.Say(talker, offset, line.TextWithoutCharacterName.Text, listener, () => advanced = true);
        await YarnTask.WaitUntil(() => advanced || !bubble.IsOpen, token.NextContentToken).SuppressCancellationThrow();
    }

    public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken token)
    {
        if (wheel == null || !IsPlaying) return null;
        if (listener == null)
        {
            Debug.LogWarning("Yarn options need a player to show the ChoiceWheel above; skipping them.", this);
            return null;
        }
        // Options whose condition failed are left off the wheel.
        var available = new List<DialogueOption>();
        var choices = new List<ChoiceWheel.Choice>();
        foreach (DialogueOption option in dialogueOptions)
        {
            if (!option.IsAvailable) continue;
            available.Add(option);
            choices.Add(new ChoiceWheel.Choice { label = option.Line.TextWithoutCharacterName.Text });
        }
        if (available.Count == 0) return null;

        // The question bubble would overlap the wheel when the speaker stands next to the player.
        // The next line reopens it.
        if (bubble != null) bubble.Close();
        int picked = -1;
        wheel.Open(listener, choices, index => picked = index);
        await YarnTask.WaitUntil(() => picked >= 0 || !wheel.IsOpen, token.NextContentToken).SuppressCancellationThrow();
        if (wheel.IsOpen) wheel.Close();
        // Closing the wheel frees the player; the dialogue is still going.
        if (listener != null) listener.InteractionMenuOpen = true;
        return picked >= 0 ? available[picked] : null;
    }

    private void Finish()
    {
        if (!IsPlaying) return;
        if (wheel != null && wheel.IsOpen) wheel.Close();
        if (bubble != null) bubble.Close();
        if (listener != null) listener.InteractionMenuOpen = false;
        Action finished = onFinished;
        playing = false;
        listener = null;
        defaultTalker = null;
        onFinished = null;
        finished?.Invoke();
    }

    private void OnDisable() => Finish();
}
