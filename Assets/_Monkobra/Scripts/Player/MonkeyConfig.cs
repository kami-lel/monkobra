using UnityEngine;

/// <summary>
/// Shared tuning for the monkey. Currently covers only
/// <see cref="MonkeyArm"/>'s stroke and reach drives.
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
    }
}
