using UnityEngine;
using UnityEngine.InputSystem;

/**
 * A free-flying camera for exploring large terrains.
 * WASD = move, Q/E = down/up, hold right mouse button = look around,
 * Shift = move faster, mouse wheel = change the base speed.
 */
public class FlyCamera : MonoBehaviour {
    [SerializeField] float speed = 100f;
    [SerializeField] float fastMultiplier = 5f;
    [SerializeField] float lookSensitivity = 0.15f;
    [SerializeField] float minSpeed = 5f;
    [SerializeField] float maxSpeed = 2000f;

    float yaw, pitch;

    void Start() {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    void Update() {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (mouse.rightButton.isPressed) {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0) speed = Mathf.Min(speed * 1.1f, maxSpeed);
        if (scroll < 0) speed = Mathf.Max(speed / 1.1f, minSpeed);

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += Vector3.forward;
        if (keyboard.sKey.isPressed) move += Vector3.back;
        if (keyboard.dKey.isPressed) move += Vector3.right;
        if (keyboard.aKey.isPressed) move += Vector3.left;
        if (keyboard.eKey.isPressed) move += Vector3.up;
        if (keyboard.qKey.isPressed) move += Vector3.down;

        float currentSpeed = speed * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
        transform.Translate(move * currentSpeed * Time.deltaTime, Space.Self);
    }
}
