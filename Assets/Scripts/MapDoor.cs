using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class MapDoor : MonoBehaviour
{
    [Header("Destination")]
    public string destinationScene = "SceneTemplate";

    [Header("Entry")]
    [Tooltip("Solid collider belonging to this building.")]
    public Collider2D buildingCollider;

    [Tooltip("Point just inside the building.")]
    public Transform entryPoint;

    [Header("Sound")]
    [Tooltip("Optional. Plays as the transition starts closing, after the player walks in.")]
    public AudioClip enterSound;

    [Range(0f, 1f)]
    public float enterVolume = 1f;

    void Reset()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
    }
}
