using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared tuning for the monkey: the arm stroke and reach drives, the climb
/// and orbit speeds, the branch-hit stun, and the one movement action every
/// consumer reads its input from. Consumed by <see cref="ArmRoot"/> and
/// <see cref="MonkeyPrefabRoot"/>, and by the older <see cref="MonkeyArm"/>,
/// which shares the arm drive values.
/// </summary>
[CreateAssetMenu(
    fileName = "MonkeyConfig",
    menuName = "Scriptable Objects/MonkeyConfig"
)]
public class MonkeyConfig: ScriptableObject {
    // Public Members  #########################################################
    /// <returns>bottom of the stroke, arm fully pulled down, in deg</returns>
    public float MinAngleDeg => minAngleDeg;

    /// <returns>top of the stroke, arm fully reached up, in deg</returns>
    public float MaxAngleDeg => maxAngleDeg;

    /// <returns>amplitude of each extra free axis as a fraction of the main
    /// stroke, 0 keeps the hand on a straight line</returns>
    public float SideSwingRatio => sideSwingRatio;

    /// <returns>stroke phase speed, in deg/s, 360 is one full stroke per
    /// second</returns>
    public float StrokeSpeedDeg => strokeSpeedDeg;

    /// <returns>the same stroke speed as <see cref="StrokeSpeedDeg"/>, in
    /// full strokes per second, for drives that track a 0..1 phase rather
    /// than an angle</returns>
    public float StrokeSpeedCycles => strokeSpeedDeg / 360f;

    /// <returns>angular drive stiffness pulling the arm to its target
    /// angle</returns>
    public float DriveSpring => driveSpring;

    /// <returns>angular drive damping, curbs oscillation around the
    /// target</returns>
    public float DriveDamper => driveDamper;

    /// <returns>farthest the arm can extend past its rest length; u</returns>
    public float MaxReachDistanceU => maxReachDistanceU;

    /// <returns>linear drive stiffness pulling the arm to its reach
    /// target</returns>
    public float ReachSpring => reachSpring;

    /// <returns>linear drive damping, curbs oscillation on reach</returns>
    public float ReachDamper => reachDamper;

    /// <returns>extra travel past <see cref="MaxReachDistanceU"/> the joint
    /// limit allows, the room an overshoot lives in; u</returns>
    public float OvershootAllowanceU => overshootAllowanceU;

    /// <returns>whether the linear drive target has to be negated to push the
    /// arm outward, true for the usual ConfigurableJoint axis setup</returns>
    public bool InvertDriveAxis => invertDriveAxis;

    /// <returns>how far the hand pushes out at the top of a climb stroke; u
    /// </returns>
    public float CycleReachU => cycleReachU;

    /// <returns>resting outward lean of each arm, keeps the two off each
    /// other; deg</returns>
    public float SplayDeg => splayDeg;

    /// <returns>how much heavier the solver pretends the body is when an arm
    /// pulls on it, 1 lets an arm throw the body around, high values let the
    /// body drive itself and the arms follow</returns>
    public float BodyMassScale => bodyMassScale;

    /// <returns>cap on the linear reach drive's force, the most an arm can
    /// shove its own body with; N</returns>
    public float MaxReachForceN => maxReachForceN;

    /// <returns>cap on the angular aim drive's torque, the most an arm can
    /// spin its own body with; N·m</returns>
    public float MaxAimTorqueNm => maxAimTorqueNm;

    /// <returns>climb speed moving up; u/s</returns>
    public float UpSpeedU => upSpeedU;

    /// <returns>climb speed moving down; u/s</returns>
    public float DownSpeedU => downSpeedU;

    /// <returns>orbit speed around the trunk; deg/s</returns>
    public float RotationSpeedDeg => rotationSpeedDeg;

    /// <returns>ramp rate toward target velocity while input is held; u/s²
    /// </returns>
    public float AccelerationU => accelerationU;

    /// <returns>ramp rate toward target velocity while idle; u/s²</returns>
    public float DecelerationU => decelerationU;

    /// <returns>how long control is lost after a branch hit; s</returns>
    public float HitStunDurationS => hitStunDurationS;

    /// <returns>how far the monkey falls on a branch hit; u</returns>
    public float HitDropDistanceU => hitDropDistanceU;

    /// <returns>how fast the monkey falls on a branch hit; u/s</returns>
    public float HitDropSpeedU => hitDropSpeedU;

    /// <returns>directional input action every consumer reads, a Vector2
    /// where x orbits and y climbs</returns>
    public InputActionReference MoveAction => moveAction;

    // Inspector Fields  #######################################################
    [Header("Arm - Stroke")]
    [SerializeField]
    [Tooltip("bottom of the stroke, arm fully pulled down, in deg")]
    private float minAngleDeg = -15f;

    [SerializeField]
    [Tooltip("top of the stroke, arm fully reached up, in deg")]
    private float maxAngleDeg = 30f;

    [SerializeField]
    [Tooltip(
        "amplitude of each extra free axis as a fraction of the main "
            + "stroke, 0 keeps the hand on a straight line"
    )]
    private float sideSwingRatio = 0.1f;

    [SerializeField]
    [Tooltip("stroke phase speed, in deg/s, 360 is 1 full stroke per sec")]
    private float strokeSpeedDeg = 240f;

    [SerializeField]
    [Tooltip("angular drive stiffness pulling arm to target angle")]
    private float driveSpring = 200f;

    [SerializeField]
    [Tooltip("angular drive damping, curbs oscillation around target")]
    private float driveDamper = 20f;

    [Header("Arm - Reach")]
    [SerializeField]
    [Tooltip("farthest arm can extend past its rest length; u")]
    private float maxReachDistanceU = 3f;

    [SerializeField]
    [Tooltip("linear drive stiffness pulling arm to reach target")]
    private float reachSpring = 400f;

    [SerializeField]
    [Tooltip("linear drive damping, curbs oscillation on reach")]
    private float reachDamper = 40f;

    [SerializeField]
    [Tooltip(
        "extra travel past max reach the joint limit allows, the room an "
            + "overshoot lives in; u"
    )]
    private float overshootAllowanceU = 0.5f;

    [SerializeField]
    [Tooltip(
        "uncheck if an arm telescopes inward instead of outward, the linear "
            + "drive target reads inverted on some joint axis setups"
    )]
    private bool invertDriveAxis = true;

    [Header("Arm - Climb Cycle")]
    [SerializeField]
    [Tooltip("how far the hand pushes out at the top of a stroke; u")]
    private float cycleReachU = 0.6f;

    [SerializeField]
    [Tooltip(
        "resting outward lean of each arm, keeps the 2 off each other; deg"
    )]
    private float splayDeg = 20f;

    [Header("Arm - Body Isolation")]
    [SerializeField]
    [Tooltip(
        "how much heavier the solver pretends the body is when an arm pulls "
            + "on it, 1 lets an arm throw the body around, high values let "
            + "the body drive itself and the arms follow"
    )]
    private float bodyMassScale = 100f;

    [SerializeField]
    [Tooltip(
        "cap on reach drive force, the most an arm can shove its body by; N"
    )]
    private float maxReachForceN = 1000f;

    [SerializeField]
    [Tooltip(
        "cap on aim drive torque, the most an arm can spin its body by; N·m"
    )]
    private float maxAimTorqueNm = 1000f;

    [Header("Movement")]
    [SerializeField]
    [Tooltip("climb speed moving up; u/s")]
    private float upSpeedU = 6f;

    [SerializeField]
    [Tooltip("climb speed moving down; u/s")]
    private float downSpeedU = 12f;

    [SerializeField]
    [Tooltip("orbit speed around the trunk; deg/s")]
    private float rotationSpeedDeg = 50f;

    [SerializeField]
    [Tooltip("ramp rate toward target velocity while input held; u/s²")]
    private float accelerationU = 20f;

    [SerializeField]
    [Tooltip("ramp rate toward target velocity while idle; u/s²")]
    private float decelerationU = 30f;

    [Header("Branch Hit")]
    [SerializeField]
    [Tooltip("control lost after a branch hit; s")]
    private float hitStunDurationS = 0.75f;

    [SerializeField]
    [Tooltip("distance fallen on a branch hit; u")]
    private float hitDropDistanceU = 10f;

    [SerializeField]
    [Tooltip("fall speed on a branch hit; u/s")]
    private float hitDropSpeedU = 8f;

    [Header("Input")]
    [SerializeField]
    [Tooltip(
        "directional input action shared by the body and both arms, a "
            + "Vector2 where x orbits the trunk and y climbs"
    )]
    private InputActionReference moveAction;

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxAngleDeg = Mathf.Max(minAngleDeg, maxAngleDeg);
        sideSwingRatio = Mathf.Max(0f, sideSwingRatio);
        strokeSpeedDeg = Mathf.Max(0f, strokeSpeedDeg);
        driveSpring = Mathf.Max(0f, driveSpring);
        driveDamper = Mathf.Max(0f, driveDamper);
        maxReachDistanceU = Mathf.Max(0f, maxReachDistanceU);
        reachSpring = Mathf.Max(0f, reachSpring);
        reachDamper = Mathf.Max(0f, reachDamper);
        overshootAllowanceU = Mathf.Max(0f, overshootAllowanceU);
        cycleReachU = Mathf.Max(0f, cycleReachU);
        bodyMassScale = Mathf.Max(1f, bodyMassScale);
        maxReachForceN = Mathf.Max(0f, maxReachForceN);
        maxAimTorqueNm = Mathf.Max(0f, maxAimTorqueNm);
        upSpeedU = Mathf.Max(0f, upSpeedU);
        downSpeedU = Mathf.Max(0f, downSpeedU);
        rotationSpeedDeg = Mathf.Max(0f, rotationSpeedDeg);
        accelerationU = Mathf.Max(0f, accelerationU);
        decelerationU = Mathf.Max(0f, decelerationU);
        hitStunDurationS = Mathf.Max(0f, hitStunDurationS);
        hitDropDistanceU = Mathf.Max(0f, hitDropDistanceU);
        hitDropSpeedU = Mathf.Max(0f, hitDropSpeedU);
    }
}
