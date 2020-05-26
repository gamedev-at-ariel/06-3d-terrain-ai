using System.Collections;
using UnityEngine;


/**
 * This component represents a player controlled with a CharacterController. Controls: 
 * Left-click = shooot; 
 * R = reload;
 * ESC = Show cursor
 */
public class Player : MonoBehaviour {
    [Tooltip("Speed of player keyboard-movement, in meters/second")]
    [SerializeField] private float _speed = 3.5f;

    [Tooltip("Particle-effect that is triggered near the weapon mouth when the player shoots")]
    [SerializeField] private GameObject _muzzleFlash;

    [Tooltip("Particle-effect that is triggered where the player's bullet hits")]
    [SerializeField] private GameObject _bulletHole;

    [Tooltip("How many bullets the player currently has")]
    [SerializeField] private int _ammo;

    private CharacterController _cc;
    private float _gravity = 9.81f;
    private int _startAmmo=50;
    private bool _isReloading;

    void Start() {
        _cc = GetComponent<CharacterController>();
        _ammo=_startAmmo;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()  {
        if (_ammo>0) {
            if (Input.GetMouseButton(0)) {
                Shoot();
            }
            _muzzleFlash.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading) {
            _isReloading = true;
            Reload();
        }
        if (Input.GetKeyDown(KeyCode.Escape)) {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        MovementCalc();
    }

    private void Shoot() {
        _muzzleFlash.SetActive(true);
        Debug.Log("ey");
        _ammo--;

        //position ray casted from 
        Ray rayOrigin = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hitInfo;
        if (Physics.Raycast(rayOrigin, out hitInfo)) {
            if(hitInfo.collider.tag == "Enemy") {
                // pass
            } else { 
                GameObject hitMarker = Instantiate(_bulletHole, hitInfo.point, Quaternion.LookRotation(hitInfo.normal));
                Destroy(hitMarker, 1f);
            }
        }
    }

    void MovementCalc() {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 direction = new Vector3(x, 0, z);
        Vector3 velocity = direction * _speed;
        if (!_cc.isGrounded) {
            velocity.y -= _gravity*Time.deltaTime;
        }
        velocity = transform.TransformDirection(velocity);
        Debug.Log("velocity=" + velocity);
        _cc.Move(velocity * Time.deltaTime);
    }

    private void Reload() {
        StartCoroutine(ShootingCoolDownRoutine());
    }

    IEnumerator ShootingCoolDownRoutine() {
        yield return new WaitForSeconds(1.5f);
        _ammo = _startAmmo;
        _isReloading = false;
    }

    IEnumerator CoolDownRoutine() {
        yield return new WaitForSeconds(5.0f);
    }

    public void _addAmmo() {
        _ammo = _startAmmo;
    }
}
