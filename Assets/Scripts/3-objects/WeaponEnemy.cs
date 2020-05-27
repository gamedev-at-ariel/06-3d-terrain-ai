using UnityEngine;

/**
 * This component plays its audio-source whenever the enemy is shooting.
 */
public class WeaponEnemy : MonoBehaviour {
    AudioSource _audioS;

    [SerializeField] Enemy _enemy = null;

    void Start() {
        _audioS = GetComponent<AudioSource>();
    }

    void Update() {
        if (_enemy.IsShooting()){
            _audioS.Play();
        }
    }
}
