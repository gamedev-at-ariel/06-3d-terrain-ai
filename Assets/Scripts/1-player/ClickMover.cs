using UnityEngine;
using UnityEngine.AI;


/**
 * This component sends its object to a point in the world whenever the player clicks on that point.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class ClickMover: MonoBehaviour {

    [Tooltip("0 = left mouse button; 1 = right mouse button")] [Range(0, 1)]
    [SerializeField] int mouseButtonToClick;

    [SerializeField] bool drawRayForDebug = true;

    private NavMeshAgent agent;
    void Start() {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update() {
        if (Input.GetMouseButton(mouseButtonToClick)) {
            Ray rayFromCameraToClickPosition = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (drawRayForDebug)
                Debug.DrawRay(rayFromCameraToClickPosition.origin, rayFromCameraToClickPosition.direction * 100, Color.white, 1);

            RaycastHit hitInfo;
            bool hasHit = Physics.Raycast(rayFromCameraToClickPosition, out hitInfo);
            if (hasHit) {
                agent.SetDestination(hitInfo.point);
            }
        }
    }
}
