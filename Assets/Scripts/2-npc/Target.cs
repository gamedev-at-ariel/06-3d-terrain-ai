using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * This component denotes a target that can be shown on the scene editor with a red sphere.
 */
public class Target : MonoBehaviour {
    [SerializeField] float sphereRadius = 0.3f;
    private void OnDrawGizmos() {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, sphereRadius);
    }
}
