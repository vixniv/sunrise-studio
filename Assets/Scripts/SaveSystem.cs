using System;
using System.Collections.Generic;
using System.IO;
using SunriseStudio.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

// Saves and loads the game's progress in one JSON file on the device (Application.persistentDataPath/save.json).
// Nothing to attach: the pause menu's Save button calls Save, the title screen's Play button calls TryRestore,
// and SaveAutosave saves on its own each time the player gets control in a new room.
//
// A save holds everything the story keeps between scenes: the Yarn variables (PersistentVariableStorage),
// the time of day and loop count (YarnCommands), the cutscenes already played (YarnCutscene), the current quest
// (QuestTracker), and the scene with the player's X position and facing. Outfits, NPC crowds and sprite swaps
// are not saved: they follow from those values when the scene opens.
//
// Saving is refused while dialogue runs, a scene or time transition plays, or a memory montage is on,
// so a save always resumes with the player free to move.
public static class SaveSystem
{
    public const int CurrentVersion = 1;
    private const string FileName = "save.json";

    // Saves each time the player gets control in a new room (see SaveAutosave).
    public static bool AutosaveEnabled = true;

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private static string TempPath => SavePath + ".tmp";
    private static string BackupPath => SavePath + ".bak";

    // True when a save file exists (the backup counts, in case the game closed in the middle of writing).
    public static bool HasSave => File.Exists(SavePath) || File.Exists(BackupPath);

    // True once the title screen started or continued a game in this session. Autosave waits for it, so pressing Play
    // in a room scene in the Editor (with Tools > Playtest Start values) never overwrites the real save.
    public static bool GameStartedFromTitle { get; private set; }

    public static event Action Saved;

    [Serializable]
    private class SaveData
    {
        public int version;
        public string savedAtUtc;
        public string scene;
        public bool hasPlayerPosition;
        public float playerX;
        public bool playerFacesRight;
        public string timeOfDay;
        public int loopsCompleted;
        public string questId;
        public string questObjective;
        public List<string> playedCutscenes = new List<string>();
        public List<string> firstVisitFadesPlayed = new List<string>();
        public List<YarnValue> yarnVariables = new List<YarnValue>();
    }

    // JsonUtility cannot write a Dictionary, so each Yarn variable is one entry with its type.
    [Serializable]
    private class YarnValue
    {
        public string name;
        public string type;
        public float number;
        public string text;
        public bool flag;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        GameStartedFromTitle = false;
        Saved = null;
    }

    // Called by the title screen when Play or New Game starts a game.
    public static void MarkGameStarted() => GameStartedFromTitle = true;

    // True when the scene has a room player (PlayerMove) or a map player (MapPlayerMove), so it can be saved.
    public static bool HasPlayer(Scene scene) => FindPlayer(scene) != null || FindMapPlayer(scene) != null;

    public static bool CanSaveNow(out string reason)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!HasPlayer(scene))
        {
            reason = $"{scene.name} has no player, so there is nothing to save";
            return false;
        }
        if (YarnCommands.MemoryMontageActive)
        {
            reason = "a memory is playing";
            return false;
        }
        if (CircleTransition.Instance != null && CircleTransition.Instance.IsTransitioning ||
            TimeTransition.Instance != null && TimeTransition.Instance.IsTransitioning)
        {
            reason = "a transition is playing";
            return false;
        }
        foreach (DialogueRunner runner in UnityEngine.Object.FindObjectsByType<DialogueRunner>())
        {
            if (runner.IsDialogueRunning)
            {
                reason = "dialogue is running";
                return false;
            }
        }
        PlayerMove player = FindPlayer(scene);
        if (player != null && player.InteractionMenuOpen)
        {
            reason = "the player is in a cutscene or menu";
            return false;
        }
        reason = null;
        return true;
    }

    public static bool Save() => Save(out _);

    public static bool Save(out string reason)
    {
        if (!CanSaveNow(out reason))
        {
            Debug.Log($"SaveSystem: not saved because {reason}.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        SaveData data = new SaveData
        {
            version = CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            scene = scene.name,
            timeOfDay = YarnCommands.TimeOfDay,
            loopsCompleted = YarnCommands.LoopsCompleted,
            questId = QuestTracker.QuestId,
            questObjective = QuestTracker.Objective,
            playedCutscenes = YarnCutscene.GetPlayedNodes(),
            firstVisitFadesPlayed = YarnCutscene.GetFirstVisitFadesPlayed(),
        };

        PlayerMove player = FindPlayer(scene);
        if (player != null)
        {
            data.hasPlayerPosition = true;
            data.playerX = player.transform.position.x;
            data.playerFacesRight = player.FacingRight;
        }

        foreach (KeyValuePair<string, object> variable in PersistentVariableStorage.GetSnapshot())
        {
            YarnValue entry = new YarnValue { name = variable.Key };
            switch (variable.Value)
            {
                case float number: entry.type = "number"; entry.number = number; break;
                case string text: entry.type = "string"; entry.text = text; break;
                case bool flag: entry.type = "bool"; entry.flag = flag; break;
                default: continue;
            }
            data.yarnVariables.Add(entry);
        }

        try
        {
            WriteFile(JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            reason = "the file could not be written";
            Debug.LogError($"SaveSystem: could not write {SavePath}: {exception}");
            return false;
        }

        Debug.Log($"SaveSystem: saved {scene.name}, {data.timeOfDay}, loop {data.loopsCompleted}, " +
                  $"{data.yarnVariables.Count} Yarn variables, to {SavePath}.");
        Saved?.Invoke();
        return true;
    }

    // Puts the saved game's state back and returns the scene to load. The caller loads it (with its usual transition).
    // Returns false, with the state untouched, when there is no save or it cannot be read.
    public static bool TryRestore(out string sceneName)
    {
        sceneName = null;
        if (!TryReadFile(out SaveData data))
            return false;

        if (data.version > CurrentVersion)
        {
            Debug.LogError($"SaveSystem: the save is version {data.version}, newer than this build ({CurrentVersion}).");
            return false;
        }
        if (string.IsNullOrWhiteSpace(data.scene) || !Application.CanStreamedLevelBeLoaded(data.scene))
        {
            Debug.LogError($"SaveSystem: the saved scene \"{data.scene}\" is not in the Build Profile scene list.");
            return false;
        }

        var variables = new Dictionary<string, object>();
        if (data.yarnVariables != null)
        {
            foreach (YarnValue entry in data.yarnVariables)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name)) continue;
                switch (entry.type)
                {
                    case "number": variables[entry.name] = entry.number; break;
                    case "string": variables[entry.name] = entry.text ?? string.Empty; break;
                    case "bool": variables[entry.name] = entry.flag; break;
                }
            }
        }

        PersistentVariableStorage.RestoreSnapshot(variables);
        YarnCutscene.RestorePlayed(data.playedCutscenes, data.firstVisitFadesPlayed);
        YarnCommands.RestoreFromSave(data.timeOfDay, data.loopsCompleted);
        if (string.IsNullOrWhiteSpace(data.questId))
            QuestTracker.ClearQuest();
        else
            QuestTracker.StartQuest(data.questId, data.questObjective);
        if (data.hasPlayerPosition)
            DoorSpawn.SetNextSpawn(data.scene, data.playerX, data.playerFacesRight);

        sceneName = data.scene;
        Debug.Log($"SaveSystem: loaded the save from {data.savedAtUtc} (UTC): {data.scene}, {data.timeOfDay}, " +
                  $"loop {data.loopsCompleted}, {variables.Count} Yarn variables.");
        return true;
    }

    public static void DeleteSave()
    {
        foreach (string path in new[] { SavePath, TempPath, BackupPath })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveSystem: could not delete {path}: {exception}");
            }
        }
    }

    // Writes to a temporary file first, so closing the game in the middle never leaves a broken save:
    // the old save is kept as the backup until the new one is in place.
    private static void WriteFile(string json)
    {
        File.WriteAllText(TempPath, json);
        if (File.Exists(SavePath))
        {
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
            File.Move(SavePath, BackupPath);
        }
        File.Move(TempPath, SavePath);
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
    }

    private static bool TryReadFile(out SaveData data)
    {
        data = null;
        string path = File.Exists(SavePath) ? SavePath : File.Exists(BackupPath) ? BackupPath : null;
        if (path == null)
            return false;

        try
        {
            data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogError($"SaveSystem: could not read {path}: {exception}");
            return false;
        }

        if (data == null)
        {
            Debug.LogError($"SaveSystem: {path} is empty or not a save file.");
            return false;
        }
        return true;
    }

    private static PlayerMove FindPlayer(Scene scene)
    {
        foreach (PlayerMove player in UnityEngine.Object.FindObjectsByType<PlayerMove>())
            if (player.gameObject.scene == scene && player.isActiveAndEnabled) return player;
        return null;
    }

    private static MapPlayerMove FindMapPlayer(Scene scene)
    {
        foreach (MapPlayerMove player in UnityEngine.Object.FindObjectsByType<MapPlayerMove>())
            if (player.gameObject.scene == scene && player.isActiveAndEnabled) return player;
        return null;
    }
}
