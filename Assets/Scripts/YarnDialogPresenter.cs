using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;
using SunriseStudio.UI;

// Shows Yarn dialogue with the game's own UI: lines go to the DialogBubble above the speaking character,
// options go to the ChoiceWheel above the player. Add it to the Dialogue Runner's Dialogue Presenters list.
// A line with a character name ("Kirana: Hi.") shows above the matching DialogSpeaker;
// a line without one shows above the object the dialogue was started from.
// With no player (a scene without one, such as the Prologue), nobody is frozen, and taps still advance the lines.
public class YarnDialogPresenter : DialoguePresenterBase
{
    [SerializeField] private DialogueRunner runner;
    [SerializeField] private DialogBubble bubble;
    [SerializeField] private ChoiceWheel wheel;
    [SerializeField] private DialogueHudView hud;

    // Empty references are found in the scene, so this works from a prefab without per-scene setup.
    private void Awake()
    {
        if (runner == null) runner = GetComponent<DialogueRunner>();
        if (bubble == null) bubble = FindAnyObjectByType<DialogBubble>();
        if (wheel == null) wheel = FindAnyObjectByType<ChoiceWheel>();
        hud = ResolveActiveHud();
    }

    private bool playing;
    private PlayerMove listener;
    private Transform defaultTalker;
    private Vector2 defaultOffset;
    private Action onFinished;
    private IReadOnlyList<string> directLines;
    private int directLineIndex;

    public bool IsPlaying => playing;

    public bool CanPlayNode(string node)
    {
        if (runner == null || runner.YarnProject == null || string.IsNullOrWhiteSpace(node))
            return false;

        string wanted = node.Trim();
        foreach (string available in runner.YarnProject.NodeNames)
            if (string.Equals(available, wanted, StringComparison.Ordinal))
                return true;
        return false;
    }

    // Hides the active dialogue display while Yarn continues a cutscene command.
    public void HideDialogueForCutscene()
    {
        if (hud != null)
            hud.Hide();
        else if (bubble != null)
            bubble.HideForCutscene();
    }

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
        hud = ResolveActiveHud();
        RunNode(node);
    }

    // Uses same HUD for inspector-authored fallback lines when configured Yarn node is missing.
    public void PlayLines(
        IReadOnlyList<string> lines,
        Transform talker,
        Vector2 talkerOffset,
        PlayerMove player,
        Action finished)
    {
        if (IsPlaying || runner != null && runner.IsDialogueRunning || lines == null || lines.Count == 0)
        {
            finished?.Invoke();
            return;
        }

        playing = true;
        listener = player;
        if (listener != null)
            listener.InteractionMenuOpen = true;
        defaultTalker = talker;
        defaultOffset = talkerOffset;
        onFinished = finished;
        directLines = lines;
        directLineIndex = 0;
        hud = ResolveActiveHud();

        if (hud != null)
            ShowNextDirectLine();
        else if (bubble != null)
            bubble.Show(talker, talkerOffset, lines, player, Finish);
        else
            Finish();
    }

    private void ShowNextDirectLine()
    {
        if (!playing || directLines == null)
            return;
        if (directLineIndex >= directLines.Count)
        {
            Finish();
            return;
        }

        DialogSpeaker speaker = defaultTalker != null
            ? defaultTalker.GetComponent<DialogSpeaker>()
            : null;
        string speakerName = speaker != null ? speaker.CharacterName : string.Empty;
        string line = directLines[directLineIndex++];
        hud.ShowLine(speakerName, line, defaultTalker, defaultOffset, ShowNextDirectLine);
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
        if (!IsPlaying) return;
        hud = ResolveActiveHud();
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

        if (hud != null)
        {
            // Never let the legacy world-space DialogBubble render alongside the HUD.
            // Its TMP text lives in world space and can otherwise appear far below the
            // screen-space bubble while the HUD panel itself looks empty.
            if (bubble != null && bubble.IsOpen)
                bubble.Close();

            bool hudAdvanced = false;
            hud.ShowLine(line.CharacterName, line.TextWithoutCharacterName.Text, talker, offset, () => hudAdvanced = true);
            await YarnTask.WaitUntil(() => hudAdvanced || !hud.IsVisible, token.NextContentToken).SuppressCancellationThrow();
            return;
        }
        if (bubble == null) return;

        bool advanced = false;
        bubble.Say(talker, offset, line.TextWithoutCharacterName.Text, listener, () => advanced = true);
        await YarnTask.WaitUntil(() => advanced || !bubble.IsOpen, token.NextContentToken).SuppressCancellationThrow();
    }

    public override async YarnTask<DialogueOption> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken token)
    {
        if (!IsPlaying) return null;
        if (hud == null && wheel == null) return null;
        if (hud == null && listener == null)
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

        if (hud != null)
        {
            int hudPicked = -1;
            var labels = new List<string>();
            foreach (DialogueOption option in available)
                labels.Add(option.Line.TextWithoutCharacterName.Text);
            hud.ShowChoices(labels, index => hudPicked = index);
            await YarnTask.WaitUntil(() => hudPicked >= 0 || !hud.IsVisible, token.NextContentToken).SuppressCancellationThrow();
            hud.ClearChoices();
            return hudPicked >= 0 ? available[hudPicked] : null;
        }

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

    // A finished line stays on screen until the next line replaces it. Commands that cover the screen
    // (<<advance_time>>, <<fade>>, scene changes) call this first, so the old line is gone when the screen opens again.
    // The dialogue keeps running: the next line opens the bubble (or the HUD) again.
    public void HideCurrentLine()
    {
        if (!IsPlaying) return;
        if (bubble != null) bubble.Close();
        // Lines show in the DialogueHudView when the scene has one, so it must be hidden too.
        if (hud != null) hud.Hide();
        // Closing the bubble frees the player; the dialogue is still going.
        if (listener != null) listener.InteractionMenuOpen = true;
    }

    private void Finish()
    {
        if (!IsPlaying) return;
        if (wheel != null && wheel.IsOpen) wheel.Close();
        if (bubble != null) bubble.Close();
        if (hud != null) hud.Hide();
        if (listener != null) listener.InteractionMenuOpen = false;
        Action finished = onFinished;
        playing = false;
        listener = null;
        defaultTalker = null;
        directLines = null;
        directLineIndex = 0;
        onFinished = null;
        finished?.Invoke();
    }

    private void OnDisable() => Finish();

    // FindAnyObjectByType may return a hidden HUD from another loaded scene. Prefer the active HUD
    // that belongs to this Dialogue Runner's scene, then any active HUD, then an inactive fallback.
    private DialogueHudView ResolveActiveHud()
    {
        DialogueHudView[] candidates = FindObjectsByType<DialogueHudView>(FindObjectsInactive.Include);
        DialogueHudView activeFallback = null;
        DialogueHudView sameSceneInactive = null;
        DialogueHudView inactiveFallback = null;

        foreach (DialogueHudView candidate in candidates)
        {
            if (candidate == null)
                continue;
            inactiveFallback ??= candidate;
            if (candidate.gameObject.scene == gameObject.scene)
            {
                if (candidate.gameObject.activeInHierarchy)
                    return candidate;
                sameSceneInactive ??= candidate;
                continue;
            }
            if (!candidate.gameObject.activeInHierarchy)
                continue;
            activeFallback ??= candidate;
        }

        if (sameSceneInactive != null)
            return sameSceneInactive;
        return activeFallback != null ? activeFallback : inactiveFallback;
    }
}
