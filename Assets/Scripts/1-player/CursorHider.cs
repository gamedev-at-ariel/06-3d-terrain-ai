using UnityEngine;
using UnityEngine.InputSystem;

/**
 * This component lets the player show/hide the cursor by clicking ESC.
 * Initially, it hides the cursor.
 */
public class CursorHider : MonoBehaviour {
    [SerializeField] InputAction toggleCursorAction = new InputAction(type: InputActionType.Button);
    void OnEnable() { toggleCursorAction.Enable(); }
    void OnDisable() { toggleCursorAction.Disable(); }
    void OnValidate() {
        // Provide default bindings for the input actions: https://gamedev.stackexchange.com/a/205345/18261
        if (toggleCursorAction.bindings.Count == 0)
            toggleCursorAction.AddBinding("<Mouse>/leftButton");
    }


    void Start() {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update() {
        if (toggleCursorAction.WasPerformedThisFrame()) {
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
