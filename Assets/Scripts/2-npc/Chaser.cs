using System;
using UnityEngine;
using UnityEngine.AI;


/**
 * This component represents an enemy NPC that chases the player.
 */
[RequireComponent(typeof(NavMeshAgent))]
public class Chaser: MonoBehaviour {

    [Tooltip("The object that this enemy chases after")] [SerializeField]
    GameObject player = null;

    [Header("These fields are for display only")]
    [SerializeField] private Vector3 playerPosition;

    private Animator animator;
    private NavMeshAgent navMeshAgent;

    private void Start() {
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    private void Update() {
        playerPosition = player.transform.position;
        float distanceToPlayer = Vector3.Distance(playerPosition, transform.position);
        FacePlayer();
        navMeshAgent.SetDestination(playerPosition);
    }

    private void FacePlayer() {
        Vector3 direction = (playerPosition - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0 , direction.z));
        // transform.rotation = lookRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5);
    }

    internal Vector3 TargetObjectPosition() {
        return player.transform.position;
    }

    private void FaceDirection() {
        Vector3 direction = (navMeshAgent.destination - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        // transform.rotation = lookRotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5);
    }

}
