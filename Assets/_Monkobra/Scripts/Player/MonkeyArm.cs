using UnityEngine;
using UnityEngine.InputSystem;

// BUG BUG arm

/// <summary>
/// Drives the arm's <see cref="ConfigurableJoint"/> in one of two modes.
/// Normal mode mimics hand-over-hand climbing by angular drive: while the
/// move action has input, the first free angular axis strokes between the
/// min and max angles (reach up, pull down), and any further free axis
/// swings a quarter cycle behind at a reduced amplitude, so the hand traces
/// a loop. The right arm runs half a cycle ahead of the left, so the two
/// alternate, and the left arm's angles are mirrored (sign flipped) from the
/// right's. Downward input plays the stroke in reverse. With no input, the
/// arm holds its current pose and resumes from there on the next input.
/// Reach mode, entered via <see cref="SetReachTarget"/>, aims the shoulder
/// at a world position and drives the joint's local Z linear axis to
/// telescope the arm toward it, up to
/// <see cref="MonkeyConfig.MaxReachDistanceU"/> past its rest length.
/// </summary>
[RequireComponent(typeof(ConfigurableJoint))]
public class MonkeyArm: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip(
        "whether this is the monkey's right arm, else the left arm, the "
            + "left arm strokes half a cycle out of phase with the right and "
            + "with every angle mirrored"
    )]
    [SerializeField]
    private bool isRightArm;

    [Tooltip(
        "directional input action, arm climbs only while it is held, a "
            + "Vector2 like the one driving MonkeyPrefabRoot"
    )]
    [SerializeField]
    private InputActionReference moveAction;

    [Tooltip("shared stroke and reach tuning")]
    [SerializeField]
    private MonkeyConfig config;

    [Tooltip(
        "child Transform to scale along its local Z axis for the "
            + "telescoping reach visual, unassigned skips the visual stretch"
    )]
    [SerializeField]
    private Transform armVisual;

    [Tooltip(
        "child Transform marking the hand's tip, its start distance from "
            + "the shoulder is read once at Awake as the arm's rest length"
    )]
    [SerializeField]
    private Transform handTip;

    // public API  #############################################################
    public bool IsReaching => isReaching;

    // switches the arm into reach mode and aims it at a world position,
    // call every frame the target should track (eg. a moving grab point)
    public void SetReachTarget(Vector3 targetWorldPos) {
        isReaching = true;
        reachTargetWorld = targetWorldPos;
    }

    // switches the arm back to normal hand-over-hand climbing
    public void CancelReach() {
        isReaching = false;
    }

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (moveAction == null) {
            Debug.LogWarning(
                "MonkeyArm:\tmust assign Inspector Field: moveAction",
                this
            );
        }
        if (config == null) {
            Debug.LogWarning(
                "MonkeyArm:\tmust assign Inspector Field: config",
                this
            );
        }
        if (armVisual == null) {
            Debug.LogWarning(
                "MonkeyArm:\tmust assign Inspector Field: armVisual",
                this
            );
        }
        if (handTip == null) {
            Debug.LogWarning(
                "MonkeyArm:\tmust assign Inspector Field: handTip",
                this
            );
        }

        joint = GetComponent<ConfigurableJoint>();
        if (joint == null) {
            Debug.LogError(
                "MonkeyArm:\tfail to get Component: " + "ConfigurableJoint",
                this
            );
        }

        // slerp drive rotates the free angular axes toward targetRotation
        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.slerpDrive = new JointDrive {
            positionSpring = config.DriveSpring,
            positionDamper = config.DriveDamper,
            maximumForce = float.MaxValue,
        };

        // local Z carries the reach: locked at rest, unlocked only while
        // actually reaching so normal-mode stroking stays rigid
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.linearLimit = new SoftJointLimit {
            limit = config.MaxReachDistanceU,
        };
        joint.zDrive = new JointDrive {
            positionSpring = config.ReachSpring,
            positionDamper = config.ReachDamper,
            maximumForce = float.MaxValue,
        };

        restLengthU = handTip != null
            ? Vector3.Distance(transform.position, handTip.position)
            : 0f;
        armVisualBaseScaleZ = armVisual != null ? armVisual.localScale.z : 1f;

        // arms alternate, so one starts half a stroke ahead of the other
        strokePhaseDeg = isRightArm ? 180f : 0f;
    }

    private void OnEnable() {
        if (moveAction != null) {
            moveAction.action.Enable();
        }
    }

    private void OnDisable() {
        if (moveAction != null) {
            moveAction.action.Disable();
        }
    }

    private void FixedUpdate() {
        if (isReaching) {
            // unlock Z only on the transition into a reach, re-assigning
            // an unchanged joint motion every frame can jolt the solver
            if (joint.zMotion != ConfigurableJointMotion.Limited) {
                joint.zMotion = ConfigurableJointMotion.Limited;
            }
            DriveReach();
            return;
        }

        // not reaching: lock the linear axis back down, no spring slack
        if (joint.zMotion != ConfigurableJointMotion.Locked) {
            joint.zMotion = ConfigurableJointMotion.Locked;
        }
        joint.targetPosition = Vector3.zero;
        if (armVisual != null) {
            armVisual.localScale = new Vector3(
                armVisual.localScale.x,
                armVisual.localScale.y,
                armVisualBaseScaleZ
            );
        }

        DriveStroke();
    }

    // constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;
    private const float SIDE_SWING_LAG_DEG = 90f;

    // private members  ########################################################
    private float strokePhaseDeg; // advances only while moving
    private bool isReaching;
    private Vector3 reachTargetWorld;
    private float restLengthU;
    private float armVisualBaseScaleZ;

    // cached references  ------------------------------------------------------
    private ConfigurableJoint joint;

    // private methods  ########################################################
    // hand-over-hand climb: angular drive strokes the free axes per input
    private void DriveStroke() {
        Vector2 directionalInput =
            moveAction != null
                ? moveAction.action.ReadValue<Vector2>()
                : Vector2.zero;
        bool isMoving = directionalInput.sqrMagnitude > INPUT_DEADZONE_SQR;

        // no input: leave targetRotation untouched so the arm holds its pose
        if (!isMoving) {
            return;
        }

        // moving down plays the stroke backward, a climb-down
        float direction = directionalInput.y < -INPUT_DEADZONE ? -1f : 1f;
        strokePhaseDeg = Mathf.Repeat(
            strokePhaseDeg
                + direction * config.StrokeSpeedDeg * Time.fixedDeltaTime,
            360f
        );

        int freeAxisIdx = 0;
        Vector3 anglesDeg = Vector3.zero;
        anglesDeg.x = CalcAxisAngleDeg(joint.angularXMotion, ref freeAxisIdx);
        anglesDeg.y = CalcAxisAngleDeg(joint.angularYMotion, ref freeAxisIdx);
        anglesDeg.z = CalcAxisAngleDeg(joint.angularZMotion, ref freeAxisIdx);
        // left arm mirrors the right, so every angle flips sign
        joint.targetRotation = Quaternion.Euler(
            isRightArm ? anglesDeg : -anglesDeg
        );
    }

    // reach: aim the shoulder at reachTargetWorld and telescope the local Z
    // linear drive out to close the remaining distance, clamped to the max
    private void DriveReach() {
        Vector3 toTargetWorld = reachTargetWorld - transform.position;
        float distanceU = toTargetWorld.magnitude;
        float extensionU = Mathf.Clamp(
            distanceU - restLengthU, 0f, config.MaxReachDistanceU
        );

        // targetRotation is relative to the connected body's rotation, so
        // undo it here to express the aim as a world-space look direction
        Quaternion connectedRotation = joint.connectedBody != null
            ? joint.connectedBody.rotation
            : Quaternion.identity;
        Quaternion worldAim = Quaternion.LookRotation(
            toTargetWorld.normalized
        );
        joint.targetRotation =
            Quaternion.Inverse(connectedRotation) * worldAim;

        joint.targetPosition = new Vector3(0f, 0f, extensionU);

        if (armVisual != null && restLengthU > 0f) {
            float stretchFactor = (restLengthU + extensionU) / restLengthU;
            armVisual.localScale = new Vector3(
                armVisual.localScale.x,
                armVisual.localScale.y,
                armVisualBaseScaleZ * stretchFactor
            );
        }
    }

    // locked axis stays at 0; 1st free axis is the main stroke, the rest are
    // a quarter cycle behind at a reduced amplitude
    private float CalcAxisAngleDeg(
        ConfigurableJointMotion motion,
        ref int freeAxisIdx
    ) {
        if (motion == ConfigurableJointMotion.Locked) {
            return 0f;
        }

        float midDeg = (config.MaxAngleDeg + config.MinAngleDeg) * 0.5f;
        float halfRangeDeg = (config.MaxAngleDeg - config.MinAngleDeg) * 0.5f;
        bool isMainStroke = freeAxisIdx == 0;
        freeAxisIdx++;

        float phaseDeg = isMainStroke
            ? strokePhaseDeg
            : strokePhaseDeg - SIDE_SWING_LAG_DEG;
        float amplitudeDeg = isMainStroke
            ? halfRangeDeg
            : halfRangeDeg * config.SideSwingRatio;
        return isMainStroke
            ? midDeg + amplitudeDeg * Mathf.Sin(phaseDeg * Mathf.Deg2Rad)
            : amplitudeDeg * Mathf.Sin(phaseDeg * Mathf.Deg2Rad);
    }
}
