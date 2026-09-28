using UnityEngine;

/// <summary>
/// Shared stroke and reach tuning for <see cref="MonkeyArm"/>, so both arms
/// (and any future ones) can read from one asset instead of duplicating
/// Inspector values per instance.
/// </summary>
[CreateAssetMenu(
    fileName = "MonkeyArmConfig",
    menuName = "Monkobra/Monkey Arm Config"
)]
public class MonkeyArmConfig: ScriptableObject {
    // Inspector Fields  #######################################################
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

    [Tooltip("farthest the arm can extend past its rest length, in u")]
    [SerializeField]
    private float maxReachDistanceU = 3f;

    [Tooltip("linear drive stiffness pulling the arm to its reach target")]
    [SerializeField]
    private float reachSpring = 400f;

    [Tooltip("linear drive damping, curbs oscillation on reach")]
    [SerializeField]
    private float reachDamper = 40f;

    // public API  #############################################################
    public float MinAngleDeg => minAngleDeg;
    public float MaxAngleDeg => maxAngleDeg;
    public float SideSwingRatio => sideSwingRatio;
    public float StrokeSpeedDeg => strokeSpeedDeg;
    public float DriveSpring => driveSpring;
    public float DriveDamper => driveDamper;
    public float MaxReachDistanceU => maxReachDistanceU;
    public float ReachSpring => reachSpring;
    public float ReachDamper => reachDamper;
}
