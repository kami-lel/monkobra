using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the 2 camera views under the monkey's <c>CameraRig</c> and swaps
/// which one is live while the interact action is held, by raising its
/// <see cref="CinemachineCamera.Priority"/> above the other. Side view is
/// live only while held, releasing returns to behind. The
/// <c>CinemachineBrain</c> on the main camera handles the actual blend.
/// </summary>
public class CameraRigManager: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip("view tracking the monkey from behind")]
    [SerializeField]
    private CinemachineCamera behindCamera;
    [Tooltip("view tracking the monkey from the side")]
    [SerializeField]
    private CinemachineCamera sideCamera;
    [Tooltip("action that toggles which camera view is live")]
    [SerializeField]
    private InputActionReference interactAction;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (behindCamera == null) {
            Debug.LogWarning("must assign Inspector Field: behindCamera",
                              this);
        }
        if (sideCamera == null) {
            Debug.LogWarning("must assign Inspector Field: sideCamera", this);
        }
        if (interactAction == null) {
            Debug.LogWarning("must assign Inspector Field: interactAction",
                              this);
        }

        ApplyPriorities();
    }

    private void OnEnable() {
        interactAction.action.Enable();
        interactAction.action.performed += OnInteractPerformed;
        interactAction.action.canceled += OnInteractCanceled;
    }

    private void OnDisable() {
        interactAction.action.performed -= OnInteractPerformed;
        interactAction.action.canceled -= OnInteractCanceled;
        interactAction.action.Disable();
    }

    // Event Handlers  #########################################################
    private void OnInteractPerformed(InputAction.CallbackContext context) {
        SetSideActive(true);
    }

    private void OnInteractCanceled(InputAction.CallbackContext context) {
        SetSideActive(false);
    }

    // constants  ##############################################################
    private const int LIVE_PRIORITY = 20;
    private const int STANDBY_PRIORITY = 10;

    // private members  ########################################################
    private bool isSideActive;

    // private methods  ########################################################
    private void SetSideActive(bool active) {
        isSideActive = active;
        ApplyPriorities();

        if (Debug.isDebugBuild) {
            Debug.Log($"camera view switched, isSideActive={isSideActive}");
        }
    }

    private void ApplyPriorities() {
        behindCamera.Priority = new PrioritySettings {
            Enabled = true,
            Value = isSideActive ? STANDBY_PRIORITY : LIVE_PRIORITY,
        };
        sideCamera.Priority = new PrioritySettings {
            Enabled = true,
            Value = isSideActive ? LIVE_PRIORITY : STANDBY_PRIORITY,
        };
    }
}
