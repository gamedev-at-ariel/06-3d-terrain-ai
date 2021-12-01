using System.Collections;
using UnityEngine;


/**
 * This component moves a player controlled with a CharacterController using the keyboard.
 */
[RequireComponent(typeof(CharacterController))]
public class CharacterKeyboardMover: MonoBehaviour {
    [Tooltip("Speed of player keyboard-movement, in meters/second")]
    [SerializeField] float speed = 3.5f;
    [SerializeField] float gravity = 9.81f;

    private CharacterController cc;
    void Start() {
        cc = GetComponent<CharacterController>();
    }

    Vector3 velocity;

    void Update()  {
        if (cc.isGrounded) {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            velocity.x = x * speed;
            velocity.z = z * speed;
        } else {
            velocity.y -= gravity*Time.deltaTime;
        }

        // Move in the direction you look:
        velocity = transform.TransformDirection(velocity);

        cc.Move(velocity * Time.deltaTime);
    }
}
