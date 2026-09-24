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
    [Tooltip("detector raising the branch hit event, knocks the monkey off "
             + "its climb")]
    [SerializeField]
    private HitBranchDetection hitBranchDetection;
    [Tooltip("duration of lost control after a branch hit, in s")]
    [SerializeField]
    private float hitStunDuration = 0.5f;
    [Tooltip("distance fallen during a branch hit, in units")]
    [SerializeField]
    private float hitDropDistance = 2f;
    [Tooltip("fall speed during a branch hit, in units/s")]
    [SerializeField]
    private float hitDropSpeed = 8f;
    [Tooltip("climb speed while moving up, in units/s")]
    [SerializeField]
    private float upSpeed;
    [Tooltip("climb speed while moving down, in units/s")]
    [SerializeField]
    private float downSpeed;
    [Tooltip("orbit speed around this object's parent, in deg/s")]
    [SerializeField]
    private float rotationSpeed;
    [Tooltip("ramp rate toward target velocity while input is held, in "
             + "units/s²")]
    [SerializeField]
    private float acceleration;
    [Tooltip("ramp rate toward target velocity while idle, in units/s²")]
    [SerializeField]
    private float deceleration;
    [Tooltip("directional input action driving climb and orbit, a Vector2 "
             + "of horizontal (orbit) and vertical (climb) axes")]
    [SerializeField]
    private InputActionReference moveAction;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (moveAction == null) {
            Debug.LogWarning("must assign Inspector Field: moveAction", this);
        }
        if (hitBranchDetection == null) {
            Debug.LogWarning(
                "must assign Inspector Field: hitBranchDetection", this);
        }

        if (transform.parent == null) {
            Debug.LogError("fail to get Transform: parent", this);
        }

        body = GetComponent<Rigidbody>();
        if (body == null) {
            Debug.LogError("fail to get Component: Rigidbody", this);
        }

        orbitAngleDeg = CalcCurrentOrbitAngle();
        orbitRadius = CalcCurrentOrbitRadius();
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

        orbitAngleDeg -= directionalInput.x * rotationSpeed * Time.fixedDeltaTime;

        Vector3 orbitTarget = transform.parent.position
                               + CalcOrbitOffset(orbitAngleDeg);
        Vector3 towardOrbit = orbitTarget - transform.position;
        towardOrbit.y = 0f;

        float climbSpeed = directionalInput.y >= 0f ? upSpeed : downSpeed;
        Vector3 targetVelocity = towardOrbit / Time.fixedDeltaTime;
        targetVelocity.y = directionalInput.y * climbSpeed;

        float rampRate = hasInput ? acceleration : deceleration;
        Vector3 velocity = Vector3.MoveTowards(body.linearVelocity,
                                               targetVelocity,
                                               rampRate * Time.fixedDeltaTime);
        if (isStunned) {
            // set fall speed directly, clamped to the remaining distance so
            // the drop neither lags behind the ramp nor overshoots its target
            float remaining = transform.position.y - dropTargetY;
            velocity.y = isDropping
                ? -Mathf.Min(hitDropSpeed, remaining / Time.fixedDeltaTime)
                : 0f;
        }
        body.linearVelocity = velocity;
        body.MoveRotation(Quaternion.Euler(0f, orbitAngleDeg, 0f));
    }

    // Event Handlers  #########################################################
    // knocks monkey off its climb: input ignored for hitStunDuration while it
    // falls hitDropDistance. Ignored while a previous hit is in effect
    private void OnBranchHit(Collider branch) {
        if (IsStunned) {
            return;
        }

        stunEndTime = Time.time + hitStunDuration;
        dropTargetY = transform.position.y - hitDropDistance;
        if (Debug.isDebugBuild) {
            Debug.Log("MonkeyPrefabRoot:\tbranch hit, control lost for "
                      + $"{hitStunDuration}s");
        }
    }

    // constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;

    // private members  ########################################################
    private float orbitAngleDeg;
    private float orbitRadius;  // dist kept from parent's root, set once in Awake
    private float stunEndTime;  // Time.time when control returns
    private float dropTargetY;  // world Y where the hit fall stops

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
