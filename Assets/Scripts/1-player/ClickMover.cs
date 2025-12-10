using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;


/**
 * This component sends its object to a point in the world whenever the player clicks on that point.
 * 
 * NOTE: To use this componenet, the CharacterController components should be disabled.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class ClickMover : MonoBehaviour {
    [SerializeField] bool drawRayForDebug = true;
    [SerializeField] float rayLength = 100f;
    [SerializeField] float rayDuration = 1f;
    [SerializeField] Color rayColor = Color.white;

    [SerializeField] InputAction moveTo = new InputAction(type: InputActionType.Button);
    [SerializeField] InputAction moveToLocation = new InputAction(type: InputActionType.Value, expectedControlType: "Vector2");
    void OnEnable() {
        moveTo.Enable();
        moveToLocation.Enable();
    }
    void OnDisable() {
        moveTo.Disable();
        moveToLocation.Disable();
    }
    void OnValidate() {
        // Provide default bindings for the input actions. Based on  https://gamedev.stackexchange.com/a/205345/18261
        if (moveTo.bindings.Count == 0)
            moveTo.AddBinding("<Mouse>/leftButton");
        if (moveToLocation.bindings.Count == 0)
            moveToLocation.AddBinding("<Mouse>/position");
    }



    private NavMeshAgent agent;
    void Start() {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update() {
        if (moveTo.WasPerformedThisFrame()) {
            Vector2 mousePositionInScreenCoordinates = moveToLocation.ReadValue<Vector2>();
            // Debug.Log("moving to screen coordinates: " + mousePositionInScreenCoordinates);
            Ray rayFromCameraToClickPosition = Camera.main.ScreenPointToRay(mousePositionInScreenCoordinates);
            
            if (drawRayForDebug)
                Debug.DrawRay(rayFromCameraToClickPosition.origin, rayFromCameraToClickPosition.direction * rayLength, rayColor, rayDuration);
            
            RaycastHit hitInfo;
            bool hasHit = Physics.Raycast(rayFromCameraToClickPosition, out hitInfo);
            if (hasHit) {
                agent.SetDestination(hitInfo.point);
                //agent.destination = hitInfo.point;
            }
        }
    }
}
