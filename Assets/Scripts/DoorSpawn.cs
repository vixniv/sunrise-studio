using UnityEngine;
using UnityEngine.SceneManagement;

// Puts the player at the room's door every time a room scene loads, unless a YarnCutscene with
// Keep Player Position is about to play: then the player stays where they are placed in the scene,
// such as Anin waking up in bed. Nothing to attach: it runs on its own in every scene with a PlayerMove and a SceneDoor.
// With several doors, the player comes in through the one that leads back to the previous scene
// (any one of them when none does).
public static class DoorSpawn
{
    private static string previousScene = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        previousScene = "";
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene) => previousScene = scene.name;

    // Runs after every Awake and OnEnable of the new scene (so ActiveDuringTime and ActiveIfYarnVariable
    // have already shown or hidden the cutscenes), and before any Start (so no cutscene has started yet).
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null) return;

        foreach (YarnCutscene cutscene in Object.FindObjectsByType<YarnCutscene>())
            if (cutscene.KeepsPlayerInPlace) return;

        SceneDoor[] doors = Object.FindObjectsByType<SceneDoor>();
        if (doors.Length == 0) return;
        SceneDoor door = doors[0];
        foreach (SceneDoor candidate in doors)
        {
            if (candidate.LeadsTo(previousScene))
            {
                door = candidate;
                break;
            }
        }

        Vector2 position = door.SpawnPosition(player.transform.position);
        player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector2.zero;
        }
    }
}
