using System.Collections;
using UnityEngine;


/**
 * This component represents a player that can shoot using left-click and reload using R.
 * The player has limited ammunition.
 */
public class Shooter: MonoBehaviour {
    [Tooltip("Particle-effect that is triggered near the weapon mouth when the player shoots")]
    [SerializeField] private GameObject muzzleFlash = null;

    [Tooltip("Particle-effect that is triggered where the player's bullet hits")]
    [SerializeField] private GameObject bulletHole = null;

    [Tooltip("How many bullets the player initially has")]
    [SerializeField] private int startAmmo = 50;

    [Tooltip("How many bullets the player currently has")]
    [SerializeField] private int ammo;

    private bool _isReloading;

    void Start() {
        ammo=startAmmo;
        muzzleFlash.SetActive(false);
    }

    void Update()  {
        if (Input.GetMouseButtonDown(0)) {
            if (ammo>0) {
                Shoot();
            } else {
                Debug.Log("Out of ammunition!");
            }
        }
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading) {
            _isReloading = true;
            Reload();
        }
    }

    private void Shoot() {
        muzzleFlash.SetActive(true);
        StartCoroutine(StopEffect());
        //Debug.Log("Shooting");

        Ray rayOrigin = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hitInfo;
        if (Physics.Raycast(rayOrigin, out hitInfo)) {
            GameObject hitMarker = Instantiate(bulletHole, hitInfo.point, Quaternion.LookRotation(hitInfo.normal));
            Destroy(hitMarker, 1f);
            if (hitInfo.collider.tag == "Enemy") {
                Debug.Log("Enemy is hit! You win!");
            }
        }
        ammo--;
    }

    IEnumerator StopEffect() {
        yield return new WaitForSeconds(0.3f);
        muzzleFlash.SetActive(false);
    }

    private void Reload() {
        StartCoroutine(ShootingCoolDownRoutine());
    }

    IEnumerator ShootingCoolDownRoutine() {
        yield return new WaitForSeconds(1.5f);
        ammo = startAmmo;
        _isReloading = false;
    }

    IEnumerator CoolDownRoutine() {
        yield return new WaitForSeconds(5.0f);
    }

    public void _addAmmo() {
        ammo = startAmmo;
    }
}
