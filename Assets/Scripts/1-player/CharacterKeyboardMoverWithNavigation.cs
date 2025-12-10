using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NavMeshAgent))]
public class CharacterKeyboardMoverWithNavigation : MonoBehaviour {
    [Tooltip("Speed of player keyboard-movement, in meters/second")]
    [SerializeField] float speed = 3.5f;
    [SerializeField] float gravity = 9.81f;
    [SerializeField] float standingSpeed = -0.01f;

    protected CharacterController cc;
    protected NavMeshAgent agent;

    [SerializeField] InputAction moveAction = new InputAction(type: InputActionType.Button);
    private void OnEnable() { moveAction.Enable(); }
    private void OnDisable() { moveAction.Disable(); }
    void OnValidate()
    {
        // Provide default bindings for the input actions.
        // Based on answer by DMGregory: https://gamedev.stackexchange.com/a/205345/18261
        if (moveAction.bindings.Count == 0)
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
    }

    void Start()
    {
        cc = GetComponent<CharacterController>();
            
    }

    [SerializeField]
    Vector3 velocity = new Vector3(0, 0, 0);

    void Update()
    {
        Vector3 movement = moveAction.ReadValue<Vector2>(); // Implicitly convert Vector2 to Vector3, setting z=0.
        if (movement.x == 0 && movement.y == 0)
        {
            if (agent.remainingDistance > agent.stoppingDistance)
            {
                // move using the NavMeshAgent
                Vector3 velocity = agent.desiredVelocity;
                cc.Move(velocity * Time.deltaTime);
                agent.nextPosition = transform.position;
            }
        }
        else
        {
            // move using the CharacterController
            if (cc.isGrounded)
            {
                velocity.x = movement.x * speed;
                velocity.z = movement.y * speed;
                velocity = transform.TransformDirection(velocity); // Move in the direction you look:
                velocity.y = standingSpeed;
            } else
            {
                velocity.y -= gravity * Time.deltaTime;
            }

            cc.Move(velocity * Time.deltaTime);
        }
    }
}
