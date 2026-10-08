using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tilts the behind camera up while the player climbs and down while the
/// player descends. Moves the <see cref="CinemachineRotationComposer"/> aim
/// point above or below the tracked target, found on the same GameObject, in
/// proportion to vertical move input, so the camera changes its angle and
/// never its position. Leaves center fast and settles back slowly.
/// <para>
/// Reads the move action from the shared <see cref="MonkeyConfig"/> asset,
/// the same one the body and arms read. Set the composer's own Damping Y to 0,
/// this script does the smoothing.
/// </para>
/// </summary>
[RequireComponent(typeof(CinemachineRotationComposer))]
public class CameraLookUp: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("shared input tuning, supplies the move action")]
    private MonkeyConfig config;

    [SerializeField]
    [Tooltip("aim point height above the target at full up input; u")]
    private float maxLookUpOffsetU = 6f;

    [SerializeField]
    [Tooltip("aim point offset at full down input, negative is below; u")]
    private float maxLookDownOffsetU = -1f;

    [SerializeField]
    [Tooltip("how fast the aim point leaves center while moving up/down; s")]
    private float riseSmoothTimeS = 0.12f;

    [SerializeField]
    [Tooltip("how fast the aim point settles back once vertical input ends; s")]
    private float settleSmoothTimeS = 0.5f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (config == null) {
            Debug.LogWarning(
                "CameraLookUp:\tmust assign Inspector Field: config",
                this
            );
        }
        else if (config.MoveAction == null) {
            Debug.LogWarning(
                "CameraLookUp:\tmust assign MonkeyConfig field: moveAction",
                config
            );
        }

        composer = GetComponent<CinemachineRotationComposer>();
        if (composer == null) {
            Debug.LogError(
                "CameraLookUp:\tfail to get Component: "
                    + "CinemachineRotationComposer",
                this
            );
        }
    }

    private void LateUpdate() {
        if (composer == null || config == null || config.MoveAction == null) {
            return;
        }

        float verticalInput = config.MoveAction.action.ReadValue<Vector2>().y;
        float targetOffsetU =
            verticalInput >= 0f
                ? verticalInput * maxLookUpOffsetU
                : -verticalInput * maxLookDownOffsetU;
        // moving away from center rises fast, returning to center settles slow
        float smoothTimeS =
            Mathf.Abs(targetOffsetU) > Mathf.Abs(offsetU)
                ? riseSmoothTimeS
                : settleSmoothTimeS;
        offsetU = Mathf.SmoothDamp(
            offsetU,
            targetOffsetU,
            ref offsetVelocityU,
            smoothTimeS
        );

        Vector3 offset = composer.TargetOffset;
        offset.y = offsetU;
        composer.TargetOffset = offset;
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxLookUpOffsetU = Mathf.Max(0f, maxLookUpOffsetU);
        maxLookDownOffsetU = Mathf.Min(0f, maxLookDownOffsetU);
        riseSmoothTimeS = Mathf.Max(0f, riseSmoothTimeS);
        settleSmoothTimeS = Mathf.Max(0f, settleSmoothTimeS);
    }

    // private members  ########################################################
    private float offsetU; // current aim point height above target
    private float offsetVelocityU; // SmoothDamp state

    // cached references  ------------------------------------------------------
    private CinemachineRotationComposer composer;
}
