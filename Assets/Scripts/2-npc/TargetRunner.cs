using UnityEngine;
using UnityEngine.AI;


/**
 * This component represents an NPC that runs randomly between targets.
 * The targets are all the objects with a Target component.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class TargetRunner: MonoBehaviour {
    [Tooltip("Minimum time to wait at target between running to the next target")]
    [SerializeField] private float minWaitAtTarget = 7f;

    [Tooltip("Maximum time to wait at target between running to the next target")]
    [SerializeField] private float maxWaitAtTarget = 15f;

    private Target[] allTargets;
    [SerializeField] private Target currentTarget = null;
    private float timeToNextTarget = 0;

    private NavMeshAgent navMeshAgent;
    private Animator animator;

    private void SelectNewTarget() {
        currentTarget = allTargets[Random.Range(0, allTargets.Length - 1)];
        navMeshAgent.SetDestination(currentTarget.transform.position);
        if (animator) animator.SetBool("Run", true);
    }


    private void Start() {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        allTargets = FindObjectsOfType<Target>();
        SelectNewTarget();
    }

    private void Update() {
        if (navMeshAgent.hasPath) {
            //FaceDirection();
            //transform.LookAt(navMeshAgent.destination);
        } else {   // we are at the target
            if (animator) animator.SetBool("Run", false);
            timeToNextTarget -= Time.deltaTime;
            if (timeToNextTarget <= 0) {
                SelectNewTarget();
                timeToNextTarget = Random.Range(minWaitAtTarget, maxWaitAtTarget);
            }
        }
    }

    private void FaceDirection() {
        Vector3 direction = (navMeshAgent.destination - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        // transform.rotation = lookRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5);
    }


}
