using System.Collections;
using UnityEngine;


/**
 * This component moves a player using the keyboard.
 */
public class AgentKeyboardMover: MonoBehaviour {
    [Tooltip("Speed of player keyboard-movement, in meters/second")]
    [SerializeField] private float _speed = 3.5f;

    void Update()  {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(x, 0, z);
        Vector3 velocity = direction * _speed;
        velocity = transform.TransformDirection(velocity);
        transform.position += velocity * Time.deltaTime;
        //_cc.Move(velocity * Time.deltaTime);
    }
}
