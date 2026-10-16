using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 7f;

    [Header("Drag (fraction of screen width)")]
    [Range(0f, 0.2f)] public float deadZone = 0.02f;      // ignore tiny wiggles
    [Range(0f, 0.5f)] public float runThreshold = 0.12f;  // beyond this = run

    Rigidbody2D rb;
    bool dragging;
    Vector2 dragStart;
    float moveX; // -1..1 direction * (walk or run)
    bool running;

    public bool IsRunning => running;
    public float MoveX => moveX;
    public bool InteractionMenuOpen { get; set; }

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void Update()
    {
        moveX = 0f;
        running = false;

        if (InteractionMenuOpen)
        {
            dragging = false;
            return;
        }

        ReadPointer();
        if (moveX == 0f) ReadKeyboard(); // editor fallback
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
        rb.linearVelocity = new Vector2(InteractionMenuOpen ? 0f : moveX * speed, rb.linearVelocity.y);
    }
}
