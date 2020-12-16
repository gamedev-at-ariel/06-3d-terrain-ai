using UnityEngine;

/**
 * This component rotates its object according to the mouse movement in the X axis, in a given rotation speed.
 */ 
public class LookX : MonoBehaviour {
    [SerializeField] private float _speedRotation = 1f;

    void Update() {
        float _mouseX = Input.GetAxis("Mouse X");
        Vector3 rotation = transform.localEulerAngles;
        rotation.y += _mouseX * _speedRotation;
        transform.localEulerAngles = rotation;
    }
}
