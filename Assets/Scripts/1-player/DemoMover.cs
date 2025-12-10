using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/**
 * Moves a NavMeshAgent to a fixed location, for demonstration
 */
[RequireComponent(typeof(NavMeshAgent))]
public class DemoMover : MonoBehaviour
{
    [SerializeField]
    Transform target;

    [SerializeField] InputAction clickAction = new InputAction(type: InputActionType.Button);
    private void OnEnable() { clickAction.Enable(); }
    private void OnDisable() { clickAction.Disable(); }
    void OnValidate() {
        // Provide default bindings for the input actions.
        // Based on answer by DMGregory: https://gamedev.stackexchange.com/a/205345/18261
        if (clickAction.bindings.Count == 0)
            clickAction.AddBinding("<Mouse>/leftButton");
    }


    private NavMeshAgent agent;
    void Start() {
        agent = GetComponent<NavMeshAgent>();
        //agent.Warp(target.position);
        agent.SetDestination(target.position);
        agent.isStopped = false;
    }

    private void Update() {
        if (clickAction.WasPressedThisFrame())
        {
            agent.SetDestination(target.position);
        }
    }
}
