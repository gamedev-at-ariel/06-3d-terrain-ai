using UnityEditorInternal;
using UnityEngine;

/**
 * This component patrols between given points, chases a given target object when it sees it, and rotates from time to time.
 */
[RequireComponent(typeof(Patroller))]
[RequireComponent(typeof(Chaser))]
[RequireComponent(typeof(Rotator))]
public class EnemyControllerStateMachine : StateMachine
{
    [SerializeField] float radiusToWatch = 5f;
    [SerializeField] float probabilityToRotate = 0.2f;
    [SerializeField] float probabilityToStopRotating = 0.2f;

    private Chaser chaser;
    private Patroller patroller;
    private Rotator rotator;

    private float DistanceToTarget() {
        return Vector3.Distance(transform.position, chaser.TargetObjectPosition());
    }

    private void Awake() {
        chaser = GetComponent<Chaser>();
        patroller = GetComponent<Patroller>();
        rotator = GetComponent<Rotator>();
        base
        .AddState(patroller)     // This would be the first active state.
        .AddState(chaser)
        .AddState(rotator)
        .AddTransition(patroller, () => DistanceToTarget() <= radiusToWatch, chaser)
        .AddTransition(rotator, () => DistanceToTarget() <= radiusToWatch, chaser)
        .AddTransition(chaser, () => DistanceToTarget() > radiusToWatch, patroller)
        .AddTransition(rotator, () => Random.Range(0f, 1f) < probabilityToStopRotating * Time.deltaTime, patroller)
        .AddTransition(patroller, () => Random.Range(0f, 1f) < probabilityToRotate * Time.deltaTime, rotator)
        ;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radiusToWatch);
    }
}
