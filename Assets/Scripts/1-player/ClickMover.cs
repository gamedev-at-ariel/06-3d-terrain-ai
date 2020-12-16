using UnityEngine;
using UnityEngine.AI;


/**
 * This component sends its object to a point in the world whenever the player clicks on that point.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class ClickMover : MonoBehaviour {

    [Tooltip("0 = left mouse button; 1 = right mouse button")]
    [Range(0, 1)]
    [SerializeField] int mouseButtonToClick = 0;

    [SerializeField] bool drawRayForDebug = true;
    [SerializeField] float rayLength = 100f;
    [SerializeField] float rayDuration = 1f;
    [SerializeField] Color rayColor = Color.white;

    private NavMeshAgent agent;
    void Start() {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update() {
        if (Input.GetMouseButton(mouseButtonToClick)) {
            Ray rayFromCameraToClickPosition = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (drawRayForDebug)
                Debug.DrawRay(rayFromCameraToClickPosition.origin, rayFromCameraToClickPosition.direction * rayLength, rayColor, rayDuration);
            
            RaycastHit hitInfo;
            bool hasHit = Physics.Raycast(rayFromCameraToClickPosition, out hitInfo);
            if (hasHit) {
                agent.SetDestination(hitInfo.point);
            }
        }
    }
}
