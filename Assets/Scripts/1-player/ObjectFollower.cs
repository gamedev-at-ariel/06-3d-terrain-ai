using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * This component lets its object follow the position of a given target object, without following its rotation.
 * Useful mainly for 3rd-person cameras.
 */
public class ObjectFollower : MonoBehaviour {
    [SerializeField] Transform followedObject = null;


    void LateUpdate() {
        // LateUpdate happens after Update. 
        // We use LateUpdate here because we want the camera to move
        // only after the player has finished moving (using NavMeshAgent).
        transform.position = followedObject.position;
    }
}
