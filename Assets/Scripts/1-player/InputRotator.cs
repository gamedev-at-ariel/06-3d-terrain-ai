using UnityEngine;
using UnityEngine.InputSystem;

/**
 * This component rotates its object according to the mouse movement, in a given rotation speed.
 */ 
public class InputRotator : MonoBehaviour {
    [SerializeField] float rotationSpeed = 0.1f;

    [Tooltip("Rotate the object according to the mouse X-axis movement?")]
    [SerializeField] bool horizontalRotation = true;
    [SerializeField] float minHorizontalRotation = -360f;
    [SerializeField] float maxHorizontalRotation = 360f;

    [Tooltip("Rotate the object according to the mouse Y-axis movement?")]
    [SerializeField] bool verticalRotation = true;
    [SerializeField] float minVerticalRotation = -45f;
    [SerializeField] float maxVerticalRotation = 45f;

    [SerializeField] InputAction lookLocation = new InputAction(type: InputActionType.Value);
    void OnEnable() {        lookLocation.Enable();    }
    void OnDisable() {        lookLocation.Disable();   }
    void OnValidate() {
        // Provide default bindings for the input actions. https://gamedev.stackexchange.com/a/205345/18261
        if (lookLocation.bindings.Count == 0)
            lookLocation.AddBinding("<Mouse>/delta");
    }

    void Update() {
        Vector2 mouseDelta = lookLocation.ReadValue<Vector2>();
        Vector3 rotation = transform.localEulerAngles;

        if (horizontalRotation) {
            rotation.y = Mathf.Clamp(rotation.y + mouseDelta.x * rotationSpeed, minHorizontalRotation, maxHorizontalRotation);  // Rotation around the vertical (Y) axis
        }
        if (verticalRotation) {
            float newRotationX = rotation.x - mouseDelta.y * rotationSpeed;
            if (newRotationX > 180f)
                newRotationX -= 360f;
            float ClampedRotationX = Mathf.Clamp(newRotationX, minVerticalRotation, maxVerticalRotation);
            rotation.x = ClampedRotationX;
        }

        transform.localEulerAngles = rotation;
    }
}
