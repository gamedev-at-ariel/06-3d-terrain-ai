using UnityEngine;

/**
 * This component plays its audio-source whenever the player clicks the left mouse button.
 */
public class Weapon : MonoBehaviour {
    [SerializeField] AudioSource _audioS;

    void Start() {
        _audioS = GetComponent<AudioSource>();
    }

    void Update() {
        if (Input.GetMouseButton(0)) {
            _audioS.Play();
        }
    }

}
