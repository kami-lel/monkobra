using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Prefab root driving movement along the tree trunk. Vertical input
/// moves the object directly along world Y; horizontal input orbits
/// it around this object's own parent's root at a fixed radius, so
/// motion stays relative to wherever the prefab is parented.
/// </summary>
public class MonkeyPrefabRoot: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Branch Hit")]
    [SerializeField]
    [Tooltip("raises branch hit event, knocks monkey off climb")]
    private HitBranchDetection hitBranchDetection;

    [SerializeField]
    [Tooltip("control lost after branch hit; s")]
    private float hitStunDuration = 0.5f;

    [SerializeField]
    [Tooltip("distance fallen on branch hit; units")]
    private float hitDropDistance = 2f;

    [SerializeField]
    [Tooltip("fall speed on branch hit; units/s")]
    private float hitDropSpeed = 8f;

    [Header("Movement")]
    [SerializeField]
    [Tooltip("climb speed moving up; units/s")]
    private float upSpeed;

    [SerializeField]
    [Tooltip("climb speed moving down; units/s")]
    private float downSpeed;

    [SerializeField]
    [Tooltip("orbit speed around parent; deg/s")]
    private float rotationSpeed;

    [SerializeField]
    [Tooltip("ramp rate toward target velocity while input held; units/s²")]
    private float acceleration;

    [SerializeField]
    [Tooltip("ramp rate toward target velocity while idle; units/s²")]
    private float deceleration;

    [Header("Input")]
    [SerializeField]
    [Tooltip("Vector2 action: x orbit, y climb")]
    private InputActionReference moveAction;

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
        if (moveAction == null) {
            Debug.LogWarning(
                "MonkeyPrefabRoot:\tmust assign Inspector Field: moveAction",
                this
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

        orbitAngleDeg = CalcCurrentOrbitAngle();
        orbitRadius = CalcCurrentOrbitRadius();
        if (Debug.isDebugBuild) {
            Debug.Log(
                "MonkeyPrefabRoot:\tready, orbit radius " + $"{orbitRadius}"
            );
        }
    }

    private void OnEnable() {
        moveAction.action.Enable();
        if (hitBranchDetection != null) {
            hitBranchDetection.BranchHit += OnBranchHit;
        }
    }

    private void OnDisable() {
        moveAction.action.Disable();
        if (hitBranchDetection != null) {
            hitBranchDetection.BranchHit -= OnBranchHit;
        }
    }

    private void FixedUpdate() {
        bool isStunned = IsStunned;
        bool isDropping = isStunned && transform.position.y > dropTargetY;
        Vector2 directionalInput = isStunned
            ? Vector2.zero
            : moveAction.action.ReadValue<Vector2>();
        bool hasInput = directionalInput.sqrMagnitude > INPUT_DEADZONE_SQR;

        orbitAngleDeg -=
            directionalInput.x * rotationSpeed * Time.fixedDeltaTime;

        Vector3 orbitTarget =
            transform.parent.position + CalcOrbitOffset(orbitAngleDeg);
        Vector3 towardOrbit = orbitTarget - transform.position;
        towardOrbit.y = 0f;

        float climbSpeed = directionalInput.y >= 0f ? upSpeed : downSpeed;
        Vector3 targetVelocity = towardOrbit / Time.fixedDeltaTime;
        targetVelocity.y = directionalInput.y * climbSpeed;

        float rampRate = hasInput ? acceleration : deceleration;
        Vector3 velocity = Vector3.MoveTowards(
            body.linearVelocity,
            targetVelocity,
            rampRate * Time.fixedDeltaTime
        );
        if (isStunned) {
            // set fall speed directly, clamp to remaining distance: drop
            // neither lags behind ramp nor overshoots target
            float remaining = transform.position.y - dropTargetY;
            velocity.y = isDropping
                ? -Mathf.Min(hitDropSpeed, remaining / Time.fixedDeltaTime)
                : 0f;
        }
        body.linearVelocity = velocity;
        body.MoveRotation(Quaternion.Euler(0f, orbitAngleDeg, 0f));
    }

    // Event Handlers  #########################################################
    // knock monkey off climb: ignore input for hitStunDuration, fall
    // hitDropDistance. Ignore hit while previous one in effect
    private void OnBranchHit(Collider branch) {
        if (IsStunned) {
            if (Debug.isDebugBuild) {
                Debug.Log(
                    "MonkeyPrefabRoot:\tbranch hit ignored, "
                        + "already stunned"
                );
            }
            return;
        }

        stunEndTime = Time.time + hitStunDuration;
        dropTargetY = transform.position.y - hitDropDistance;
        if (Debug.isDebugBuild) {
            Debug.Log(
                "MonkeyPrefabRoot:\tbranch hit, control lost for "
                    + $"{hitStunDuration}s"
            );
        }
    }

    // constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;

    // private members  ########################################################
    private float orbitAngleDeg;
    private float orbitRadius; // dist from parent root, set once in Awake
    private float stunEndTime; // Time.time when control returns
    private float dropTargetY; // world Y where hit fall stops

    private bool IsStunned => Time.time < stunEndTime;

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
