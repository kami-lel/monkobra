using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the 3 camera views under the monkey's <c>CameraRig</c> and swaps
/// which one is live. Defaults to behind. While the interact action is
/// held, switches to look-left/look-right based on
/// <see cref="UpwardFruitDetection"/> (right only if fruit is reachable
/// there and not on the left; left otherwise, including when neither side
/// has reachable fruit). Releasing the action returns to behind. Swap is
/// done by raising the live camera's
/// <see cref="CinemachineCamera.Priority"/> above the other two. The
/// <c>CinemachineBrain</c> on the main camera handles the actual blend.
/// </summary>
public class CameraRigManager: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip("view tracking the monkey from behind")]
    [SerializeField]
    private CinemachineCamera behindCamera;
    [Tooltip("view tracking the monkey while fruit is reachable on the left")]
    [SerializeField]
    private CinemachineCamera lookLeftCamera;
    [Tooltip("view tracking the monkey while fruit is reachable on the right")]
    [SerializeField]
    private CinemachineCamera lookRightCamera;
    [Tooltip("source of ReachForLeft/ReachForRight driving the camera swap")]
    [SerializeField]
    private UpwardFruitDetection fruitDetection;
    [Tooltip("action that must be held for the camera to leave behind view")]
    [SerializeField]
    private InputActionReference interactAction;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (behindCamera == null) {
            Debug.LogWarning("must assign Inspector Field: behindCamera",
                              this);
        }
        if (lookLeftCamera == null) {
            Debug.LogWarning("must assign Inspector Field: lookLeftCamera",
                              this);
        }
        if (lookRightCamera == null) {
            Debug.LogWarning("must assign Inspector Field: lookRightCamera",
                              this);
        }
        if (fruitDetection == null) {
            Debug.LogWarning("must assign Inspector Field: fruitDetection",
                              this);
        }
        if (interactAction == null) {
            Debug.LogWarning("must assign Inspector Field: interactAction",
                              this);
        }

        activeView = CameraView.Behind;
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

    private void Update() {
        if (!isInteractHeld) {
            return;
        }

        UpdateActiveView();
    }

    // Event Handlers  #########################################################
    private void OnInteractPerformed(InputAction.CallbackContext context) {
        isInteractHeld = true;
        UpdateActiveView();
    }

    private void OnInteractCanceled(InputAction.CallbackContext context) {
        isInteractHeld = false;
        UpdateActiveView();
    }

    // constants  ##############################################################
    private const int LIVE_PRIORITY = 20;
    private const int STANDBY_PRIORITY = 10;

    // private members  ########################################################
    private enum CameraView {
        Behind,
        Left,
        Right
    }

    private CameraView activeView;
    private bool isInteractHeld;

    // private methods  ########################################################
    private void UpdateActiveView() {
        CameraView desiredView = !isInteractHeld ? CameraView.Behind
            : fruitDetection.ReachForRight && !fruitDetection.ReachForLeft
                ? CameraView.Right
                : CameraView.Left;

        if (desiredView == activeView) {
            return;
        }

        activeView = desiredView;
        ApplyPriorities();

        if (activeView == CameraView.Left) {
            Debug.Log("look-left camera activated", this);
        } else if (activeView == CameraView.Right) {
            Debug.Log("look-right camera activated", this);
        }

        if (Debug.isDebugBuild) {
            Debug.Log($"camera view switched, activeView={activeView}");
        }
    }

    private void ApplyPriorities() {
        behindCamera.Priority = new PrioritySettings {
            Enabled = true,
            Value = activeView == CameraView.Behind
                ? LIVE_PRIORITY
                : STANDBY_PRIORITY,
        };
        lookLeftCamera.Priority = new PrioritySettings {
            Enabled = true,
            Value = activeView == CameraView.Left
                ? LIVE_PRIORITY
                : STANDBY_PRIORITY,
        };
        lookRightCamera.Priority = new PrioritySettings {
            Enabled = true,
            Value = activeView == CameraView.Right
                ? LIVE_PRIORITY
                : STANDBY_PRIORITY,
        };
    }
}
