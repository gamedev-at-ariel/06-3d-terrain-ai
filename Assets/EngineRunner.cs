using UnityEngine;
using UnityEngine.AI;


/**
 * This component represents an NPC that runs randomly between targets.
 * The targets are all the objects with a Target component.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class EngineRunner: MonoBehaviour {

    [SerializeField] Transform engine;

    private NavMeshAgent navMeshAgent;
    private Animator animator;
    private float rotationSpeed = 5f;

    private void Start() {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        SelectNewTarget();
    }

    private void SelectNewTarget() {
        navMeshAgent.SetDestination(engine.position);
    }


    private void Update() {
        if (navMeshAgent.hasPath) {
            FaceDestination();
        } 
    }

    private void FaceDestination() {
        Vector3 directionToDestination = (navMeshAgent.destination - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(directionToDestination.x, 0, directionToDestination.z));
        //transform.rotation = lookRotation; // Immediate rotation
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed); // Gradual rotation
    }
}
