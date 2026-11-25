using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Saves the game once each time the player gets control in a newly loaded scene: after the room's opening cutscene,
// once no dialogue or transition has run for Settle Seconds. Nothing to attach: it creates itself when the game starts
// and lives across scenes. Turn it off with SaveSystem.AutosaveEnabled = false.
// It only saves after the title screen started or continued a game (see SaveSystem.GameStartedFromTitle).
public sealed class SaveAutosave : MonoBehaviour
{
    private const float SettleSeconds = 0.75f;

    private static SaveAutosave instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        GameObject host = new GameObject(nameof(SaveAutosave));
        DontDestroyOnLoad(host);
        instance = host.AddComponent<SaveAutosave>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        StopAllCoroutines();
        StartCoroutine(SaveWhenSettled(scene.name));
    }

    private IEnumerator SaveWhenSettled(string sceneName)
    {
        // Lets every Start run first, so a cutscene that plays on arrival has already frozen the player.
        yield return null;
        yield return null;

        // Title screen, Prologue and other scenes without a player: nothing to save.
        if (!SaveSystem.HasPlayer(SceneManager.GetActiveScene()))
            yield break;

        const float checkInterval = 0.1f;
        var wait = new WaitForSecondsRealtime(checkInterval);
        float settled = 0f;
        while (settled < SettleSeconds)
        {
            if (SceneManager.GetActiveScene().name != sceneName)
                yield break;

            bool ready = SaveSystem.AutosaveEnabled && SaveSystem.GameStartedFromTitle &&
                         Time.timeScale > 0f && SaveSystem.CanSaveNow(out _);
            settled = ready ? settled + checkInterval : 0f;
            yield return wait;
        }

        if (SaveSystem.Save(out string reason))
            Debug.Log($"SaveAutosave: saved on arrival in {sceneName}.");
        else
            Debug.Log($"SaveAutosave: skipped in {sceneName} because {reason}.");
    }
}
