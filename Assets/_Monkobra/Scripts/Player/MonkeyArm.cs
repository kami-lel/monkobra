using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the arm's <see cref="ConfigurableJoint"/> by angular drive to mimic
/// hand-over-hand climbing. While the move action has input, the first free
/// angular axis strokes between the min and max angles (reach up, pull down),
/// and any further free axis swings a quarter cycle behind at a reduced
/// amplitude, so the hand traces a loop. The right arm runs half a cycle
/// ahead of the left, so the two alternate, and the left arm's angles are
/// mirrored (sign flipped) from the right's. Downward input plays the stroke
/// in reverse. With no input, the arm holds its current pose and resumes from
/// there on the next input.
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

    [Tooltip("bottom of the stroke, arm fully pulled down, in deg")]
    [SerializeField]
    private float minAngleDeg = -15f;

    [Tooltip("top of the stroke, arm fully reached up, in deg")]
    [SerializeField]
    private float maxAngleDeg = 30f;

    [Tooltip(
        "amplitude of each extra free axis as a fraction of the main "
            + "stroke, 0 keeps the hand on a straight line"
    )]
    [SerializeField]
    private float sideSwingRatio = 0.1f;

    [Tooltip("stroke phase speed, in deg/s, 360 is one full stroke per second")]
    [SerializeField]
    private float strokeSpeedDeg = 240f;

    [Tooltip("angular drive stiffness pulling the arm to its target angle")]
    [SerializeField]
    private float driveSpring = 200f;

    [Tooltip("angular drive damping, curbs oscillation around the target")]
    [SerializeField]
    private float driveDamper = 20f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (moveAction == null) {
            Debug.LogWarning(
                "MonkeyArm:\tmust assign Inspector Field: moveAction",
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
            positionSpring = driveSpring,
            positionDamper = driveDamper,
            maximumForce = float.MaxValue,
        };

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
            strokePhaseDeg + direction * strokeSpeedDeg * Time.fixedDeltaTime,
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

    // constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;
    private const float SIDE_SWING_LAG_DEG = 90f;

    // private members  ########################################################
    private float strokePhaseDeg; // advances only while moving

    // cached references  ------------------------------------------------------
    private ConfigurableJoint joint;

    // private methods  ########################################################
    // locked axis stays at 0; 1st free axis is the main stroke, the rest are
    // a quarter cycle behind at a reduced amplitude
    private float CalcAxisAngleDeg(
        ConfigurableJointMotion motion,
        ref int freeAxisIdx
    ) {
        if (motion == ConfigurableJointMotion.Locked) {
            return 0f;
        }

        float midDeg = (maxAngleDeg + minAngleDeg) * 0.5f;
        float halfRangeDeg = (maxAngleDeg - minAngleDeg) * 0.5f;
        bool isMainStroke = freeAxisIdx == 0;
        freeAxisIdx++;

        float phaseDeg = isMainStroke
            ? strokePhaseDeg
            : strokePhaseDeg - SIDE_SWING_LAG_DEG;
        float amplitudeDeg = isMainStroke
            ? halfRangeDeg
            : halfRangeDeg * sideSwingRatio;
        return isMainStroke
            ? midDeg + amplitudeDeg * Mathf.Sin(phaseDeg * Mathf.Deg2Rad)
            : amplitudeDeg * Mathf.Sin(phaseDeg * Mathf.Deg2Rad);
    }
}
