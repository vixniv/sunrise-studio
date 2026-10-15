using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class MapPlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [Min(0f)] public float walkSpeed = 3f;
    [Min(0f)] public float runSpeed = 7f;

    [Header("Drag (fraction of shorter screen side)")]
    [Range(0f, 0.2f)] public float deadZone = 0.02f;
    [Range(0f, 0.5f)] public float runThreshold = 0.12f;

    [Header("Hallway Restriction")]
    public bool restrictToHallways = true;
    public LayerMask walkableLayer;

    [Header("Walking Bob")]
    [Tooltip("Assign a child object containing only the player's SpriteRenderer.")]
    public Transform visual;
    [Min(1f)] public float pixelsPerUnit = 16f;
    [Min(0f)] public float bobHeightPixels = 1f;
    [Min(0f)] public float bobCyclesPerSecond = 4f;

    Rigidbody2D rb;
    Vector2 moveDirection;
    Vector3 visualRestPosition;
    bool dragging;
    bool running;
    bool moving;
    Vector2 dragStart;
    float bobTime;

    public Vector2 MoveDirection => moveDirection;
    public bool IsRunning => running;
    public bool IsMoving => moving;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (visual == null)
            visual = transform.Find("Visual");

        if (visual != null)
            visualRestPosition = visual.localPosition;
        else
            Debug.LogWarning(
                "MapPlayerMove needs a child named Visual for walking bob. " +
                "Movement still works without it.",
                this);

        if (restrictToHallways && walkableLayer.value == 0)
            Debug.LogWarning(
                "MapPlayerMove is restricted to hallways, but Walkable Layer is empty.",
                this);
    }

    void Update()
    {
        moveDirection = Vector2.zero;
        running = false;

        ReadPointer();

        if (!dragging)
            ReadKeyboard();
    }

    void FixedUpdate()
    {
        float speed = running ? runSpeed : walkSpeed;
        Vector2 start = rb.position;
        Vector2 target = start + moveDirection * speed * Time.fixedDeltaTime;
        Vector2 resolved = ResolveWalkablePosition(start, target);

        moving = moveDirection.sqrMagnitude > 0f &&
                 (resolved - start).sqrMagnitude > 0.000001f;

        rb.MovePosition(resolved);
    }

    void LateUpdate()
    {
        if (visual == null)
            return;

        if (!moving || bobHeightPixels <= 0f || bobCyclesPerSecond <= 0f)
        {
            bobTime = 0f;
            visual.localPosition = visualRestPosition;
            return;
        }

        bobTime += Time.deltaTime;

        float heightInUnits = bobHeightPixels / pixelsPerUnit;
        float rawOffset = Mathf.Sin(
            bobTime * bobCyclesPerSecond * Mathf.PI * 2f) * heightInUnits;

        float pixelSnappedOffset =
            Mathf.Round(rawOffset * pixelsPerUnit) / pixelsPerUnit;

        visual.localPosition =
            visualRestPosition + Vector3.up * pixelSnappedOffset;
    }

    void ReadPointer()
    {
        Pointer pointer = Pointer.current;
        if (pointer == null)
            return;

        if (pointer.press.wasPressedThisFrame)
        {
            dragging = true;
            dragStart = pointer.position.ReadValue();
        }

        if (pointer.press.wasReleasedThisFrame)
        {
            dragging = false;
            return;
        }

        if (!dragging)
            return;

        float screenScale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        Vector2 drag = (pointer.position.ReadValue() - dragStart) / screenScale;
        float distance = drag.magnitude;

        if (distance < deadZone)
            return;

        moveDirection = SnapToEightDirections(drag);
        running = distance >= runThreshold;
    }

    void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        Vector2 input = Vector2.zero;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            input.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            input.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            input.y += 1f;

        moveDirection = input.normalized;
        running = moveDirection.sqrMagnitude > 0f &&
                  (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    Vector2 SnapToEightDirections(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float snappedAngle = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle));
    }

    Vector2 ResolveWalkablePosition(Vector2 start, Vector2 target)
    {
        if (!restrictToHallways)
            return target;

        if (walkableLayer.value == 0)
            return start;

        if (IsWalkable(target))
            return target;

        Vector2 resolved = start;
        Vector2 horizontal = new Vector2(target.x, start.y);

        if (IsWalkable(horizontal))
            resolved.x = horizontal.x;

        Vector2 vertical = new Vector2(resolved.x, target.y);

        if (IsWalkable(vertical))
            resolved.y = vertical.y;

        return resolved;
    }

    bool IsWalkable(Vector2 position)
    {
        return Physics2D.OverlapPoint(position, walkableLayer) != null;
    }

    void OnDisable()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (visual != null)
            visual.localPosition = visualRestPosition;
    }

    void OnValidate()
    {
        runThreshold = Mathf.Max(runThreshold, deadZone);
        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
    }
}
