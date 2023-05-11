using UnityEngine;
using UnityEngine.InputSystem;

/**
 * This component rotates its object according to the mouse movement in the Y axis, in a given rotation speed.
 */
public class LookY : MonoBehaviour { 
    [SerializeField] private float rotationSpeed = 0.1f;

    void Update() {
        float mouseY = Mouse.current.delta.y.ReadValue();
        Vector3 rotation = transform.localEulerAngles;
        rotation.x -= mouseY * rotationSpeed;
        transform.localEulerAngles = rotation;
    }
}
