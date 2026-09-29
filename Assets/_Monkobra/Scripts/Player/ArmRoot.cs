using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prefab root of one arm, sitting at the hand end: the joint telescopes it
/// away from the shoulder, so this transform is the hand.
/// <para>
/// Reaching aims at a tracked target and drives the arm out to it, otherwise
/// the arm sweeps a climb stroke on move input, left and right alternating.
/// </para>
/// <para>
/// The <see cref="ConfigurableJoint"/> is authored in the Inspector; this
/// script only writes its drive targets.
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

    /// <returns>shoulder to hand distance on the first step, the arm's
    /// unstretched length; u</returns>
    public float RestLengthU => restLengthU;

    /// <returns>shoulder to hand distance right now, rest length plus
    /// whatever the linear drive has telescoped out; u</returns>
    public float CurrentLengthU =>
        Vector3.Distance(ShoulderWorld, transform.position);

    // Public Methods  #########################################################
    /// <summary>
    /// Switches the arm into reach mode and tracks <paramref name="target"/>'s
    /// world position every step until <see cref="CancelReach"/>. A null
    /// target cancels the reach.
    /// </summary>
    public void SetReachTarget(Transform target) {
        isReaching = target != null;
        reachTarget = target;
        // a settled arm sleeps, and a new drive target alone will not wake it
        if (body != null) {
            body.WakeUp();
        }
    }

    /// <summary>Switches the arm back to climbing.</summary>
    public void CancelReach() {
        isReaching = false;
        reachTarget = null;
    }

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip(
        "renderer-only cube child, stretched to span shoulder to hand"
    )]
    private Transform armVisual;

    [SerializeField]
    [Tooltip(
        "collider-only hand child, only its contacts count as hand contacts"
    )]
    private Collider handCollider;

    [SerializeField]
    [Tooltip(
        "shared stroke, input and arm-width tuning, the same asset on both "
            + "arms, everything physical lives on the ConfigurableJoint"
    )]
    private MonkeyConfig config;

    [Header("Identity")]
    [SerializeField]
    [Tooltip(
        "whether this is the monkey's right arm, else the left arm, the two "
            + "climb half a cycle out of phase. Which way the arm leans and "
            + "sweeps comes from its transform, not from this flag"
    )]
    private bool isRightArm;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
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
        if (config == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign Inspector Field: config", this
            );
        }
        else if (config.MoveAction == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign MonkeyConfig field: moveAction",
                config
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
        else if (joint.connectedBody == null) {
            Debug.LogWarning(
                "ArmRoot:\tmust assign ConfigurableJoint field: Connected "
                    + "Body, the arm has nothing to hang off",
                this
            );
        }

        // the arm is posed by its drives alone, never by weight, and a prefab
        // override can quietly switch gravity back on, so settle it here
        if (body != null) {
            body.useGravity = false;
        }

        // arms alternate, so one starts half a stroke ahead of the other
        cyclePhase = isRightArm ? 0.5f : 0f;
    }

    // the joint finishes resolving its anchors after Awake, so the authored
    // setup is only safe to read from here on
    private void Start() {
        if (joint == null) {
            return;
        }

        reachZMotion = joint.zMotion;
        Vector3 outward = transform.position - ShoulderWorld;
        restLengthU = outward.magnitude;
        reachAxisSign = Vector3.Dot(
            transform.TransformDirection(LocalReachAxis), outward
        ) >= 0f
            ? 1f
            : -1f;

        // PhysX orthogonalizes Secondary Axis against Axis to build the frame,
        // so rebuild it the same way rather than trusting the authored pair to
        // be square. Captured at rest, where the arm's frame and the connected
        // body's still agree, which is what targetRotation is measured from
        Vector3 jointUp = Vector3.ProjectOnPlane(
            joint.secondaryAxis, joint.axis
        );
        Quaternion localJointFrame =
            jointUp.sqrMagnitude > Mathf.Epsilon
            ? Quaternion.LookRotation(LocalReachAxis, jointUp.normalized)
            : Quaternion.identity;
        Quaternion connectedRotation = joint.connectedBody != null
            ? joint.connectedBody.rotation
            : Quaternion.identity;
        connectedJointFrame = Quaternion.Inverse(connectedRotation)
            * transform.rotation
            * localJointFrame;

        IgnoreBodyCollisions();

        // the drives are what move the arm: a target written against a zero
        // spring does nothing, and the arm is left floating free in the world
        if (AimSpring <= 0f) {
            Debug.LogWarning(
                "ArmRoot:\tConfigurableJoint has no angular drive spring, "
                    + "the arm will not follow its aim target",
                this
            );
        }
        if (joint.zDrive.positionSpring <= 0f) {
            Debug.LogWarning(
                "ArmRoot:\tConfigurableJoint Z Drive has no spring, the arm "
                    + "will not telescope on reach",
                this
            );
        }
        if (joint.linearLimit.limit <= 0f) {
            Debug.LogWarning(
                "ArmRoot:\tConfigurableJoint Linear Limit is 0, the arm has "
                    + "no room to reach",
                this
            );
        }

        // the visual spans whatever the joint says the arm measures, so a
        // hand bigger than that swallows the whole limb and the arm reads on
        // screen as having retracted into the body
        if (handCollider != null) {
            Vector3 handExtents = handCollider.bounds.extents;
            float handRadiusU = Mathf.Max(
                handExtents.x, Mathf.Max(handExtents.y, handExtents.z)
            );
            if (handRadiusU >= restLengthU) {
                Debug.LogWarning(
                    $"ArmRoot:\thand is {handRadiusU}u across but the joint "
                        + $"anchor puts the shoulder only {restLengthU}u away, "
                        + "so the arm draws entirely inside its own hand, move "
                        + "the anchor to the shoulder or shrink the hand",
                    this
                );
            }
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"ArmRoot:\tready, {(isRightArm ? "right" : "left")} arm, "
                    + $"rest length {restLengthU}u, max reach "
                    + $"{joint.linearLimit.limit}u",
                this
            );
        }
    }

    private void OnEnable() {
        if (MoveAction != null) {
            MoveAction.action.Enable();
        }
    }

    private void OnDisable() {
        if (MoveAction != null) {
            MoveAction.action.Disable();
        }
        // physics stops reporting while disabled, so stale contacts would
        // never clear on their own
        handContacts.Clear();
    }

    private void FixedUpdate() {
        if (joint == null || config == null) {
            return;
        }

        // the linear axis carries its authored motion only while reaching, so
        // nothing can telescope the arm while it climbs. Re-assigning an
        // unchanged joint motion every step jolts the solver, hence the guards
        if (isReaching) {
            if (joint.zMotion != reachZMotion) {
                joint.zMotion = reachZMotion;
            }
            DriveReach();
        }
        else {
            if (joint.zMotion != ConfigurableJointMotion.Locked) {
                joint.zMotion = ConfigurableJointMotion.Locked;
                SetExtension(0f);
            }
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

    // the authored linear motion, restored whenever the arm reaches
    private ConfigurableJointMotion reachZMotion =
        ConfigurableJointMotion.Limited;

    // the joint's own basis, as it sits in the connected body's frame at rest.
    // targetRotation is measured in this frame, so a direction has to be
    // brought into it before the drive can be aimed along one
    private Quaternion connectedJointFrame = Quaternion.identity;

    // +1 when the joint's reach axis already points away from the shoulder,
    // -1 when it points back through the body, measured once from the
    // authored pose. Two arms on opposite sides of one body share the same
    // local axes, so their outward directions are mirrored and the drive
    // target has to follow, else one arm telescopes into the torso
    private float reachAxisSign = 1f;

    private readonly HashSet<Collider> handContacts = new HashSet<Collider>();

    // both arms share one action, held on the config asset
    private InputActionReference MoveAction =>
        config != null ? config.MoveAction : null;

    // the shoulder is the joint's own pivot, read off the connected body so it
    // tracks the body every step, no separate Transform to keep in sync
    private Vector3 ShoulderWorld {
        get {
            if (joint == null) {
                return transform.position;
            }
            return joint.connectedBody != null
                ? joint.connectedBody.transform.TransformPoint(
                    joint.connectedAnchor
                )
                : transform.TransformPoint(joint.anchor);
        }
    }

    // whichever angular drive the authored Rotation Drive Mode actually uses
    private float AimSpring =>
        joint.rotationDriveMode == RotationDriveMode.Slerp
        ? joint.slerpDrive.positionSpring
        : Mathf.Max(
            joint.angularXDrive.positionSpring,
            joint.angularYZDrive.positionSpring
        );

    // the joint frame's third axis, the one targetPosition's z drives, so
    // editing Axis or Secondary Axis redirects the whole arm
    private Vector3 LocalReachAxis {
        get {
            Vector3 axis = Vector3.Cross(joint.axis, joint.secondaryAxis);
            return axis.sqrMagnitude > Mathf.Epsilon
                ? axis.normalized
                : Vector3.forward;
        }
    }

    // Cached References  ------------------------------------------------------
    private Rigidbody body;
    private ConfigurableJoint joint;

    // Private Methods  ########################################################
    // an arm brushing its own torso would shove the body it is trying to
    // climb with, and would report the monkey itself as a hand contact, so
    // take the arm and the body out of each other's collision matrix
    private void IgnoreBodyCollisions() {
        if (joint.connectedBody == null) {
            return;
        }

        Collider[] armColliders = GetComponentsInChildren<Collider>(true);
        Collider[] bodyColliders =
            joint.connectedBody.GetComponentsInChildren<Collider>(true);
        foreach (Collider armCollider in armColliders) {
            foreach (Collider bodyCollider in bodyColliders) {
                // an arm parented under the body shows up in both lists
                if (
                    armCollider == bodyCollider
                    || bodyCollider.attachedRigidbody == body
                ) {
                    continue;
                }
                Physics.IgnoreCollision(armCollider, bodyCollider, true);
            }
        }
    }

    // aim the arm at the tracked target and telescope out to close the gap,
    // the drive spring is what lets the hand sail past it and the joint's own
    // Linear Limit is what stops it
    private void DriveReach() {
        if (reachTarget == null) {
            return;
        }

        Vector3 toTarget = reachTarget.position - ShoulderWorld;
        float extensionU = Mathf.Clamp(
            toTarget.magnitude - restLengthU, 0f, joint.linearLimit.limit
        );

        SetAimWorld(toTarget);
        SetExtension(extensionU);
    }

    // hand over hand: the sweep about the joint's primary axis carries the
    // stroke, the arm keeps its rest length and the hand rides the end of it
    private void DriveClimb() {
        Vector2 directionalInput = MoveAction != null
            ? MoveAction.action.ReadValue<Vector2>()
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
                + direction
                    * inputScale
                    * config.StrokeSpeedCycles
                    * Time.fixedDeltaTime,
            1f
        );

        // the sweep rides the config's stroke range: its midpoint is the
        // arm's neutral pitch, and half its span is the swing amplitude
        float phaseRad = cyclePhase * Mathf.PI * 2f;
        float midDeg = (config.MaxAngleDeg + config.MinAngleDeg) * 0.5f;
        float halfRangeDeg = (config.MaxAngleDeg - config.MinAngleDeg) * 0.5f;
        float swingDeg = midDeg + halfRangeDeg * Mathf.Sin(phaseRad);

        // the drive reads targetRotation in the joint's own frame, where the
        // authored Axis is always right and Secondary Axis always up, whatever
        // they were set to in the Inspector. Re-aiming them there still
        // re-aims the climb, by turning the frame the stroke is measured in.
        // Both arms take the same angles: the right arm's transform is flipped
        // a half turn, so one set already reads mirrored in the world, and the
        // stroke stays in step while the splay leans them apart
        SetAimJoint(
            Quaternion.AngleAxis(-swingDeg, Vector3.right)
                * Quaternion.AngleAxis(config.SplayDeg, Vector3.up)
        );
        if (body != null) {
            body.WakeUp();
        }
    }

    // point the reach axis along a world direction, converted into the joint's
    // own frame, which is what targetRotation is measured against
    private void SetAimWorld(Vector3 worldDirection) {
        if (worldDirection.sqrMagnitude <= Mathf.Epsilon) {
            return;
        }

        Quaternion connectedRotation = joint.connectedBody != null
            ? joint.connectedBody.rotation
            : Quaternion.identity;
        // carry the direction all the way into the joint's frame, where the
        // reach axis is forward, then swing forward onto it. Reading the
        // connected body rather than this arm keeps the drive from chasing
        // the rotation it is itself producing
        Vector3 jointDirection = Quaternion.Inverse(connectedJointFrame)
            * (Quaternion.Inverse(connectedRotation)
                * worldDirection.normalized);
        SetAimJoint(
            Quaternion.FromToRotation(Vector3.forward, jointDirection)
        );
    }

    // the rotation is read in the joint's frame, not the arm's: Axis is right,
    // Secondary Axis is up, and the reach axis is forward
    private void SetAimJoint(Quaternion jointRotation) {
        joint.targetRotation = jointRotation;
    }

    // InvertDriveAxis covers the joint's own drive sign convention, shared by
    // both arms, and reachAxisSign covers the mirror between them
    private void SetExtension(float extensionU) {
        bool isInverted = config == null || config.InvertDriveAxis;
        joint.targetPosition = new Vector3(
            0f,
            0f,
            reachAxisSign * (isInverted ? -extensionU : extensionU)
        );
    }

    // span the visual from the shoulder to wherever the hand actually ended
    // up, measured and not commanded, so an overshoot shows on screen
    private void StretchVisual() {
        if (armVisual == null) {
            return;
        }

        Vector3 shoulderLocal =
            transform.InverseTransformPoint(ShoulderWorld);
        float lengthU = shoulderLocal.magnitude;

        // sit the cube's centre halfway to the shoulder and point its local Z
        // down that line, so the span holds however the joint frame is aimed
        armVisual.localPosition = shoulderLocal * 0.5f;
        armVisual.localRotation = lengthU > Mathf.Epsilon
            ? Quaternion.LookRotation(shoulderLocal)
            : Quaternion.identity;
        armVisual.localScale = new Vector3(
            config.ArmWidthU, config.ArmWidthU, lengthU
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
