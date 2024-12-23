using UnityEngine;
using UnityEngine.AI;

/**
 * Moves a NavMeshAgent to a fixed location, for demonstration
 */
[RequireComponent(typeof(NavMeshAgent))]
public class DemoMover : MonoBehaviour
{
    [SerializeField]
    Transform target;

    private NavMeshAgent agent;
    void Start() {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update() {
        agent.destination = target.position;
    }
}
