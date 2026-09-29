using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared tuning for the monkey: arm stroke and reach, climb and orbit speeds,
/// branch-hit stun, and the input actions every consumer reads. Consumed by
/// <see cref="ArmRoot"/> and <see cref="MonkeyPrefabRoot"/>.
/// <para>
/// <see cref="ArmRoot"/> takes nothing physical from here, springs, limits,
/// anchors and axes are authored on its <see cref="ConfigurableJoint"/>.
/// </para>
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

    /// <returns>stroke phase speed; deg/s, 360 is 1 stroke per s</returns>
    public float StrokeSpeedDeg => strokeSpeedDeg;

    /// <returns><see cref="StrokeSpeedDeg"/> as strokes per s, for a 0..1
    /// phase</returns>
    public float StrokeSpeedCycles => strokeSpeedDeg / 360f;

    /// <returns>speed the arm stretches out while interact is held; u/s
    /// </returns>
    public float ReachExtendSpeedU => reachExtendSpeedU;

    /// <returns>if the linear drive target is negated to push the arm
    /// outward, the usual joint axis setup</returns>
    public bool InvertDriveAxis => invertDriveAxis;

    /// <returns>resting outward lean of each arm; deg</returns>
    public float SplayDeg => splayDeg;

    /// <returns>arm cube thickness across its 2 short axes; u</returns>
    public float ArmWidthU => armWidthU;

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

    /// <returns>action held to reach for fruit, also read by the camera rig
    /// </returns>
    public InputActionReference InteractAction => interactAction;

    // Inspector Fields  #######################################################
    [Header("Arm - Stroke")]
    [SerializeField]
    [Tooltip("bottom of the stroke, arm fully pulled down, in deg")]
    private float minAngleDeg = -15f;

    [SerializeField]
    [Tooltip("top of the stroke, arm fully reached up, in deg")]
    private float maxAngleDeg = 30f;

    [SerializeField]
    [Tooltip("stroke phase speed, in deg/s, 360 is 1 full stroke per sec")]
    private float strokeSpeedDeg = 240f;

    [Header("Arm - Reach")]
    [SerializeField]
    [Tooltip("arm stretch speed while interact is held; u/s")]
    private float reachExtendSpeedU = 4f;

    [SerializeField]
    [Tooltip("uncheck if an arm telescopes inward instead of outward")]
    private bool invertDriveAxis = true;

    [Header("Arm - Climb Cycle")]
    [SerializeField]
    [Tooltip(
        "resting outward lean of each arm, keeps the 2 off each other; deg"
    )]
    private float splayDeg = 20f;

    [Header("Arm - Visual")]
    [SerializeField]
    [Tooltip(
        "thickness of the arm cube across both axes it is not stretched "
            + "along, the authored scale is overwritten every step; u"
    )]
    private float armWidthU = 0.2f;

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

    [SerializeField]
    [Tooltip(
        "action held to reach an arm out for fruit, the same action "
            + "CameraRigManager reads"
    )]
    private InputActionReference interactAction;

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxAngleDeg = Mathf.Max(minAngleDeg, maxAngleDeg);
        strokeSpeedDeg = Mathf.Max(0f, strokeSpeedDeg);
        reachExtendSpeedU = Mathf.Max(0f, reachExtendSpeedU);
        armWidthU = Mathf.Max(0.001f, armWidthU);
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
