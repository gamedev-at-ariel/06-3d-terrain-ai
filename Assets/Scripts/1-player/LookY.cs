using UnityEngine;

/**
 * This component rotates its object according to the mouse movement in the Y axis, in a given rotation speed.
 */
public class LookY : MonoBehaviour { 
    [SerializeField] private float _speedRotation = 1f;

    void Update() {
        float _mouseY = Input.GetAxis("Mouse Y");
        Vector3 rotation = transform.localEulerAngles;
        rotation.x -= _mouseY * _speedRotation;
        transform.localEulerAngles = rotation;
    }
}
