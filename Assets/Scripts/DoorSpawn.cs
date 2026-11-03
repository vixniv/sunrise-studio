using UnityEngine;
using UnityEngine.SceneManagement;

// Puts the player at the room's door every time a room scene loads, unless a YarnCutscene with
// Keep Player Position is about to play: then the player stays where they are placed in the scene,
// such as Anin waking up in bed. Nothing to attach: it runs on its own in every scene with a PlayerMove and a SceneDoor.
// With several doors, the player comes in through the one that leads back to the previous scene;
// when none does (such as pressing Play in this scene), through the one marked Default Entrance, else the leftmost door.
// Logs which door it picked and why.
public static class DoorSpawn
{
    private static string currentScene = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Register()
    {
        currentScene = "";
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Runs after every Awake and OnEnable of the new scene (so ActiveDuringTime and ActiveIfYarnVariable
    // have already shown or hidden the cutscenes), and before any Start (so no cutscene has started yet).
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        // Remembered here rather than when the old scene unloads, because that can happen after this runs.
        string previousScene = currentScene;
        currentScene = scene.name;

        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null) return;

        foreach (YarnCutscene cutscene in Object.FindObjectsByType<YarnCutscene>())
        {
            if (cutscene.KeepsPlayerInPlace)
            {
                Debug.Log($"DoorSpawn: {cutscene.name} plays with Keep Player Position, so the player stays where the scene places them.", cutscene);
                return;
            }
        }

        SceneDoor[] doors = Object.FindObjectsByType<SceneDoor>();
        if (doors.Length == 0) return;
        SceneDoor door = null;
        string reason = "";
        foreach (SceneDoor candidate in doors)
        {
            if (candidate.LeadsTo(previousScene))
            {
                door = candidate;
                reason = $"it leads back to {previousScene}";
                break;
            }
        }
        if (door == null)
        {
            foreach (SceneDoor candidate in doors)
            {
                if (candidate.DefaultEntrance)
                {
                    door = candidate;
                    reason = "no door leads back to the previous scene, and it is the Default Entrance";
                    break;
                }
            }
        }
        if (door == null && doors.Length == 1)
        {
            door = doors[0];
            reason = "it is the only door";
        }
        if (door == null)
        {
            // FindObjectsByType returns the doors in no fixed order, so pick the leftmost: the same door every time.
            door = doors[0];
            foreach (SceneDoor candidate in doors)
                if (candidate.transform.position.x < door.transform.position.x) door = candidate;
            Debug.LogWarning($"DoorSpawn: in {scene.name}, no door leads back to \"{previousScene}\" and none is the Default Entrance, " +
                             $"so the player starts at the leftmost door, {door.name}. Tick Default Entrance on the door the player should use.", door);
            reason = "it is the leftmost door";
        }
        Debug.Log($"DoorSpawn: player starts at {door.name} in {scene.name} (came from \"{previousScene}\") because {reason}.", door);

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
