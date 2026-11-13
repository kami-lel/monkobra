using System.Collections.Generic;
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
/// itself, comes from the shared <see cref="MonkeyInputConfig"/> asset, the same
/// one the arms read, so the body and its arms cannot drift out of tune.
/// </para>
/// </summary>
public class MonkeyController: MonoBehaviour {
    // Public Members  #########################################################
    /// <summary>
    /// While true the body stays exactly where it is, ignoring input and any
    /// branch-hit fall. Set by <see cref="SpiderWebStruggle"/> while the
    /// monkey is caught in a web. Either change zeroes the climb speed, so
    /// the climb resumes from rest rather than with the speed carried in.
    /// </summary>
    public bool IsHeld {
        get => isHeld;
        set {
            isHeld = value;
            climbSpeedU = 0f;
            upHeldS = 0f;
            downHeldS = 0f;
            // freed after a pull: glide back onto the ring, never snap
            if (!value && hasPullTarget) {
                hasPullTarget = false;
                isReturning = true;
            }
        }
    }

    /// <returns>if the dev speed buff runs the body at
    /// <see cref="SPEED_BUFF_SCALE"/> times its normal speed, toggled by the
    /// <see cref="HackAction.SpeedBuff"/> key in a development build
    /// </returns>
    public bool IsSpeedBuff {
        get; private set;
    }

    // Public Methods  #########################################################
    public float RoughBarkSpeedScale => roughBarkSpeedScale;

    /// <summary>Tracks actual surface contact; overlap never stacks slowdown.</summary>
    public void SetRoughBarkContact(RoughBarkZone zone, bool touching) {
        if (zone == null) return;
        if (touching) {
            // only a patch not touched before counts as a new contact
            if (roughContacts.Add(zone)) RoughBarkTouched?.Invoke();
        } else {
            roughContacts.Remove(zone);
        }
    }

    /// <summary>raised each time the monkey meets a rough bark patch</summary>
    public event System.Action RoughBarkTouched;
    /// <summary>
    /// While held, slides the body to <paramref name="worldPos"/> in 3D, e.g.
    /// the hub of the web that caught it. Releasing the hold glides the body
    /// back onto its orbit ring, keeping the height it was pulled to.
    /// </summary>
    public void PullTo(Vector3 worldPos) {
        PullTo(worldPos, pullSpeedU);
    }

    /// <summary>
    /// Same as <see cref="PullTo(Vector3)"/>, at its own speed.
    /// </summary>
    /// <param name="worldPos">world pos to slide to</param>
    /// <param name="speedU">speed of the slide; u/s</param>
    public void PullTo(Vector3 worldPos, float speedU) {
        pullTarget = worldPos;
        activePullSpeedU = speedU;
        hasPullTarget = true;
    }

    /// <summary>
    /// Caps the climb at <paramref name="worldY"/>, eg below the Cobra King:
    /// however long up is held the body stops there, and one above it eases
    /// down to it.
    /// </summary>
    public void SetHeightCeiling(float worldY) {
        heightCeilingY = worldY;
        hasHeightCeiling = true;
    }

    /// <summary>
    /// Takes the orbit angle from where the body is now, eg after a pull
    /// carried it to a spawn point, so letting go glides it onto the ring
    /// right there instead of back at its old angle.
    /// </summary>
    public void SyncOrbitAngle() {
        orbitAngleDeg = CalcCurrentOrbitAngle();
    }

    /// <summary>Lifts the cap set by <see cref="SetHeightCeiling"/>.</summary>
    public void ClearHeightCeiling() {
        hasHeightCeiling = false;
    }

    /// <returns>if the held body has arrived at its pull target</returns>
    public bool HasReachedPullTarget =>
        hasPullTarget && body != null && body.position == pullTarget;

    /// <summary>Ends any branch-hit stun and its fall at once.</summary>
    public void ClearStun() {
        stunEndTime = 0f;
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("speed of the pull into a web and the glide back out; u/s")]
    private float pullSpeedU = 12f;

    [SerializeField]
    [Tooltip("raises branch hit event, knocks monkey off climb")]
    private HitBranchSensor hitBranchDetection;

    [SerializeField]
    [Tooltip("shared input actions, the same asset the arms use")]
    private MonkeyInputConfig inputConfig;

    [SerializeField]
    [Tooltip("player balance tuning; branch hit and starting zone")]
    private PlayerBalanceConfig playerBalanceConfig;

    [SerializeField]
    [Tooltip("world balance tuning; rough bark slowdown")]
    private WorldBalanceConfig worldBalanceConfig;

    [SerializeField]
    [Tooltip(
        "climb build-up, exhaustion slow down, idle stamina recovery, orbit "
            + "speed and deceleration"
    )]
    private MonkeyClimbConfig climbConfig;

    [SerializeField]
    [Tooltip("dev/hack keys, its speed buff key works in a development build "
        + "only")]
    private HackShortcutConfig hackShortcuts;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (hitBranchDetection == null) {
            Debug.LogWarning(
                "MonkeyController:\tmust assign Inspector Field: "
                    + "hitBranchDetection",
                this
            );
        }
        if (inputConfig == null) {
            Debug.LogWarning(
                "MonkeyController:\tmust assign Inspector Field: inputConfig",
                this
            );
        }
        else if (inputConfig.MoveAction == null) {
            Debug.LogWarning(
                "MonkeyController:\tmust assign MonkeyInputConfig field: "
                    + "moveAction",
                inputConfig
            );
        }

        if (playerBalanceConfig == null) {
            Debug.LogWarning(
                "MonkeyController:	must assign Inspector Field: "
                    + "playerBalanceConfig",
                this
            );
        }

        if (worldBalanceConfig == null) {
            Debug.LogWarning(
                "MonkeyController:\tmust assign Inspector Field: "
                    + "worldBalanceConfig",
                this
            );
        }

        if (climbConfig == null) {
            Debug.LogWarning(
                "MonkeyController:\tmust assign Inspector Field: "
                    + "climbConfig",
                this
            );
        }

        if (transform.parent == null) {
            Debug.LogError(
                "MonkeyController:\tfail to get Transform: parent",
                this
            );
        }

        body = GetComponent<Rigidbody>();
        if (body == null) {
            Debug.LogError(
                "MonkeyController:\tfail to get Component: Rigidbody",
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
                "MonkeyController:\tready, orbit radius " + $"{orbitRadius}"
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

    private void Update() {
        UpdateSpeedBuffKey();
        UpdateBossJumpKey();
        UpdateSpiderJumpKey();
    }

    private void FixedUpdate() {
        if (
            inputConfig == null
            || playerBalanceConfig == null
            || worldBalanceConfig == null
            || climbConfig == null
        ) {
            return;
        }

        // held, e.g. caught in a web: write no new pose so the body stays
        // put, but keep it awake so the cobra's trigger still registers it
        if (isHeld) {
            if (body != null) {
                body.WakeUp();
                if (hasPullTarget) {
                    body.MovePosition(
                        Vector3.MoveTowards(
                            body.position,
                            pullTarget,
                            activePullSpeedU * Time.fixedDeltaTime
                        )
                    );
                }
            }
            return;
        }

        bool isStunned = IsStunned;
        roughContacts.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);
        bool roughContact = roughContacts.Count > 0 && GameController.I != null
            && GameController.I.IsRunLive && !GameController.I.IsLeaping;
        float targetScale = roughContact ? worldBalanceConfig.RoughBarkSpeedMultiplier : 1f;
        float blendStep = (1f - worldBalanceConfig.RoughBarkSpeedMultiplier)
            * Time.fixedDeltaTime / Mathf.Max(0.01f, worldBalanceConfig.RoughBarkBlendS);
        roughBarkSpeedScale = Mathf.MoveTowards(roughBarkSpeedScale, targetScale, blendStep);
        Vector2 directionalInput = isStunned || MoveAction == null
            ? Vector2.zero
            : MoveAction.action.ReadValue<Vector2>();
        bool hasInput = directionalInput.sqrMagnitude > INPUT_DEADZONE_SQR;

        // the orbit angle is the only horizontal state, so the body sits
        // exactly on its ring every step rather than chasing it
        // an empty stamina bar slows the monkey instead of ending the run
        StaminaBarController staminaBar = StaminaBarController.I;
        bool isExhausted = staminaBar != null && !staminaBar.HasStamina;
        // the controller decides if stamina is low enough for the efx
        if (staminaBar != null && hasInput) {
            staminaBar.NotifyMoveAttempt();
        }
        // the starting zone softens the exhaustion slow down
        bool isInStartingZone = GameController.I != null
            && GameController.I.IsInStartingZone;
        float exhaustedScale = isInStartingZone
            ? playerBalanceConfig.StartingZoneExhaustedSpeedMultiplier
            : climbConfig.ExhaustedSpeedMultiplier;
        float upSpeedScale = isExhausted ? exhaustedScale : 1f;

        // rough bark slows the body, the dev speed buff multiplies it
        float moveScale = roughBarkSpeedScale
            * (IsSpeedBuff ? SPEED_BUFF_SCALE : 1f);

        orbitAngleDeg -= directionalInput.x
            * climbConfig.RotationSpeedDeg * moveScale
            * Time.fixedDeltaTime;

        // speed builds up along a curve of how long the key has been held;
        // with no vertical key the climb eases to a stop instead
        UpdateHeldTimes(directionalInput.y);
        if (upHeldS > 0f) {
            climbSpeedU = directionalInput.y
                * climbConfig.ClimbUpSpeedCurve.Evaluate(upHeldS)
                * upSpeedScale;
        } else if (downHeldS > 0f) {
            climbSpeedU = directionalInput.y
                * climbConfig.ClimbDownSpeedCurve.Evaluate(downHeldS);
        } else {
            climbSpeedU = Mathf.MoveTowards(
                climbSpeedU,
                0f,
                climbConfig.DecelerationU * Time.fixedDeltaTime
            );
        }

        float nextY = transform.position.y
            + climbSpeedU * moveScale * Time.fixedDeltaTime;
        if (isStunned) {
            // knocked off the climb: the ramp is bypassed, fall at a set
            // speed and stop dead on the drop target
            climbSpeedU = 0f;
            upHeldS = 0f;
            downHeldS = 0f;
            nextY = Mathf.Max(
                dropTargetY,
                transform.position.y
                    - playerBalanceConfig.HitDropSpeedU * Time.fixedDeltaTime
            );
        }

        // a ceiling stops the climb short, & eases a body above it down
        if (hasHeightCeiling && nextY > heightCeilingY) {
            nextY = Mathf.Max(
                heightCeilingY,
                Mathf.Min(nextY, transform.position.y)
                    - CEILING_SETTLE_SPEED_U * Time.fixedDeltaTime
            );
            climbSpeedU = Mathf.Min(climbSpeedU, 0f);
        }

        // the stun fall is involuntary and so is descending: only voluntary
        // upward and sideways travel costs stamina
        if (!isStunned && staminaBar != null) {
            float climbedU = Mathf.Max(0f, nextY - transform.position.y);
            float orbitedU = Mathf.Abs(directionalInput.x)
                * climbConfig.RotationSpeedDeg * moveScale
                * Time.fixedDeltaTime
                * Mathf.Deg2Rad
                * orbitRadius;
            staminaBar.DrainByMovement(climbedU, orbitedU);

            // standing still, no key pressed, regains stamina slowly
            if (!hasInput) {
                // the starting zone recovers slower, to teach the limit
                float recoveryScale = isInStartingZone
                    ? playerBalanceConfig.StartingZoneIdleRecoveryMultiplier
                    : 1f;
                staminaBar.AddStamina(
                    climbConfig.IdleStaminaRecoveryPerS
                        * recoveryScale
                        * Time.fixedDeltaTime
                );
            }
        }

        Vector3 next =
            transform.parent.position + CalcOrbitOffset(orbitAngleDeg);
        next.y = nextY;
        if (isReturning) {
            Vector3 step = Vector3.MoveTowards(
                body.position,
                next,
                pullSpeedU * Time.fixedDeltaTime
            );
            isReturning = step != next;
            next = step;
        }
        body.MovePosition(next);
        body.MoveRotation(Quaternion.Euler(0f, orbitAngleDeg, 0f));
    }

    // Event Handlers  #########################################################
    // knock monkey off climb: ignore input for the stun duration, fall the
    // drop distance, both from the balance config. Ignore hit while previous
    // one in effect
    private void OnBranchHit(Collider branch) {
        if (playerBalanceConfig == null) {
            return;
        }

        if (IsStunned) {
            if (Debug.isDebugBuild) {
                Debug.Log(
                    "MonkeyController:\tbranch hit ignored, "
                        + "already stunned"
                );
            }
            return;
        }

        stunEndTime = Time.time + playerBalanceConfig.HitStunDurationS;
        dropTargetY =
            transform.position.y - playerBalanceConfig.HitDropDistanceU;
        if (Debug.isDebugBuild) {
            Debug.Log(
                "MonkeyController:\tbranch hit, control lost for "
                    + $"{playerBalanceConfig.HitStunDurationS}s"
            );
        }
    }

    // constants  ##############################################################
    private const float INPUT_DEADZONE = 0.1f;
    private const float INPUT_DEADZONE_SQR = INPUT_DEADZONE * INPUT_DEADZONE;
    private const float SPEED_BUFF_SCALE = 3f; // dev buff on climb & orbit

    // private members  ########################################################
    private float orbitAngleDeg;
    private float orbitRadius; // dist from parent root, set once in Awake
    private float climbSpeedU; // vertical speed, this script's own
    private float upHeldS; // s the up key has been held without a break
    private float downHeldS; // s the down key has been held without a break
    private float stunEndTime; // Time.time when control returns
    private float dropTargetY; // world Y where hit fall stops
    private readonly HashSet<RoughBarkZone> roughContacts = new();
    private float roughBarkSpeedScale = 1f;
    private bool isHeld; // body frozen in place, see IsHeld
    private Vector3 pullTarget; // world pos the held body slides to
    private bool hasHeightCeiling;
    private float heightCeilingY; // world y the climb stops at, if set
    private const float CEILING_SETTLE_SPEED_U = 8f; // down to the cap; u/s
    private bool hasPullTarget; // whether the held body is being pulled
    private float activePullSpeedU; // speed of the current pull; u/s
    private bool isReturning; // whether the body glides back onto its ring

    private bool IsStunned => Time.time < stunEndTime;

    // body and arms share one action, held on the input config asset
    private InputActionReference MoveAction =>
        inputConfig != null ? inputConfig.MoveAction : null;

    // cached references  ------------------------------------------------------
    private Rigidbody body;

    // private methods  ########################################################
    // dev/hack key toggling the speed buff
    private void UpdateSpeedBuffKey() {
        if (hackShortcuts == null || !HackShortcutConfig.IsActive) {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Key speedBuffKey = hackShortcuts.GetKey(HackAction.SpeedBuff);
        if (
            keyboard == null
            || speedBuffKey == Key.None
            || !keyboard[speedBuffKey].wasPressedThisFrame
        ) {
            return;
        }

        IsSpeedBuff = !IsSpeedBuff;
        if (Debug.isDebugBuild) {
            Debug.Log("MonkeyController:\tspeed buff " + IsSpeedBuff, this);
        }
    }

    // dev/hack key teleporting the monkey to the boss zone start, so the
    // approach to the fight can be tested from its first metre
    // like the boss jump, to the spider jump height below the Spider King;
    // the Cobra King is skipped as if beaten, so it never starts on the way
    private void UpdateSpiderJumpKey() {
        if (hackShortcuts == null || !HackShortcutConfig.IsActive) {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Key spiderJumpKey = hackShortcuts.GetKey(HackAction.SpiderJump);
        if (
            keyboard == null
            || spiderJumpKey == Key.None
            || !keyboard[spiderJumpKey].wasPressedThisFrame
        ) {
            return;
        }

        RampedDifficultyManager ramp = RampedDifficultyManager.I;
        bool isLive = GameController.I != null && GameController.I.IsRunLive;
        if (ramp == null || worldBalanceConfig == null || body == null
            || !isLive) {
            return;
        }

        GameController.I.EndStartingZone();
        BossFightController cobraKing = BossFightController.I;
        if (cobraKing != null) {
            cobraKing.SkipForTest();
        }

        Vector3 target = transform.position;
        target.y = ramp.PlayerGameStartYPosition
            + worldBalanceConfig.SpiderJumpHeightU;
        transform.position = target;
        body.position = target;
        climbSpeedU = 0f;
        upHeldS = 0f;
        downHeldS = 0f;
        Physics.SyncTransforms();
        if (Debug.isDebugBuild) {
            Debug.Log(
                $"MonkeyController:\tspider jump to y {target.y}",
                this
            );
        }
    }

    private void UpdateBossJumpKey() {
        if (hackShortcuts == null || !HackShortcutConfig.IsActive) {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Key bossJumpKey = hackShortcuts.GetKey(HackAction.BossJump);
        if (
            keyboard == null
            || bossJumpKey == Key.None
            || !keyboard[bossJumpKey].wasPressedThisFrame
        ) {
            return;
        }

        RampedDifficultyManager ramp = RampedDifficultyManager.I;
        bool isLive = GameController.I != null && GameController.I.IsRunLive;
        if (ramp == null || worldBalanceConfig == null || body == null
            || !isLive) {
            return;
        }

        // the jump from the starting zone ends it, so the run is normal
        // climbing & the boss trigger can fire (it needs Playing)
        GameController.I.EndStartingZone();

        // a hair over the zone start, so landing there surely counts as inside
        Vector3 target = transform.position;
        target.y = ramp.PlayerGameStartYPosition
            + worldBalanceConfig.BossZoneStartU + 0.05f;
        transform.position = target;
        body.position = target;
        climbSpeedU = 0f;
        upHeldS = 0f;
        downHeldS = 0f;
        Physics.SyncTransforms();
        if (Debug.isDebugBuild) {
            Debug.Log(
                $"MonkeyController:\tboss jump to y {target.y}",
                this
            );
        }
    }

    // counts how long the up or down key has been held; a release or a flip
    // of direction starts the count over
    private void UpdateHeldTimes(float verticalInput) {
        if (verticalInput > INPUT_DEADZONE) {
            upHeldS += Time.fixedDeltaTime;
            downHeldS = 0f;
        } else if (verticalInput < -INPUT_DEADZONE) {
            downHeldS += Time.fixedDeltaTime;
            upHeldS = 0f;
        } else {
            upHeldS = 0f;
            downHeldS = 0f;
        }
    }

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
