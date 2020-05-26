using UnityEngine;
using UnityEngine.AI;


/**
 * This component sends its object to a point in the world whenever the player right-clicks on that point.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class ClickMover: MonoBehaviour {

    private NavMeshAgent agent;
    void Start() {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update() {
        if (Input.GetMouseButtonDown(1)) {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitInfo;
            Physics.Raycast(ray, out hitInfo);
            agent.SetDestination(hitInfo.point);
            Debug.Log("Right button clicked. Hit point "+ hitInfo.point);
        }
    }
}
