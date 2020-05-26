using System.Collections;
using UnityEngine;


/**
 * This component moves a player controlled with a CharacterController using the keyboard.
 */
[RequireComponent(typeof(CharacterController))]
public class KeyboardMover: MonoBehaviour {
    [Tooltip("Speed of player keyboard-movement, in meters/second")]
    [SerializeField] private float _speed = 3.5f;

    private CharacterController _cc;
    private float _gravity = 9.81f;
    void Start() {
        _cc = GetComponent<CharacterController>();
    }

    void Update()  {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(x, 0, z);
        Vector3 velocity = direction * _speed;
        if (!_cc.isGrounded) {
            velocity.y -= _gravity*Time.deltaTime;
        }
        velocity = transform.TransformDirection(velocity);
        _cc.Move(velocity * Time.deltaTime);
    }
}
