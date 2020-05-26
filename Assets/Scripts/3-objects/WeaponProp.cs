using UnityEngine;


/**
 * This component adds ammunition to the player whenever the player triggers it and clicks E.
 */
public class WeaponProp: MonoBehaviour {
    private void OnTriggerStay(Collider other) {
        if (other.tag == "Player") {
            if (Input.GetKeyDown(KeyCode.E)){
                Shooter shooter = other.GetComponent<Shooter>();
                if(shooter != null) {
                    shooter._addAmmo();
                    Destroy(this.gameObject);
                }
            }
        }
    }
}
