using UnityEngine;
using UnityEngine.InputSystem;

/**
 * This component rotates its object according to the mouse movement in the X axis, in a given rotation speed.
 */ 
public class LookX : MonoBehaviour {
    [SerializeField] private float rotationSpeed = 0.1f;

    /*
    [SerializeField] InputAction lookLocation;
    void OnEnable() {        lookLocation.Enable();    }
    void OnDisable() {        lookLocation.Disable();   }
    void OnValidate() {
        // Provide default bindings for the input actions.
        // Based on answer by DMGregory: https://gamedev.stackexchange.com/a/205345/18261
        if (lookLocation == null)
            lookLocation = new InputAction(type: InputActionType.Value);
        if (lookLocation.bindings.Count == 0)
            lookLocation.AddBinding("<Mouse>/x");
    }
    */

    void Update() {
        float mouseX = Mouse.current.delta.x.ReadValue();
        Vector3 rotation = transform.localEulerAngles;
        rotation.y += mouseX * rotationSpeed;  // Rotation around the vertical (Y) axis
        transform.localEulerAngles = rotation;
    }
}
