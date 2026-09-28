using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prefab root of one arm. This GameObject carries the arm's only
/// <see cref="Rigidbody"/> and the <see cref="ConfigurableJoint"/> tying it
/// to the monkey's body, and it sits at the <em>hand</em> end of the arm: the
/// joint's local Z linear drive telescopes it away from the shoulder, so this
/// transform's position is the hand's position and local +Z always points
/// away from the shoulder.
/// <para>
/// Two children hang off it. The arm visual is a renderer-only cube stretched
/// along local Z every step to span shoulder to hand; scaling lives on that
/// child so no collider or joint ever gets a scale written to it. The hand is
/// a collider-only child at this transform's origin, part of this Rigidbody's
/// compound collider, so its collisions arrive here and are reported through
/// <see cref="IsHandTouching"/> and <see cref="HandContacts"/>.
/// </para>
/// <para>
/// The arm runs in one of two modes. Reach mode, entered through
/// <see cref="SetReachTarget"/>, aims local +Z at a tracked Transform (read
/// fresh every step, so a swaying branch is followed w/o re-issuing the call)
/// and drives the linear axis out to close the gap. That drive is a spring,
/// so an underdamped damper lets the hand overshoot the target and settle
/// back, and the joint's linear limit leaves an overshoot allowance past max
/// reach for the overshoot to live in. Otherwise the arm climbs: while the
/// move action has input, a cycle phase advances at a rate scaled by input
/// magnitude and drives a reach-out / pull-down stroke on the linear and
/// angular drives a quarter cycle apart, so the hand traces a loop. The right
/// arm runs half a cycle out of phase with the left, so the two alternate.
/// Downward input plays the stroke in reverse; w/o input the phase freezes and
/// the arm holds its pose.
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ConfigurableJoint))]
public class ArmRoot: MonoBehaviour {
    // Public Members  #########################################################
    /// <returns>whether the arm is reaching for a target, else climbing
    /// </returns>
    public bool IsReaching => isReaching;

    /// <returns>whether the hand is touching anything at all right now
    /// </returns>
    public bool IsHandTouching {
        get {
            PruneHandContacts();
            return handContacts.Count > 0;
        }
    }

    /// <returns>every collider the hand is touching right now, a live view,
    /// do not hold onto it across frames</returns>
    public IReadOnlyCollection<Collider> HandContacts {
        get {
            PruneHandContacts();
            return handContacts;
        }
    }

    /// <returns>shoulder to hand distance at Awake, the arm's unstretched
    /// length; u</returns>
    public float RestLengthU => restLengthU;

    /// <returns>shoulder to hand distance right now, rest length plus
    /// whatever the linear drive has telescoped out; u</returns>
    public float CurrentLengthU => shoulder != null
        ? Vector3.Distance(shoulder.position, transform.position)
        : restLengthU;

    // Public Methods  #########################################################
    /// <summary>
    /// Switches the arm into reach mode and tracks <paramref name="target"/>'s
    /// world position every step until <see cref="CancelReach"/>. A null
    /// target cancels the reach.
    /// </summary>
    public void SetReachTarget(Transform target) {
        isReaching = target != null;
        reachTarget = target;
    }

    /// <summary>Switches the arm back to climbing.</summary>
    public void CancelReach() {
        isReaching = false;
        reachTarget = null;
    }

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip("the monkey's body, this arm's joint hangs off it")]
    private Rigidbody bodyRigidbody;

    [SerializeField]
    [Tooltip(
        "empty child of the body marking where this arm pivots, both the "
            + "joint anchor and the near end of the arm visual"
    )]
    private Transform shoulder;

    [SerializeField]
    [Tooltip(
        "renderer-only cube child, scaled along its local Z to span "
            + "shoulder to hand"
    )]
    private Transform armVisual;

    [SerializeField]
    [Tooltip(
        "collider-only hand child, only its contacts count as hand contacts"
    )]
    private Collider handCollider;

    [Header("Identity")]
    [SerializeField]
    [Tooltip(
        "whether this is the monkey's right arm, else the left arm, the two "
            + "climb half a cycle out of phase and splay to opposite sides"
    )]
    private bool isRightArm;

    [Header("Input")]
    [SerializeField]
    [Tooltip(
        "directional input action, arm climbs only while it is held, a "
            + "Vector2 like the one driving MonkeyPrefabRoot"
    )]
    private InputActionReference moveAction;

    [Header("Reach")]
    [SerializeField]
    [Tooltip("farthest the arm aims past its rest length; u")]
    private float maxReachDistanceU = 3f;

    [SerializeField]
    [Tooltip(
        "extra travel past max reach the joint limit allows, the room the "
            + "overshoot lives in; u"
    )]
    private float overshootAllowanceU = 0.5f;

    [SerializeField]
    [Tooltip("linear drive stiffness telescoping the arm toward its target")]
    private float reachSpring = 400f;

    [SerializeField]
    [Tooltip("linear drive damping, lower overshoots the target further")]
    private float reachDamper = 15f;

    [SerializeField]
    [Tooltip(
        "flip if the arm telescopes inward instead of outward, the linear "
            + "drive target reads inverted on some joint axis setups"
    )]
    private bool invertDriveAxis = true;

    [Header("Climb Cycle")]
    [SerializeField]
    [Tooltip("stroke rate at full input; cycles/s")]
    private float cycleSpeed = 1.2f;

    [SerializeField]
    [Tooltip("how far the hand pushes out at the top of a stroke; u")]
    private float cycleReachU = 0.6f;

    [SerializeField]
    [Tooltip("how far the arm sweeps up and down over a stroke; deg")]
    private float cycleSwingDeg = 25f;

    [SerializeField]
    [Tooltip("resting outward lean, keeps the two arms off each other; deg")]
    private float splayDeg = 20f;

    [Header("Aim")]
    [SerializeField]
    [Tooltip("angular drive stiffness pulling the arm to its target angle")]
    private float aimSpring = 200f;

    [SerializeField]
    [Tooltip("angular drive damping, curbs oscillation around the target")]
    private float aimDamper = 20f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (bodyRigidbody == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign Inspector Field: bodyRigidbody", this
            );
        }
        if (shoulder == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign Inspector Field: shoulder", this
            );
        }
        if (armVisual == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign Inspector Field: armVisual", this
            );
        }
        if (handCollider == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign Inspector Field: handCollider", this
            );
        }
        if (moveAction == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign Inspector Field: moveAction", this
            );
        }

        body = GetComponent<Rigidbody>();
        if (body == null) {
            Debug.LogError("ArmRoot:\tfail to get Component: Rigidbody", this);
        }

        joint = GetComponent<ConfigurableJoint>();
        if (joint == null) {
            Debug.LogError(
                "ArmRoot:\tfail to get Component: ConfigurableJoint", this
            );
        }

        restLengthU = shoulder != null
            ? Vector3.Distance(shoulder.position, transform.position)
            : 0f;
        if (armVisual != null) {
            armVisualWidth = new Vector2(
                armVisual.localScale.x, armVisual.localScale.y
            );
        }

        // arms alternate, so one starts half a stroke ahead of the other
        cyclePhase = isRightArm ? 0.5f : 0f;

        ConfigureJoint();

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"ArmRoot:\tready, {(isRightArm ? "right" : "left")} arm, "
                    + $"rest length {restLengthU}u",
                this
            );
        }
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
        // physics stops reporting while disabled, so stale contacts would
        // never clear on their own
        handContacts.Clear();
    }

    private void FixedUpdate() {
        if (joint == null) {
            return;
        }

        if (isReaching) {
            DriveReach();
        }
        else {
            DriveClimb();
        }
        StretchVisual();
    }

    private void OnCollisionEnter(Collision collision) {
        if (IsHandContact(collision)) {
            handContacts.Add(collision.collider);
        }
    }

    private void OnCollisionExit(Collision collision) {
        // exit carries no contact points to filter on, so drop the collider
        // either way, it is only in the set if the hand put it there
        handContacts.Remove(collision.collider);
    }

    // Constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;

    // Private Members  ########################################################
    private bool isReaching;
    private Transform reachTarget;
    private float cyclePhase; // 0..1, advances only while moving
    private float restLengthU;
    private Vector2 armVisualWidth; // visual's local X and Y scale, kept as-is

    private readonly HashSet<Collider> handContacts = new HashSet<Collider>();

    // mirrors every side-dependent angle for the left arm
    private float SideSign => isRightArm ? 1f : -1f;

    // Cached References  ------------------------------------------------------
    private Rigidbody body;
    private ConfigurableJoint joint;

    // Private Methods  ########################################################
    // shoulder pivot, local Z free to telescope, angular free for the slerp
    // drive to aim. Set once: re-assigning joint motion churns the solver
    private void ConfigureJoint() {
        if (joint == null) {
            return;
        }

        joint.connectedBody = bodyRigidbody;
        joint.autoConfigureConnectedAnchor = false;
        if (shoulder != null) {
            joint.anchor = transform.InverseTransformPoint(shoulder.position);
            joint.connectedAnchor = bodyRigidbody != null
                ? bodyRigidbody.transform.InverseTransformPoint(
                    shoulder.position
                )
                : shoulder.position;
        }

        // only local Z carries the reach, the other two stay rigid
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Limited;
        joint.linearLimit = new SoftJointLimit {
            limit = maxReachDistanceU + overshootAllowanceU,
        };
        joint.zDrive = new JointDrive {
            positionSpring = reachSpring,
            positionDamper = reachDamper,
            maximumForce = float.MaxValue,
        };

        // slerp drive aims the whole arm in one shot, no per-axis bookkeeping
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.slerpDrive = new JointDrive {
            positionSpring = aimSpring,
            positionDamper = aimDamper,
            maximumForce = float.MaxValue,
        };
    }

    // aim local Z at the tracked target and telescope out to close the gap,
    // the drive spring is what lets the hand sail past it
    private void DriveReach() {
        if (reachTarget == null || shoulder == null) {
            return;
        }

        Vector3 toTarget = reachTarget.position - shoulder.position;
        float extensionU = Mathf.Clamp(
            toTarget.magnitude - restLengthU, 0f, maxReachDistanceU
        );

        SetAimWorld(toTarget);
        SetExtension(extensionU);
    }

    // hand over hand: the linear stroke pushes the hand out and pulls it back
    // in, the angular sweep runs a quarter cycle ahead so the hand traces a
    // loop rather than a straight line
    private void DriveClimb() {
        Vector2 directionalInput = moveAction != null
            ? moveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        // no input: hold the pose, leave the drive targets where they are
        if (directionalInput.sqrMagnitude <= INPUT_DEADZONE_SQR) {
            return;
        }

        // moving down plays the stroke backward, a climb-down
        float direction = directionalInput.y < -INPUT_DEADZONE ? -1f : 1f;
        float inputScale = Mathf.Clamp01(directionalInput.magnitude);
        cyclePhase = Mathf.Repeat(
            cyclePhase
                + direction * inputScale * cycleSpeed * Time.fixedDeltaTime,
            1f
        );

        float phaseRad = cyclePhase * Mathf.PI * 2f;
        // sin is remapped to 0..1 so the arm never pulls inside its rest
        // length, cos puts the sweep a quarter cycle ahead of the push
        float extensionU = cycleReachU * 0.5f * (1f + Mathf.Sin(phaseRad));
        float swingDeg = cycleSwingDeg * Mathf.Cos(phaseRad);

        SetAimLocal(Quaternion.Euler(-swingDeg, SideSign * splayDeg, 0f));
        SetExtension(extensionU);
    }

    // point local +Z along a world direction, expressed in the connected
    // body's frame, which is what targetRotation is measured against
    private void SetAimWorld(Vector3 worldDirection) {
        if (worldDirection.sqrMagnitude <= Mathf.Epsilon) {
            return;
        }

        Quaternion connectedRotation = joint.connectedBody != null
            ? joint.connectedBody.rotation
            : Quaternion.identity;
        SetAimLocal(
            Quaternion.Inverse(connectedRotation)
                * Quaternion.LookRotation(worldDirection.normalized)
        );
    }

    private void SetAimLocal(Quaternion localRotation) {
        joint.targetRotation = localRotation;
    }

    private void SetExtension(float extensionU) {
        joint.targetPosition = new Vector3(
            0f, 0f, invertDriveAxis ? -extensionU : extensionU
        );
    }

    // span the visual from the shoulder to wherever the hand actually ended
    // up, measured and not commanded, so an overshoot shows on screen
    private void StretchVisual() {
        if (armVisual == null || shoulder == null) {
            return;
        }

        float lengthU = Vector3.Distance(
            shoulder.position, transform.position
        );
        armVisual.localPosition = new Vector3(0f, 0f, -lengthU * 0.5f);
        armVisual.localScale = new Vector3(
            armVisualWidth.x, armVisualWidth.y, lengthU
        );
    }

    // every child collider's collisions land on this Rigidbody, so keep only
    // the ones the hand itself made
    private bool IsHandContact(Collision collision) {
        if (handCollider == null) {
            return true;
        }

        for (int i = 0; i < collision.contactCount; i++) {
            if (collision.GetContact(i).thisCollider == handCollider) {
                return true;
            }
        }
        return false;
    }

    // a contact's collider can be destroyed while still touching, and exit
    // never fires for it
    private void PruneHandContacts() {
        handContacts.RemoveWhere(contact => contact == null);
    }
}
