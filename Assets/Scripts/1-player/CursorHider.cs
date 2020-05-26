using System.Collections;
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
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
