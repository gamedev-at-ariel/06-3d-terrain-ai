using UnityEngine;

/**
 * This component rotates its object according to the mouse movement in the X axis, in a given rotation speed.
 */ 
public class LookX : MonoBehaviour {
    [SerializeField] private float rotationSpeed = 1f;

    void Update() {
        float mouseX = Input.GetAxis("Mouse X");
        //Debug.Log("mouse x = " + _mouseX);
        Vector3 rotation = transform.localEulerAngles;
        rotation.y += mouseX * rotationSpeed;  // Rotation around the vertical (Y) axis
        transform.localEulerAngles = rotation;
    }
}
