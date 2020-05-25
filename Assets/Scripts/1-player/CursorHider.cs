using UnityEngine;


/**
 * This component hides the cursor, but lets the player show it by clicking ESC.
 */
public class CursorHider: MonoBehaviour {
    void Start() {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()  {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (!Cursor.visible) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            } else {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
        }
    }
}
