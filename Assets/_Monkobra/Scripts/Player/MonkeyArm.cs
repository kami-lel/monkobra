using UnityEngine;

/// <summary>
/// Drives the arm's <see cref="ConfigurableJoint"/> by angular drive: the arm
/// sweeps back and forth between the min and max angles at a constant speed,
/// never resting.
/// </summary>
[RequireComponent(typeof(ConfigurableJoint))]
public class MonkeyArm: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip("one end of the sweep, in deg around the joint axis")]
    [SerializeField]
    private float minAngleDeg = -60f;
    [Tooltip("other end of the sweep, in deg around the joint axis")]
    [SerializeField]
    private float maxAngleDeg = 120f;
    [Tooltip("constant sweep speed, in deg/s")]
    [SerializeField]
    private float sweepSpeedDeg = 180f;
    [Tooltip("angular drive stiffness pulling the arm to its target angle")]
    [SerializeField]
    private float driveSpring = 200f;
    [Tooltip("angular drive damping, curbs oscillation around the target")]
    [SerializeField]
    private float driveDamper = 20f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        joint = GetComponent<ConfigurableJoint>();
        if (joint == null) {
            Debug.LogError("MonkeyArm:\tfail to get Component: "
                           + "ConfigurableJoint", this);
        }

        // slerp drive rotates the free angular axes toward targetRotation
        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.slerpDrive = new JointDrive {
            positionSpring = driveSpring,
            positionDamper = driveDamper,
            maximumForce = float.MaxValue,
        };

        if (Debug.isDebugBuild) {
            Debug.Log("MonkeyArm:\tsweep start");
        }
    }

    private void FixedUpdate() {
        // ping-pong keeps the speed constant, flipping direction at each end
        float sweepTravelDeg = sweepSpeedDeg * Time.time;
        float angleDeg = minAngleDeg
                         + Mathf.PingPong(sweepTravelDeg,
                                          maxAngleDeg - minAngleDeg);
        joint.targetRotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    // cached references  ------------------------------------------------------
    private ConfigurableJoint joint;
}
