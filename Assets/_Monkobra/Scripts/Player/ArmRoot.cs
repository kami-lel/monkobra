using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prefab root of one arm, at the hand end: the joint telescopes it away from
/// the shoulder, so this transform is the hand.
/// <para>
/// Sweeps a climb stroke on move input, left and right alternating. Holding
/// interact aims once at the newest fruit and stretches out along that line,
/// releasing grabs the fruit iff the hand overlaps it then: too short or
/// stretched past it is a miss.
/// </para>
/// <para>
/// The <see cref="ConfigurableJoint"/> is authored in the Inspector, this
/// script only writes its drive targets.
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(ConfigurableJoint))]
public class ArmRoot: MonoBehaviour {
    // Public Members  #########################################################
    /// <returns>if the arm is reaching for fruit, else climbing</returns>
    public bool IsReaching => isReaching;

    /// <returns>if the hand touches anything right now</returns>
    public bool IsHandTouching {
        get {
            PruneHandContacts();
            return handContacts.Count > 0;
        }
    }

    /// <returns>colliders the hand touches right now, a live view, do not
    /// hold across frames</returns>
    public IReadOnlyCollection<Collider> HandContacts {
        get {
            PruneHandContacts();
            return handContacts;
        }
    }

    /// <returns>shoulder to hand distance at start, unstretched; u</returns>
    public float RestLengthU => restLengthU;

    /// <returns>shoulder to hand distance now; u</returns>
    public float CurrentLengthU =>
        Vector3.Distance(ShoulderWorld, transform.position);

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip("renderer-only cube child, stretched from shoulder to hand")]
    private Transform armVisual;

    [SerializeField]
    [Tooltip("collider-only hand child, only its contacts count as hand's")]
    private Collider handCollider;

    [SerializeField]
    [Tooltip(
        "shared stroke, input and arm-width tuning, same asset on both arms"
    )]
    private MonkeyConfig config;

    [Header("Identity")]
    [SerializeField]
    [Tooltip(
        "whether this is the right arm, else left, the 2 climb half a cycle "
            + "out of phase. Lean and sweep direction come from the transform"
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
        else {
            if (config.MoveAction == null) {
                Debug.LogWarning(
                    "ArmRoot:\tmust assign MonkeyConfig field: moveAction",
                    config
                );
            }
            if (config.InteractAction == null) {
                Debug.LogWarning(
                    "ArmRoot:\tmust assign MonkeyConfig field: "
                        + "interactAction",
                    config
                );
            }
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

        // drives pose the arm, a prefab override may re-enable gravity
        if (body != null) {
            body.useGravity = false;
        }

        // arms alternate, 1 starts half a stroke ahead
        cyclePhase = isRightArm ? 0.5f : 0f;
    }

    // joint resolves its anchors after Awake, read authored setup from here
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

        // PhysX orthogonalizes Secondary Axis against Axis, so rebuild the
        // frame the same way. Captured at rest, where arm and connected body
        // frames agree, the base targetRotation is measured from
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

        // a target against a zero spring does nothing, arm floats free
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

        // a hand bigger than the arm swallows the visual, arm reads retracted
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
        if (InteractAction != null) {
            InteractAction.action.Enable();
            InteractAction.action.performed += OnInteractPerformed;
            InteractAction.action.canceled += OnInteractCanceled;
        }
    }

    private void OnDisable() {
        if (MoveAction != null) {
            MoveAction.action.Disable();
        }
        if (InteractAction != null) {
            InteractAction.action.performed -= OnInteractPerformed;
            InteractAction.action.canceled -= OnInteractCanceled;
            InteractAction.action.Disable();
        }
        EndReach();
        // no physics reports while disabled, stale contacts never clear
        handContacts.Clear();
    }

    private void FixedUpdate() {
        if (joint == null || config == null) {
            return;
        }

        // linear axis moves only while reaching, else locked. Guards skip
        // re-assigning an unchanged motion, which jolts the solver
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
        // exit has no contact points, drop either way, only hand's are in
        handContacts.Remove(collision.collider);
    }

    // Event Handlers  #########################################################
    // detection singleton picks the side, only the matching arm reaches
    private void OnInteractPerformed(InputAction.CallbackContext context) {
        UpwardFruitDetection fruitDetection = UpwardFruitDetection.I;
        if (fruitDetection == null) {
            Debug.LogError(
                "ArmRoot:\tfail to get singleton: UpwardFruitDetection", this
            );
            return;
        }

        DetectionSide armSide =
            isRightArm ? DetectionSide.Right : DetectionSide.Left;
        if (fruitDetection.ReachSide != armSide) {
            return;
        }

        Collider fruit = fruitDetection.GetNewestFruit(armSide);
        if (fruit == null) {
            return;
        }

        isReaching = true;
        reachFruit = fruit;
        reachExtensionU = 0f;
        // aim once, the arm then only stretches along that line
        SetAimWorld(fruit.transform.position - ShoulderWorld);
        // a settled arm sleeps, a new drive target alone will not wake it
        if (body != null) {
            body.WakeUp();
        }
        if (Debug.isDebugBuild) {
            Debug.Log($"ArmRoot:\treaching for {fruit.name}", this);
        }
    }

    // release decides the grab, then the arm pulls back
    private void OnInteractCanceled(InputAction.CallbackContext context) {
        TryGrabFruit();
        EndReach();
    }

    // Constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;

    // Private Members  ########################################################
    private bool isReaching;
    private Collider reachFruit; // fruit the reach aims at
    private float reachExtensionU; // commanded extension while held; u
    private float cyclePhase; // 0..1, advances only while moving
    private float restLengthU;

    // authored linear motion, restored on reach
    private ConfigurableJointMotion reachZMotion =
        ConfigurableJointMotion.Limited;

    // joint basis in the connected body's frame at rest, what targetRotation
    // is measured in, so directions go through it before aiming
    private Quaternion connectedJointFrame = Quaternion.identity;

    // +1 iff the joint's reach axis points away from the shoulder, else -1,
    // measured once from the authored pose. Arms on opposite sides share
    // local axes, so their outward directions mirror and the drive follows
    private float reachAxisSign = 1f;

    private readonly HashSet<Collider> handContacts = new HashSet<Collider>();

    // both arms share 1 action, held on the config asset
    private InputActionReference MoveAction =>
        config != null ? config.MoveAction : null;

    private InputActionReference InteractAction =>
        config != null ? config.InteractAction : null;

    // the joint's pivot, read off the connected body so it tracks every step
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

    // angular drive the authored Rotation Drive Mode actually uses
    private float AimSpring =>
        joint.rotationDriveMode == RotationDriveMode.Slerp
        ? joint.slerpDrive.positionSpring
        : Mathf.Max(
            joint.angularXDrive.positionSpring,
            joint.angularYZDrive.positionSpring
        );

    // joint frame's 3rd axis, driven by targetPosition z, so editing Axis or
    // Secondary Axis redirects the arm
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
    // arm brushing the torso would shove the body and report the monkey as a
    // hand contact, so drop collisions between them
    private void IgnoreBodyCollisions() {
        if (joint.connectedBody == null) {
            return;
        }

        Collider[] armColliders = GetComponentsInChildren<Collider>(true);
        Collider[] bodyColliders =
            joint.connectedBody.GetComponentsInChildren<Collider>(true);
        foreach (Collider armCollider in armColliders) {
            foreach (Collider bodyCollider in bodyColliders) {
                // an arm parented under the body is in both lists
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

    private void EndReach() {
        isReaching = false;
        reachFruit = null;
        reachExtensionU = 0f;
    }

    // grab the aimed fruit iff the hand overlaps it now: switch it off and
    // drop it from detection. Too short or past it, no overlap, no grab
    private void TryGrabFruit() {
        if (!isReaching || reachFruit == null) {
            return;
        }

        Collider fruit = reachFruit;
        if (!fruit.gameObject.activeInHierarchy) {
            return;
        }

        if (!IsHandOnFruit(fruit)) {
            if (Debug.isDebugBuild) {
                bool isTooShort = CurrentLengthU
                    < Vector3.Distance(ShoulderWorld, fruit.bounds.center);
                Debug.Log(
                    "ArmRoot:\tmiss " + fruit.name + ", arm too "
                        + (isTooShort ? "short" : "long"),
                    this
                );
            }
            return;
        }

        fruit.gameObject.SetActive(false);
        // a disabled collider may never raise its trigger exit
        UpwardFruitDetection fruitDetection = UpwardFruitDetection.I;
        if (fruitDetection != null) {
            fruitDetection.RemoveFruit(fruit);
        }
        if (Debug.isDebugBuild) {
            Debug.Log($"ArmRoot:\tgrabbed {fruit.name}", this);
        }
    }

    // triggers report no contacts, so measure the overlap directly
    private bool IsHandOnFruit(Collider fruit) {
        if (handCollider == null) {
            return false;
        }

        Transform handXfm = handCollider.transform;
        Transform fruitXfm = fruit.transform;
        return Physics.ComputePenetration(
            handCollider,
            handXfm.position,
            handXfm.rotation,
            fruit,
            fruitXfm.position,
            fruitXfm.rotation,
            out _,
            out _
        );
    }

    // stretch at a steady speed along the locked aim, through the fruit,
    // until released
    private void DriveReach() {
        reachExtensionU = Mathf.MoveTowards(
            reachExtensionU,
            joint.linearLimit.limit,
            config.ReachExtendSpeedU * Time.fixedDeltaTime
        );
        SetExtension(reachExtensionU);
    }

    // hand over hand: sweep about the joint's primary axis, arm keeps its
    // rest length and the hand rides the end
    private void DriveClimb() {
        Vector2 directionalInput = MoveAction != null
            ? MoveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        // no input: hold pose, leave drive targets as they are
        if (directionalInput.sqrMagnitude <= INPUT_DEADZONE_SQR) {
            return;
        }

        // moving down plays the stroke backward
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

        // stroke range midpoint is the neutral pitch, half its span the swing
        float phaseRad = cyclePhase * Mathf.PI * 2f;
        float midDeg = (config.MaxAngleDeg + config.MinAngleDeg) * 0.5f;
        float halfRangeDeg = (config.MaxAngleDeg - config.MinAngleDeg) * 0.5f;
        float swingDeg = midDeg + halfRangeDeg * Mathf.Sin(phaseRad);

        // targetRotation is in the joint's own frame: Axis right, Secondary
        // Axis up. Both arms take the same angles, the right arm's transform
        // is flipped a half turn so it reads mirrored, splay leans them apart
        SetAimJoint(
            Quaternion.AngleAxis(-swingDeg, Vector3.right)
                * Quaternion.AngleAxis(config.SplayDeg, Vector3.up)
        );
        if (body != null) {
            body.WakeUp();
        }
    }

    // point the reach axis along a world direction, taken into the joint's
    // frame, which targetRotation is measured in
    private void SetAimWorld(Vector3 worldDirection) {
        if (worldDirection.sqrMagnitude <= Mathf.Epsilon) {
            return;
        }

        // read the connected body, not this arm, so the drive never chases
        // its own rotation
        Quaternion connectedRotation = joint.connectedBody != null
            ? joint.connectedBody.rotation
            : Quaternion.identity;
        Vector3 jointDirection = Quaternion.Inverse(connectedJointFrame)
            * (Quaternion.Inverse(connectedRotation)
                * worldDirection.normalized);
        // targetRotation runs backward, write the inverse swing
        SetAimJoint(
            Quaternion.FromToRotation(jointDirection, Vector3.forward)
        );
    }

    // joint frame: Axis right, Secondary Axis up, reach axis forward
    private void SetAimJoint(Quaternion jointRotation) {
        joint.targetRotation = jointRotation;
    }

    // InvertDriveAxis covers the joint's drive sign, reachAxisSign the mirror
    // between arms
    private void SetExtension(float extensionU) {
        bool isInverted = config == null || config.InvertDriveAxis;
        joint.targetPosition = new Vector3(
            0f,
            0f,
            reachAxisSign * (isInverted ? -extensionU : extensionU)
        );
    }

    // span the visual from shoulder to where the hand actually is, measured
    // not commanded
    private void StretchVisual() {
        if (armVisual == null) {
            return;
        }

        Vector3 shoulderLocal =
            transform.InverseTransformPoint(ShoulderWorld);
        float lengthU = shoulderLocal.magnitude;

        // cube centre halfway to the shoulder, local Z down that line
        armVisual.localPosition = shoulderLocal * 0.5f;
        armVisual.localRotation = lengthU > Mathf.Epsilon
            ? Quaternion.LookRotation(shoulderLocal)
            : Quaternion.identity;
        armVisual.localScale = new Vector3(
            config.ArmWidthU, config.ArmWidthU, lengthU
        );
    }

    // child colliders all collide on this Rigidbody, keep the hand's
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

    // a touching collider may be destroyed, its exit never fires
    private void PruneHandContacts() {
        handContacts.RemoveWhere(contact => contact == null);
    }
}
