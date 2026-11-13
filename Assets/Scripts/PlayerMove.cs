using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 7f;
    [Tooltip("Untick in rooms where the player may only walk, e.g. the Dorm. A long drag or Shift then walks.")]
    public bool allowRunning = true;

    [Header("Drag (fraction of screen width)")]
    [Range(0f, 0.2f)] public float deadZone = 0.02f;      // ignore tiny wiggles
    [Range(0f, 0.5f)] public float runThreshold = 0.12f;  // beyond this = run

    [Header("Facing")]
    [Tooltip("Sprite to flip when walking. Left empty, the SpriteRenderer on the child named \"Visual\" is used.")]
    public SpriteRenderer visual;
    [Tooltip("Tick if the art is drawn looking left, untick if it looks right.")]
    public bool artFacesLeft = true;

    [Header("Animation")]
    [Tooltip("Animator with IsWalking and IsRunning bools. Left empty, the Animator on the Visual sprite is used.")]
    public Animator animator;

    static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    Rigidbody2D rb;
    bool dragging;
    Vector2 dragStart;
    float moveX; // -1..1 direction * (walk or run)
    bool running;

    public bool IsRunning => running;
    public float MoveX => moveX;
    public bool InteractionMenuOpen { get; set; }

    // Scripted walk (WalkTo): input is ignored until the player reaches walkTargetX.
    const float WalkArriveDistance = 0.02f;
    const float WalkStuckSeconds = 0.3f;
    const float WalkMaxSeconds = 5f;
    bool autoWalking;
    float walkTargetX;
    float walkFaceDirection;
    Action walkArrived;
    float walkStartTime;
    float walkLastProgressTime;
    float walkLastX;

    public bool IsAutoWalking => autoWalking;

    // Walks the player to targetX at walk speed, ignoring input, then turns to faceDirection
    // (> 0 right, < 0 left, 0 keeps the walking direction) and calls arrived.
    // Something in the way (a wall) stops the walk early; arrived still runs.
    public void WalkTo(float targetX, float faceDirection, Action arrived)
    {
        if (autoWalking) FinishWalk();
        autoWalking = true;
        walkTargetX = targetX;
        walkFaceDirection = faceDirection;
        walkArrived = arrived;
        walkStartTime = Time.time;
        walkLastProgressTime = Time.time;
        walkLastX = transform.position.x;
        dragging = false;
        if (Mathf.Abs(targetX - transform.position.x) <= WalkArriveDistance) FinishWalk();
    }

    void UpdateWalk()
    {
        float x = transform.position.x;
        float remaining = walkTargetX - x;
        if (Mathf.Abs(x - walkLastX) > 0.001f)
        {
            walkLastX = x;
            walkLastProgressTime = Time.time;
        }
        if (Mathf.Abs(remaining) <= WalkArriveDistance
            || Time.time - walkLastProgressTime > WalkStuckSeconds
            || Time.time - walkStartTime > WalkMaxSeconds)
        {
            FinishWalk();
            return;
        }
        moveX = Mathf.Sign(remaining);
        Face(moveX);
    }

    void FinishWalk()
    {
        autoWalking = false;
        moveX = 0f;
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        Face(walkFaceDirection);
        Action arrived = walkArrived;
        walkArrived = null;
        arrived?.Invoke();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (visual == null)
        {
            Transform child = transform.Find("Visual");
            if (child != null) visual = child.GetComponent<SpriteRenderer>();
        }
        if (animator == null && visual != null)
            animator = visual.GetComponent<Animator>();
    }

    void Update()
    {
        moveX = 0f;
        running = false;

        if (autoWalking)
        {
            UpdateWalk();
            return;
        }

        if (InteractionMenuOpen)
        {
            dragging = false;
            return;
        }

        ReadPointer();
        if (moveX == 0f) ReadKeyboard(); // editor fallback
        if (!allowRunning) running = false;

        // Keeps the last direction when standing still.
        Face(moveX);
    }

    // Turns the sprite to look right (direction > 0) or left (direction < 0); 0 keeps the current facing.
    public void Face(float direction)
    {
        if (direction == 0f || visual == null) return;
        visual.flipX = artFacesLeft ? direction > 0f : direction < 0f;
    }

    void LateUpdate()
    {
        if (animator == null) return;
        animator.SetBool(IsWalkingHash, moveX != 0f);
        animator.SetBool(IsRunningHash, running);
    }

    void ReadPointer()
    {
        var pointer = Pointer.current; // Mouse in Editor, Touchscreen on iPhone
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame)
        {
            dragging = true;
            dragStart = pointer.position.ReadValue();
        }
        if (pointer.press.wasReleasedThisFrame) dragging = false;
        if (!dragging) return;

        float dx = (pointer.position.ReadValue().x - dragStart.x) / Screen.width;
        float dist = Mathf.Abs(dx);
        if (dist < deadZone) return;

        moveX = Mathf.Sign(dx);
        running = dist >= runThreshold;
    }

    void ReadKeyboard()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveX -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveX += 1f;
        running = moveX != 0f && kb.leftShiftKey.isPressed;
    }

    void FixedUpdate()
    {
        float speed = running ? runSpeed : walkSpeed;
        if (autoWalking)
        {
            // Slow down on the last step so the player does not overshoot the target.
            float remaining = walkTargetX - rb.position.x;
            float step = Mathf.Min(speed, Mathf.Abs(remaining) / Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(Mathf.Sign(remaining) * step, rb.linearVelocity.y);
            return;
        }
        rb.linearVelocity = new Vector2(InteractionMenuOpen ? 0f : moveX * speed, rb.linearVelocity.y);
    }

    void OnDisable()
    {
        // A walk cut short by a scene change must not leave its callback waiting.
        autoWalking = false;
        walkArrived = null;
    }
}
