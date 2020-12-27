using System.Collections;
using UnityEngine;


/**
 * This component moves a player controlled with a CharacterController using the keyboard.
 */
[RequireComponent(typeof(CharacterController))]
public class CharacterKeyboardMover: MonoBehaviour {
    [Tooltip("Speed of player keyboard-movement, in meters/second")]
    [SerializeField] float _speed = 3.5f;
    private float originalSpeed;
    [SerializeField] float _gravity = 9.81f;
    [SerializeField] KeyCode keyToChangeSpeed;
    [SerializeField] float fastSpeed;


    private CharacterController _cc;
    void Start() {
        _cc = GetComponent<CharacterController>();
        originalSpeed = _speed;

    }

    Vector3 velocity;

    void Update()  {
        speedChangeByKey(keyToChangeSpeed);
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        //if (x == 0 && z == 0) return;
        velocity.x = x * _speed;
        velocity.z = z * _speed;
        if (!_cc.isGrounded) {
            velocity.y -= _gravity*Time.deltaTime;
        }
        // Click Up: velocity = (0,0,1)
        velocity = transform.TransformDirection(velocity);
        //Debug.Log("velocity="+velocity+" isGrounded="+ _cc.isGrounded);
        _cc.Move(velocity * Time.deltaTime);
    }

    public void speedChangeByKey(KeyCode key){
        if(Input.GetKey(key)){
            Debug.Log("the key that pressed is "+ key);
            _speed = fastSpeed;
        }
        else{
            Debug.Log("nothing pressed");
            _speed = originalSpeed;
        }
    }
}
