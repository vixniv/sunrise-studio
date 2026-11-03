using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

// Puts the player at the room's door every time a room scene loads, unless a YarnCutscene with
// Keep Player Position is about to play: then the player stays where they are placed in the scene,
// such as Anin waking up in bed. Nothing to attach: it runs on its own in every scene with a PlayerMove and a SceneDoor.
// With several doors, the player comes in through the one that leads back to the previous scene;
// when none does (such as pressing Play in this scene), through the one marked Default Entrance, else the leftmost door.
// The player then looks into the room (see FacePlayer). Logs which door it picked, which way the player faces, and why.
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
        if (mode != LoadSceneMode.Single)
        {
            Debug.Log($"DoorSpawn: {scene.name} loaded additively, so the player is not moved.");
            return;
        }
        // Remembered here rather than when the old scene unloads, because that can happen after this runs.
        string previousScene = currentScene;
        currentScene = scene.name;

        // Searched in the new scene only: the old scene's objects (such as the Dorm's player) can still exist here.
        PlayerMove[] players = InScene<PlayerMove>(scene);
        if (players.Length == 0)
        {
            Debug.Log($"DoorSpawn: {scene.name} has no active PlayerMove, so nothing to place.");
            return;
        }
        PlayerMove player = players[0];

        foreach (YarnCutscene cutscene in InScene<YarnCutscene>(scene))
        {
            if (cutscene.KeepsPlayerInPlace)
            {
                Debug.Log($"DoorSpawn: {cutscene.name} plays with Keep Player Position, so the player stays where the scene places them.", cutscene);
                return;
            }
        }

        SceneDoor[] doors = InScene<SceneDoor>(scene);
        if (doors.Length == 0)
        {
            Debug.Log($"DoorSpawn: {scene.name} has no active SceneDoor, so the player stays where the scene places them.");
            return;
        }
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

        FacePlayer(scene, player, door, position.x);
    }

    // Active components of type T in scene only, unlike FindObjectsByType, which also returns the old scene's.
    private static T[] InScene<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>());
        return found.ToArray();
    }

    // Turns the player to look into the room: the door's Spawn Facing when set, otherwise toward the side
    // of the camera confiner with more space, so a door on the left wall faces right and one on the right wall faces left.
    private static void FacePlayer(Scene scene, PlayerMove player, SceneDoor door, float spawnX)
    {
        if (door.Facing == SceneDoor.SpawnFacing.Left || door.Facing == SceneDoor.SpawnFacing.Right)
        {
            player.Face(door.Facing == SceneDoor.SpawnFacing.Right ? 1f : -1f);
            Debug.Log($"DoorSpawn: player faces {door.Facing} because {door.name} sets Spawn Facing.", door);
            return;
        }

        CinemachineConfiner2D[] confiners = InScene<CinemachineConfiner2D>(scene);
        Collider2D room = confiners.Length > 0 ? confiners[0].BoundingShape2D : null;
        if (room == null || !room.enabled || !room.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"DoorSpawn: no active camera confiner shape, so the player keeps their facing. " +
                             $"Set Spawn Facing on {door.name} to choose it.", door);
            return;
        }

        Bounds bounds = room.bounds;
        float spaceLeft = spawnX - bounds.min.x;
        float spaceRight = bounds.max.x - spawnX;
        bool faceRight = spaceRight >= spaceLeft;
        player.Face(faceRight ? 1f : -1f);
        Debug.Log($"DoorSpawn: player faces {(faceRight ? "Right" : "Left")}: {spaceLeft:0.##} units of room to the left, " +
                  $"{spaceRight:0.##} to the right. Set Spawn Facing on {door.name} if that is wrong.", door);
    }
}
