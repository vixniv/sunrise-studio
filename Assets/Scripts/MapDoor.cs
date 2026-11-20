using System;
using FMODUnity;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MapDoor : MonoBehaviour
{
    [Header("Destination")]
    public string destinationScene = "SceneTemplate";

    [Header("Tap Area")]
    [Tooltip("Optional room collider (for example under PlayableMap/Rooms) so tapping anywhere on that room opens this destination. This object's own Collider2D is also tappable.")]
    public Collider2D tapArea;

    [Header("Entry")]
    [Tooltip("Solid collider belonging to this building.")]
    public Collider2D buildingCollider;

    [Tooltip("Point just inside the building.")]
    public Transform entryPoint;

    [Tooltip("Where the map player appears after returning from this destination. " +
             "Leave empty to use this door's position.")]
    public Transform returnPoint;

    [Header("Sound")]
    [Tooltip("Optional FMOD event played as the transition starts closing. Defaults to event:/SFX/Door/Open when empty.")]
    public EventReference enterEvent;

    [Tooltip("Legacy fallback clip when FMOD banks are not loaded yet.")]
    public AudioClip enterSound;

    [Range(0f, 1f)]
    public float enterVolume = 1f;

    public bool LeadsTo(string sceneName) =>
        !string.IsNullOrWhiteSpace(sceneName) &&
        string.Equals(
            destinationScene?.Trim(),
            sceneName.Trim(),
            StringComparison.OrdinalIgnoreCase);

    public bool ContainsCollider(Collider2D hit) =>
        hit != null &&
        (hit == tapArea ||
         hit.gameObject == gameObject ||
         (buildingCollider != null && hit == buildingCollider));

    public Vector2 ReturnPosition =>
        returnPoint != null ? returnPoint.position : transform.position;

    void Reset()
    {
        if (TryGetComponent(out BoxCollider2D trigger))
            trigger.isTrigger = true;
    }
}
