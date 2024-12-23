using UnityEngine;
using UnityEngine.InputSystem;

/**
 * This component rotates its object according to the mouse movement, in a given rotation speed.
 */ 
public class InputRotator : MonoBehaviour {
    [SerializeField] float rotationSpeed = 0.1f;

    [Tooltip("Rotate the object according to the mouse X-axis movement?")]
    [SerializeField] bool horizontalRotation = true;

    [Tooltip("Rotate the object according to the mouse Y-axis movement?")]
    [SerializeField] bool verticalRotation = true;

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

        if (horizontalRotation)
            rotation.y += mouseDelta.x * rotationSpeed;  // Rotation around the vertical (Y) axis
        if (verticalRotation)
            rotation.x -= mouseDelta.y * rotationSpeed;
        transform.localEulerAngles = rotation;
    }
}
