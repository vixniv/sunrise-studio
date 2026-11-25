using System.Collections.Generic;
using UnityEngine;

// Puts a character with a dynamic Rigidbody2D straight down onto the floor below it.
// The rooms place the player at a height authored in each scene, which does not always match that room's floor
// (the Library floor is lower than the others). Without this, the player appears in the air and gravity drops
// them in view, or a scripted walk (which turns physics off) carries them along in the air and they drop afterward.
// The floor is found from the colliders' own shapes and Transforms, not a physics cast: room-entry cutscenes run
// in the room's first frame, when a cast does not see the room's floor yet.
// Only moves down, by at most MaxDistance, onto a solid collider of the character's own scene; otherwise nothing changes.
public static class GroundSnap
{
    private const float MaxDistance = 3f;
    // Gaps this small (under a pixel at 16 pixels per unit) count as standing already.
    private const float AlreadyGrounded = 0.02f;

    private static readonly List<Collider2D> own = new List<Collider2D>();
    private static readonly List<Collider2D> found = new List<Collider2D>();

    // Returns true when the character was moved.
    public static bool Snap(GameObject character)
    {
        if (character == null) return false;
        return Snap(character.GetComponent<Rigidbody2D>());
    }

    public static bool Snap(Rigidbody2D body)
    {
        if (body == null) return false;
        if (!body.simulated || !body.gameObject.activeInHierarchy ||
            body.bodyType != RigidbodyType2D.Dynamic || body.gravityScale <= 0f)
        {
            Debug.Log($"GroundSnap: {body.name} left as is (simulated {body.simulated}, active {body.gameObject.activeInHierarchy}, " +
                      $"{body.bodyType}, gravity {body.gravityScale}).", body);
            return false;
        }

        // The character's feet: the lowest point of its solid colliders, and the X range they cover.
        own.Clear();
        body.GetComponentsInChildren(own);
        bool hasShape = false;
        float feet = float.PositiveInfinity, left = float.PositiveInfinity, right = float.NegativeInfinity;
        foreach (Collider2D collider in own)
        {
            if (!IsSolid(collider) || collider.attachedRigidbody != body) continue;
            if (!TryGetBounds(collider, out float minX, out float maxX, out float minY, out _)) continue;
            hasShape = true;
            feet = Mathf.Min(feet, minY);
            left = Mathf.Min(left, minX);
            right = Mathf.Max(right, maxX);
        }
        if (!hasShape)
        {
            Debug.Log($"GroundSnap: {body.name} has no solid collider to stand on, so it is left as is.", body);
            return false;
        }

        // The highest top of a solid collider in this scene under the feet, no more than MaxDistance below.
        int layerMask = Physics2D.GetLayerCollisionMask(body.gameObject.layer);
        float floorTop = float.NegativeInfinity;
        Collider2D floor = null;
        foreach (GameObject root in body.gameObject.scene.GetRootGameObjects())
        {
            found.Clear();
            root.GetComponentsInChildren(found);
            foreach (Collider2D collider in found)
            {
                if (!IsSolid(collider) || collider.attachedRigidbody == body ||
                    (layerMask & (1 << collider.gameObject.layer)) == 0) continue;
                if (!TryGetBounds(collider, out float minX, out float maxX, out float minY, out float maxY)) continue;
                // Under the feet: overlaps them in X, and its top is at or just below them
                // (a wall or character reaching above the feet is not a floor).
                if (maxX <= left || minX >= right) continue;
                if (maxY > feet + AlreadyGrounded || maxY < feet - MaxDistance) continue;
                if (maxY > floorTop)
                {
                    floorTop = maxY;
                    floor = collider;
                }
            }
        }
        if (floor == null)
        {
            Debug.Log($"GroundSnap: no floor found within {MaxDistance} units below {body.name} (feet at Y {feet:0.###}).", body);
            return false;
        }

        float drop = feet - floorTop;
        if (drop <= AlreadyGrounded) return false;

        Vector2 position = body.position + Vector2.down * drop;
        body.position = position;
        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        Transform transform = body.transform;
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        Debug.Log($"GroundSnap: moved {body.name} down {drop:0.###} onto {floor.name}, to Y {position.y:0.###}.", body);
        return true;
    }

    private static bool IsSolid(Collider2D collider) =>
        collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy;

    // World-space box of a collider, worked out from its Transform so it is right even before physics has run.
    // Box colliders are measured exactly (rotation is ignored: the rooms are not rotated); other shapes use their bounds.
    private static bool TryGetBounds(Collider2D collider, out float minX, out float maxX, out float minY, out float maxY)
    {
        if (collider is BoxCollider2D box)
        {
            Transform t = box.transform;
            Vector3 scale = t.lossyScale;
            Vector2 center = t.TransformPoint(box.offset);
            float halfWidth = Mathf.Abs(box.size.x * scale.x) * 0.5f + box.edgeRadius;
            float halfHeight = Mathf.Abs(box.size.y * scale.y) * 0.5f + box.edgeRadius;
            minX = center.x - halfWidth;
            maxX = center.x + halfWidth;
            minY = center.y - halfHeight;
            maxY = center.y + halfHeight;
            return true;
        }

        Bounds bounds = collider.bounds;
        minX = bounds.min.x;
        maxX = bounds.max.x;
        minY = bounds.min.y;
        maxY = bounds.max.y;
        return bounds.size.sqrMagnitude > 0f;
    }
}
