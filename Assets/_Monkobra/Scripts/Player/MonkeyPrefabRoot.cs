using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prefab root driving movement along the tree trunk. Vertical input
/// moves the object directly along world Y; horizontal input orbits
/// it around this object's own parent's root at a fixed radius, so
/// motion stays relative to wherever the prefab is parented.
/// <para>
/// The body is kinematic and its path is authored here outright, so nothing
/// in the simulation, least of all the arms hanging off it, can push it off
/// course. The orbit angle is the only horizontal state and the climb speed
/// the only vertical one, both written straight into
/// <see cref="Rigidbody.MovePosition"/>; mass and gravity never enter into it.
/// </para>
/// <para>
/// Every speed, ramp rate and branch-hit number, plus the input action
/// itself, comes from the shared <see cref="MonkeyConfig"/> asset, the same
/// one the arms read, so the body and its arms cannot drift out of tune.
/// </para>
/// </summary>
public class MonkeyPrefabRoot: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("raises branch hit event, knocks monkey off climb")]
    private HitBranchDetection hitBranchDetection;

    [SerializeField]
    [Tooltip(
        "shared movement, branch-hit and input tuning, the same asset the "
            + "arms use"
    )]
    private MonkeyConfig config;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (hitBranchDetection == null) {
            Debug.LogWarning(
                "MonkeyPrefabRoot:\tmust assign Inspector Field: "
                    + "hitBranchDetection",
                this
            );
        }
        if (config == null) {
            Debug.LogWarning(
                "MonkeyPrefabRoot:\tmust assign Inspector Field: config",
                this
            );
        }
        else if (config.MoveAction == null) {
            Debug.LogWarning(
                "MonkeyPrefabRoot:\tmust assign MonkeyConfig field: "
                    + "moveAction",
                config
            );
        }

        if (transform.parent == null) {
            Debug.LogError(
                "MonkeyPrefabRoot:\tfail to get Transform: parent",
                this
            );
        }

        body = GetComponent<Rigidbody>();
        if (body == null) {
            Debug.LogError(
                "MonkeyPrefabRoot:\tfail to get Component: Rigidbody",
                this
            );
        }

        // the body's motion is authored here, not simulated: kinematic means
        // no arm joint, collision or weight can ever move it off its path
        if (body != null) {
            body.isKinematic = true;
            body.useGravity = false;
        }

        orbitAngleDeg = CalcCurrentOrbitAngle();
        orbitRadius = CalcCurrentOrbitRadius();
        if (Debug.isDebugBuild) {
            Debug.Log(
                "MonkeyPrefabRoot:\tready, orbit radius " + $"{orbitRadius}"
            );
        }
    }

    private void OnEnable() {
        if (MoveAction != null) {
            MoveAction.action.Enable();
        }
        if (hitBranchDetection != null) {
            hitBranchDetection.BranchHit += OnBranchHit;
        }
    }

    private void OnDisable() {
        if (MoveAction != null) {
            MoveAction.action.Disable();
        }
        if (hitBranchDetection != null) {
            hitBranchDetection.BranchHit -= OnBranchHit;
        }
    }

    private void FixedUpdate() {
        if (config == null) {
            return;
        }

        bool isStunned = IsStunned;
        Vector2 directionalInput = isStunned || MoveAction == null
            ? Vector2.zero
            : MoveAction.action.ReadValue<Vector2>();
        bool hasInput = directionalInput.sqrMagnitude > INPUT_DEADZONE_SQR;

        // the orbit angle is the only horizontal state, so the body sits
        // exactly on its ring every step rather than chasing it
        orbitAngleDeg -= directionalInput.x
            * config.RotationSpeedDeg
            * Time.fixedDeltaTime;

        float maxClimbSpeed = directionalInput.y >= 0f
            ? config.UpSpeedU
            : config.DownSpeedU;
        float rampRate = hasInput
            ? config.AccelerationU
            : config.DecelerationU;
        climbSpeedU = Mathf.MoveTowards(
            climbSpeedU,
            directionalInput.y * maxClimbSpeed,
            rampRate * Time.fixedDeltaTime
        );

        float nextY = transform.position.y + climbSpeedU * Time.fixedDeltaTime;
        if (isStunned) {
            // knocked off the climb: the ramp is bypassed, fall at a set
            // speed and stop dead on the drop target
            climbSpeedU = 0f;
            nextY = Mathf.Max(
                dropTargetY,
                transform.position.y
                    - config.HitDropSpeedU * Time.fixedDeltaTime
            );
        }

        Vector3 next =
            transform.parent.position + CalcOrbitOffset(orbitAngleDeg);
        next.y = nextY;
        body.MovePosition(next);
        body.MoveRotation(Quaternion.Euler(0f, orbitAngleDeg, 0f));
    }

    // Event Handlers  #########################################################
    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(WIN_ZONE_TAG)) {
            return;
        }

        if (GameController.I != null) {
            GameController.I.WinGame();
        } else {
            Debug.LogError(
                "MonkeyPrefabRoot:\tGameController instance not found",
                this
            );
        }
    }

    // knock monkey off climb: ignore input for the config's stun duration,
    // fall its drop distance. Ignore hit while previous one in effect
    private void OnBranchHit(Collider branch) {
        if (config == null) {
            return;
        }

        if (IsStunned) {
            if (Debug.isDebugBuild) {
                Debug.Log(
                    "MonkeyPrefabRoot:\tbranch hit ignored, "
                        + "already stunned"
                );
            }
            return;
        }

        stunEndTime = Time.time + config.HitStunDurationS;
        dropTargetY = transform.position.y - config.HitDropDistanceU;
        if (Debug.isDebugBuild) {
            Debug.Log(
                "MonkeyPrefabRoot:\tbranch hit, control lost for "
                    + $"{config.HitStunDurationS}s"
            );
        }
    }

    // constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;
    private const string WIN_ZONE_TAG = "WinZone";

    // private members  ########################################################
    private float orbitAngleDeg;
    private float orbitRadius; // dist from parent root, set once in Awake
    private float climbSpeedU; // ramped vertical speed, this script's own
    private float stunEndTime; // Time.time when control returns
    private float dropTargetY; // world Y where hit fall stops

    private bool IsStunned => Time.time < stunEndTime;

    // body and arms share one action, held on the config asset
    private InputActionReference MoveAction =>
        config != null ? config.MoveAction : null;

    // cached references  ------------------------------------------------------
    private Rigidbody body;

    // private methods  ########################################################
    private Vector3 CalcOrbitOffset(float angleDeg) {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad))
            * orbitRadius;
    }

    private float CalcCurrentOrbitAngle() {
        Vector3 toSelf = transform.position - transform.parent.position;
        return Mathf.Atan2(toSelf.x, toSelf.z) * Mathf.Rad2Deg;
    }

    private float CalcCurrentOrbitRadius() {
        Vector3 toSelf = transform.position - transform.parent.position;
        return new Vector2(toSelf.x, toSelf.z).magnitude;
    }
}
