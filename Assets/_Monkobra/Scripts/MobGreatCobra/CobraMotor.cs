using System;
using UnityEngine;

/// <summary>
/// Cobra that coils round the trunk and climbs after the monkey without ever
/// stopping or backing down, so once it reaches the monkey, whatever part
/// touches first ends the run. Its base speed rises with the ramped
/// difficulty and by how far it trails the monkey, both as multipliers of
/// the monkey's top climb speed. Each mistake of the monkey (a branch or
/// falling cobra hit, or a web) makes it surge for a while, and mistakes
/// stack, faster and longer, possibly past the monkey's own speed; tuned in
/// <see cref="CobraBalanceConfig"/>. The one exception to never stopping: a
/// used coconut freezes the whole cobra for a while, see
/// <see cref="TryFreeze"/>. The body
/// wraps at least one full loop as a corkscrew with no gap round the trunk, so
/// a monkey dropping onto the cobra's height cannot slip past it by orbiting.
/// Segments are laid along the coil as a chain of rounded beads with small
/// gaps, slightly tapered, with the neck raised,
/// a slow travelling slither wave and eyes on the head. The body keeps its
/// material's own color.
/// Contact itself is handled by <see cref="CobraSensor"/>.
/// </summary>
public class CobraMotor: MonoBehaviour {
    // Public Members  #########################################################
    /// <returns>if the cobra is surging after a mistake of the monkey
    /// </returns>
    public bool IsCatchingUp => isCatchingUp;

    /// <summary>
    /// Raised on every mistake that adds to a surge, with the mistakes
    /// stacked in it so far, for a short warning.
    /// </summary>
    public event Action<int> CatchUpStarted;

    /// <summary>
    /// Raised once when the monkey leaves the starting zone and the cobra
    /// starts chasing, for a short warning.
    /// </summary>
    public event Action ChaseStarted;

    /// <returns>if the cobra guards the boss arena from below instead of
    /// chasing</returns>
    public bool IsGuarding => isGuarding;

    /// <summary>
    /// The box round every body segment's position as posed now, head to
    /// tail, eg for a camera to fit the whole cobra
    /// </summary>
    /// <param name="bounds">the box, world space</param>
    /// <returns>if the body is set up and a box could be made</returns>
    public bool TryGetBodyBounds(out Bounds bounds) {
        bounds = new Bounds();
        if (bodySegments == null || bodySegments.Length == 0) {
            return false;
        }

        bool isFirst = true;
        foreach (Transform segment in bodySegments) {
            if (segment == null) {
                continue;
            }
            if (isFirst) {
                bounds = new Bounds(segment.position, Vector3.zero);
                isFirst = false;
            } else {
                bounds.Encapsulate(segment.position);
            }
        }
        return !isFirst;
    }

    /// <summary>
    /// Stops the chase for the boss fight: the head is set at
    /// <paramref name="headWorldY"/> and held there for good, below the
    /// arena, still circling the trunk, while surges are ignored. Now & then
    /// its neck lifts in a smooth curve and the head springs up as a threat, fatal
    /// only to a monkey lingering that low; the coil never climbs.
    /// </summary>
    public void StartGuard(float headWorldY) {
        if (pathCenter == null) {
            return;
        }

        isGuarding = true;
        nextPeekTime = Time.time + UnityEngine.Random.Range(
            peekIntervalRangeS.x, peekIntervalRangeS.y
        );
        surgeStacks = 0;
        isCatchingUp = false;
        guardHeadHeight = headWorldY - pathCenter.position.y;
        headHeight = guardHeadHeight;
        UpdateBody();
    }

    /// <summary>
    /// Ends <see cref="StartGuard"/>: the chase picks up again from the
    /// guard height, easing up from a standstill.
    /// </summary>
    public void StopGuard() {
        if (!isGuarding) {
            return;
        }
        isGuarding = false;
        guardPeek = 0f;
        currentClimbSpeed = 0f;
        UpdateBody();
    }

    /// <summary>
    /// Throws the head back <paramref name="distanceU"/> down the trunk, eg
    /// when the monkey's gold armor takes the bite; a guarding cobra ends
    /// its peek instead. Clears the running surge.
    /// </summary>
    public void KnockBack(float distanceU) {
        if (isGuarding) {
            guardPeek = 0f;
            nextPeekTime = Time.time + UnityEngine.Random.Range(
                peekIntervalRangeS.x, peekIntervalRangeS.y
            );
        } else {
            headHeight -= Mathf.Max(0f, distanceU);
            currentClimbSpeed = 0f;
            surgeStacks = 0;
            isCatchingUp = false;
        }
        UpdateBody();
    }

    /// <returns>if a coconut holds the cobra still right now</returns>
    public bool IsFrozen => isFrozen;

    /// <returns>freeze time left, 0 when not frozen; s</returns>
    public float FreezeRemainingS =>
        isFrozen ? Mathf.Max(0f, freezeEndTime - Time.time) : 0f;

    /// <returns>full length of the running or last freeze; s</returns>
    public float FreezeDurationS => freezeDurationS;

    /// <summary>
    /// Raised when a freeze starts, with its length; s.
    /// </summary>
    public event Action<float> Frozen;

    /// <summary>
    /// Raised when a freeze runs out and the cobra moves again.
    /// </summary>
    public event Action Thawed;

    // Public Methods  #########################################################
    /// <summary>
    /// Holds the whole cobra still for <paramref name="durationS"/>: no
    /// climbing, orbiting or slithering. Clears the running surge and the
    /// starting zone exit boost, and mistakes made while frozen add no surge.
    /// Afterwards the climb eases back up from a standstill. Contact still
    /// ends the run, see <see cref="CobraSensor"/>.
    /// </summary>
    /// <returns>if the freeze started; false while already frozen, so a
    /// freeze never stacks or resets</returns>
    public bool TryFreeze(float durationS) {
        if (!isActiveAndEnabled || isFrozen || durationS <= 0f) {
            return false;
        }

        isFrozen = true;
        freezeDurationS = durationS;
        freezeEndTime = Time.time + durationS;

        surgeStacks = 0;
        isCatchingUp = false;
        exitBoostEndTime = 0f;
        currentClimbSpeed = 0f;

        Frozen?.Invoke(durationS);
        return true;
    }

    // Inspector Fields  #######################################################
    [Header("References")]
    [SerializeField]
    [Tooltip("trunk axis the cobra orbits, its y is the path's zero height")]
    private Transform pathCenter;

    [SerializeField]
    [Tooltip(
        "body segments, head first; missing ones are cloned from the last "
            + "to fill the coil"
    )]
    private Transform[] bodySegments;

    [SerializeField]
    [Tooltip(
        "cobra balance tuning; catch-up when the monkey is far ahead, "
            + "none if unset"
    )]
    private CobraBalanceConfig cobraBalanceConfig;

    [SerializeField]
    [Tooltip(
        "player balance tuning; hit stun length, so one hit counts once; "
            + "none if unset"
    )]
    private PlayerBalanceConfig playerBalanceConfig;

    [SerializeField]
    [Tooltip(
        "monkey climb tuning; its top up speed is what the base speed "
            + "multipliers scale; inspector climb speed if unset"
    )]
    private MonkeyClimbConfig monkeyClimbConfig;

    [Header("Tree Path")]
    [SerializeField]
    [Tooltip("distance from path center to the body; U")]
    private float orbitRadius = 5.8f;

    [SerializeField]
    [Tooltip("head height above path center at start; U")]
    private float startHeight = 0.5f;

    [SerializeField]
    [Tooltip("head climb speed; U/s")]
    private float climbSpeed = 0.25f;

    [SerializeField]
    [Tooltip("head orbit speed round the trunk; deg/s")]
    private float orbitSpeedDegrees = 20f;

    [SerializeField]
    [Tooltip("orbit direction seen from above")]
    private bool clockwise = true;

    [Header("Coil Shape")]
    [SerializeField]
    [Tooltip(
        "full loops the body wraps round the trunk, head to tail; "
            + "keep at least 1 so no gap is left"
    )]
    private float coilTurns = 1.25f;

    [SerializeField]
    [Tooltip("height the body rises per full loop, head on top; U")]
    private float coilPitchU = 2.5f;

    [SerializeField]
    [Tooltip(
        "largest gap allowed between neighbouring segments along the coil; "
            + "more segments are cloned until it holds; U"
    )]
    private float maxSegmentGapU = 0.6f;

    [Header("Body Look")]
    [SerializeField]
    [Tooltip("body thickness at its thickest point; U")]
    private float maxThicknessU = 0.5f;

    [SerializeField]
    [Tooltip(
        "thickness along the body as a share of max thickness, "
            + "0 = head, 1 = tail tip"
    )]
    private AnimationCurve thicknessProfile = new AnimationCurve(
        new Keyframe(0f, 0.95f),
        new Keyframe(0.1f, 0.9f),
        new Keyframe(0.4f, 1f),
        new Keyframe(1f, 0.75f)
    );

    [SerializeField]
    [Tooltip(
        "how much of the spacing between segments each one fills; below 1 "
            + "leaves a visible gap, the hit shape still has none"
    )]
    private float segmentFill = 0.8f;

    [SerializeField]
    [Tooltip("head length; U")]
    private float headLengthU = 0.75f;

    [SerializeField]
    [Tooltip("head width as a multiple of its thickness, cobra-hood flat")]
    private float headWidthRatio = 1.3f;

    [Header("Neck & Slither")]
    [SerializeField]
    [Tooltip("body length from the head that rises off the coil; U")]
    private float neckLengthU = 2f;

    [SerializeField]
    [Tooltip(
        "how far the head rises above the coil; the coil sits this much "
            + "lower, so the head still stops at the player's height; U"
    )]
    private float neckLiftU = 0.8f;

    [SerializeField]
    [Tooltip("up-down slither wave height, faded out at the neck; U")]
    private float waveAmplitudeU = 0.12f;

    [SerializeField]
    [Tooltip("body length of one slither wave; U")]
    private float waveLengthU = 2.5f;

    [SerializeField]
    [Tooltip("slither waves passing down the body; 1/s")]
    private float waveSpeedHz = 0.4f;

    [Header("Cartoon Appearance")]
    [SerializeField]
    private CartoonCobraVisual.Settings cartoonAppearance = new();
    [Header("Chase Head Pose")]
    [SerializeField, Min(0.1f)]
    private float headFollowSharpness = 5f;

    [SerializeField, Min(0f)]
    private float headTurnSpeedDegrees = 180f;

    [SerializeField, Min(.1f)] private float headRaiseStartDistanceU = 9f;
    [SerializeField, Min(.1f)] private float headRaiseFullDistanceU = 3.5f;

    private float ChaseHeadRaiseBlend() {
        if (isGuarding) return 1f;
        if (player == null || pathCenter == null) return 0f;
        float distance = Vector3.Distance(player.position, GetPathPosition(headAngle,headHeight));
        return Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(headRaiseStartDistanceU,
            Mathf.Min(headRaiseFullDistanceU,headRaiseStartDistanceU-.01f),distance));
    }

    [Header("Close Chase Hiss")]
    [SerializeField] private AudioClip proximityHiss;
    [SerializeField, Min(.1f)] private float hissStartDistanceU = 7f;
    [SerializeField, Min(0f)] private float hissExitPaddingU = 1f;
    [SerializeField, Min(.1f)] private float hissIntervalS = 2.8f;
    [SerializeField, Range(0f,1f)] private float hissVolume = .75f;
    [SerializeField, Range(.1f,2f)] private float hissPitch = .82f;
    [SerializeField, Min(.1f)] private float hissMinDistanceU = 3f;
    [SerializeField, Min(.1f)] private float hissMaxDistanceU = 35f;
    [SerializeField, Range(0f,1f)] private float chaseAlertHissVolume = .85f;
    private AudioSource proximityHissSource,chaseAlertHissSource;
    private CapsuleCollider[] hissBodyShapes;
    private float nextProximityHissTime;
    private bool wasInHissRange;

    private void CreateProximityHiss() {
        if (proximityHiss == null) return;
        hissBodyShapes=new CapsuleCollider[bodySegments.Length];
        for(int i=0;i<bodySegments.Length;i++) {
            if(bodySegments[i]!=null)bodySegments[i].TryGetComponent(out hissBodyShapes[i]);
        }
        var voice=new GameObject("GreatCobraHiss");
        voice.transform.SetParent(transform,false);
        proximityHissSource=voice.AddComponent<AudioSource>();
        proximityHissSource.clip=proximityHiss;
        proximityHissSource.playOnAwake=false;
        proximityHissSource.loop=false;
        proximityHissSource.volume=hissVolume;
        proximityHissSource.pitch=hissPitch;
        proximityHissSource.spatialBlend=1f;
        proximityHissSource.dopplerLevel=0f;
        proximityHissSource.rolloffMode=AudioRolloffMode.Linear;
        proximityHissSource.minDistance=hissMinDistanceU;
        proximityHissSource.maxDistance=Mathf.Max(hissMinDistanceU,hissMaxDistanceU);
        var alert=new GameObject("GreatCobraChaseAlertHiss");
        alert.transform.SetParent(transform,false);
        chaseAlertHissSource=alert.AddComponent<AudioSource>();
        chaseAlertHissSource.clip=proximityHiss;
        chaseAlertHissSource.playOnAwake=false;
        chaseAlertHissSource.loop=false;
        chaseAlertHissSource.volume=chaseAlertHissVolume;
        chaseAlertHissSource.pitch=hissPitch;
        // The introduction shot must be audible while the monkey is still far above the cobra.
        chaseAlertHissSource.spatialBlend=0f;
    }

    public void PlayChaseAlertHiss() {
        if(chaseAlertHissSource==null || !isActiveAndEnabled || isGuarding || isFrozen)return;
        StopProximityHiss();
        chaseAlertHissSource.Play();
        nextProximityHissTime=Time.time+Mathf.Max(hissIntervalS,proximityHiss.length/Mathf.Max(.1f,hissPitch));
    }

    private void UpdateProximityHiss(Vector3 headPosition) {
        if (proximityHissSource == null) return;
        if(chaseAlertHissSource!=null && (isFrozen || isGuarding || (GameController.I!=null && !GameController.I.IsRunLive)))chaseAlertHissSource.Stop();
        bool alertPlaying=chaseAlertHissSource!=null && chaseAlertHissSource.isPlaying;
        Vector3 nearest=headPosition;
        float distanceSquared=player!=null?(player.position-nearest).sqrMagnitude:float.PositiveInfinity;
        if(player!=null && hissBodyShapes!=null)foreach(var shape in hissBodyShapes) {
            if(shape==null || !shape.enabled || !shape.gameObject.activeInHierarchy)continue;
            Vector3 point=shape.ClosestPoint(player.position);
            float candidate=(point-player.position).sqrMagnitude;
            if(candidate<distanceSquared){distanceSquared=candidate;nearest=point;}
        }
        proximityHissSource.transform.position=nearest;
        bool near=player != null && !isFrozen && !isGuarding && Time.timeScale>0f
            && (GameController.I == null || GameController.I.IsRunLive)
            && distanceSquared<=Mathf.Pow(hissStartDistanceU+(wasInHissRange?hissExitPaddingU:0f),2f);
        if (!near) { StopProximityHiss(); return; }
        if (!wasInHissRange && !alertPlaying) nextProximityHissTime=Time.time;
        wasInHissRange=true;
        if (!alertPlaying && Time.time>=nextProximityHissTime && !proximityHissSource.isPlaying) {
            proximityHissSource.Play();
            nextProximityHissTime=Time.time+Mathf.Max(hissIntervalS,proximityHiss.length/Mathf.Max(.1f,hissPitch));
        }
    }

    private void StopProximityHiss() {
        wasInHissRange=false;
        if (proximityHissSource!=null && proximityHissSource.isPlaying) proximityHissSource.Stop();
    }

    private void OnDisable() {
        StopProximityHiss();
        if(chaseAlertHissSource!=null)chaseAlertHissSource.Stop();
    }

    private Quaternion cartoonHeadRotation;
    private bool cartoonHeadRotationInitialized;

    [Header("Eyes")]
    [SerializeField]
    [Tooltip("eye diameter, eyes are skipped at 0; U")]
    private float eyeSizeU = 0.14f;

    [Header("Boss Arena Guard")]
    [SerializeField]
    [Tooltip("least & most time between two peeks while guarding; s")]
    private Vector2 peekIntervalRangeS = new Vector2(4f, 7f);

    [SerializeField]
    [Min(0f)]
    [Tooltip("how high the head pokes straight up over the coil; u")]
    private float peekRiseU = 3.5f;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("length of neck, from the head, that lifts to poke up; u")]
    private float peekNeckU = 3.5f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("how far the lifting neck bows out from the trunk; u")]
    private float peekCurlU = 0.8f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("peek: time up, held, & back down; s")]
    private Vector3 peekTimesS = new Vector3(0.25f, 0.4f, 0.5f);

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (pathCenter == null) {
            Debug.LogError(
                "CobraMotor:\tmust assign Inspector Field: pathCenter",
                this
            );
        }

        if (bodySegments == null || bodySegments.Length == 0) {
            Debug.LogError(
                "CobraMotor:\tmust assign Inspector Field: bodySegments",
                this
            );
        }

        if (pathCenter == null || bodySegments == null
            || bodySegments.Length == 0) {
            enabled = false;
            return;
        }

        if (cobraBalanceConfig == null) {
            Debug.LogWarning(
                "CobraMotor:\tno Inspector Field: cobraBalanceConfig, "
                    + "the cobra will not catch up",
                this
            );
        }

        FillCoilSegments();
        segmentPositions = new Vector3[bodySegments.Length];
        StyleBody();
        CreateProximityHiss();
    }

    private void Start() {
        if (!enabled) {
            return;
        }

        headHeight = startHeight;
        currentClimbSpeed = climbSpeed;
        UpdateBody();
    }

    private void Update() {
        if (player == null) {
            GameObject playerObject = GameObject.FindGameObjectWithTag(
                "Player"
            );

            if (playerObject != null) {
                player = playerObject.transform;
                ListenForMistakes(playerObject);
            }
        }

        if (isFrozen) {
            if (Time.time < freezeEndTime) {
                // the whole body holds still, slither wave included
                frozenTimeS += Time.deltaTime;
                return;
            }
            isFrozen = false;
            Thawed?.Invoke();
        }

        headAngle += Direction * orbitSpeedDegrees * Time.deltaTime;

        if (isGuarding) {
            UpdateGuard();
            UpdateBody();
            return;
        }

        StartExitBoostOnZoneExit();

        float baseClimbSpeed = BaseClimbSpeed();
        float targetClimbSpeed = baseClimbSpeed;

        // a surge ends when its stacked time runs out, and its stack with it
        if (surgeStacks > 0 && Time.time >= surgeEndTime) {
            surgeStacks = 0;
        }
        isCatchingUp = surgeStacks > 0 && cobraBalanceConfig != null;

        if (isCatchingUp) {
            // a surge never slows near the monkey: it is the punishment
            targetClimbSpeed = Mathf.Max(
                baseClimbSpeed,
                Mathf.Min(
                    baseClimbSpeed
                        + cobraBalanceConfig.GetSurgeSpeedBonusU(surgeStacks),
                    cobraBalanceConfig.CobraMaxSurgeSpeedU
                )
            );
        } else if (Time.time < exitBoostEndTime) {
            // leaving the starting zone releases the cobra with a burst
            targetClimbSpeed = baseClimbSpeed
                + cobraBalanceConfig.CobraExitBoostSpeedU;
        }

        // ease toward the target, so a speed-up or a slow-down never jumps
        float speedChangeRate = cobraBalanceConfig != null
            ? cobraBalanceConfig.CobraSpeedChangeRateU
            : float.PositiveInfinity;
        currentClimbSpeed = Mathf.MoveTowards(
            currentClimbSpeed,
            targetClimbSpeed,
            speedChangeRate * Time.deltaTime
        );

        // always up, never held at the player's height and never down: the
        // coil climbs on into the monkey, and a monkey moving or dropping
        // down runs into it instead of pushing it
        // except in the starting zone: the cobra stays active & orbits, but
        // holds its height until the zone ends
        if (!IsInStartingZone) {
            headHeight += currentClimbSpeed * Time.deltaTime;
        }

        UpdateBody();
    }

    private static bool IsInStartingZone =>
        GameController.I != null
        && GameController.I.State == GameState.StartingZone;

    private void OnDestroy() {
        cartoonVisual?.Dispose();
        if (playerSensor != null) {
            playerSensor.BranchHit -= OnMonkeyHit;
        }
        if (playerStruggle != null) {
            playerStruggle.Trapped -= OnMonkeyTrapped;
        }
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        orbitRadius = Mathf.Max(0.01f, orbitRadius);
        climbSpeed = Mathf.Max(0f, climbSpeed);
        orbitSpeedDegrees = Mathf.Max(0f, orbitSpeedDegrees);
        coilTurns = Mathf.Max(1f, coilTurns);
        coilPitchU = Mathf.Max(0f, coilPitchU);
        maxSegmentGapU = Mathf.Max(0.05f, maxSegmentGapU);
        maxThicknessU = Mathf.Max(0.01f, maxThicknessU);
        segmentFill = Mathf.Clamp(segmentFill, 0.2f, 2f);
        headLengthU = Mathf.Max(0.01f, headLengthU);
        headWidthRatio = Mathf.Max(0.1f, headWidthRatio);
        neckLengthU = Mathf.Max(0.01f, neckLengthU);
        neckLiftU = Mathf.Max(0f, neckLiftU);
        waveAmplitudeU = Mathf.Max(0f, waveAmplitudeU);
        waveLengthU = Mathf.Max(0.01f, waveLengthU);
        eyeSizeU = Mathf.Max(0f, eyeSizeU);
    }

    // private members  ########################################################
    private static readonly int BASE_COLOR_ID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    private CartoonCobraVisual cartoonVisual;
    private float[] cartoonRadii;
    private Vector3[] cartoonOutward;

    private float headAngle;
    private float headHeight;
    private bool isGuarding; // holding below the boss arena, never chasing
    private float nextPeekTime; // Time.time the next guard peek starts
    private float guardPeek; // 0~1 how far the head pokes up right now
    private float guardHeadHeight; // head height held while guarding
    private float currentClimbSpeed; // eased toward the target; u/s
    private bool isCatchingUp;
    private int surgeStacks; // mistakes stacked in the running surge
    private float surgeEndTime; // Time.time the running surge ends
    private float exitBoostEndTime; // Time.time the zone exit boost ends
    private GameState lastGameState; // state seen on the previous Update
    private float nextCountedHitTime; // Time.time b4 which hits add nothing
    private bool isFrozen; // held still by a coconut
    private float freezeEndTime; // Time.time the running freeze ends
    private float freezeDurationS; // length of the running or last freeze; s
    private float frozenTimeS; // total time spent frozen, kept off the wave; s
    private HitBranchSensor playerSensor;
    private SpiderWebStruggle playerStruggle;
    private Transform player;
    private Vector3[] segmentPositions;

    private float Direction => clockwise ? -1f : 1f;

    private int LastIndex => Mathf.Max(1, bodySegments.Length - 1);

    /// <returns>angle between neighbouring segments so the whole body spans
    /// <see cref="coilTurns"/> loops; deg</returns>
    private float SegmentAngleStepDeg => coilTurns * 360f / LastIndex;

    /// <returns>height drop between neighbouring segments; U</returns>
    private float SegmentHeightStepU => coilTurns * coilPitchU / LastIndex;

    /// <returns>body length of the whole coil, head to tail; U</returns>
    private float CoilLengthU => Mathf.Sqrt(
        Mathf.Pow(coilTurns * 2f * Mathf.PI * orbitRadius, 2f)
            + Mathf.Pow(coilTurns * coilPitchU, 2f)
    );

    /// <returns>body length between neighbouring segments; U</returns>
    private float SegmentArcStepU => CoilLengthU / LastIndex;

    // private methods  ########################################################
    /// <returns>vertical gap from the coil top up to the monkey, never
    /// below 0; U</returns>
    private float CalcDistanceBelowPlayer() {
        float coilTopHeight = headHeight - neckLiftU;
        return Mathf.Max(
            0f,
            player.position.y - pathCenter.position.y - coilTopHeight
        );
    }

    /// <returns>climb speed outside a surge: the monkey's top climb speed
    /// times the config's base speed curve at the monkey's ramped difficulty
    /// (0~2), so the chase tightens as the climb goes on, times the distance
    /// curve, so a far-behind cobra runs faster; else the inspector's climb
    /// speed; u/s</returns>
    private float BaseClimbSpeed() {
        if (
            cobraBalanceConfig == null
            || monkeyClimbConfig == null
            || player == null
            || RampedDifficultyManager.I == null
        ) {
            return climbSpeed;
        }
        float difficulty =
            RampedDifficultyManager.I.GetRampedValueFromYPosition(
                player.position.y
            );
        float distanceMultiplier =
            cobraBalanceConfig.CobraDistanceSpeedCurve.Evaluate(
                CalcDistanceBelowPlayer()
            );
        float baseMultiplier = Mathf.Clamp(
            cobraBalanceConfig.CobraBaseSpeedCurve.Evaluate(difficulty),
            0f,
            2f
        );
        return Mathf.Max(
            0f,
            monkeyClimbConfig.TopClimbUpSpeedU
                * baseMultiplier
                * distanceMultiplier
        );
    }

    // the starting zone ending (to Playing) starts the one-off exit boost
    private void StartExitBoostOnZoneExit() {
        if (GameController.I == null) {
            return;
        }

        GameState state = GameController.I.State;
        bool didExitZone = lastGameState == GameState.StartingZone
            && state == GameState.Playing;
        lastGameState = state;
        if (!didExitZone) {
            return;
        }

        if (cobraBalanceConfig != null) {
            exitBoostEndTime =
                Time.time + cobraBalanceConfig.CobraExitBoostDurationS;
        }
        ChaseStarted?.Invoke();
    }

    // a branch or falling cobra hit, or a web, costs the monkey time; the
    // cobra surges to make the mistake felt
    private void ListenForMistakes(GameObject playerObject) {
        playerSensor =
            playerObject.GetComponentInChildren<HitBranchSensor>(true);
        if (playerSensor != null) {
            playerSensor.BranchHit += OnMonkeyHit;
        }
        playerStruggle = playerObject.GetComponent<SpiderWebStruggle>();
        if (playerStruggle != null) {
            playerStruggle.Trapped += OnMonkeyTrapped;
        }
    }

    // one stun is one mistake: hits the monkey ignores while stunned, e.g.
    // a 2nd branch on the way down, add nothing
    private void OnMonkeyHit(Collider hazard) {
        if (isGuarding) {
            return;
        }
        if (playerBalanceConfig != null && Time.time < nextCountedHitTime) {
            return;
        }
        if (playerBalanceConfig != null) {
            nextCountedHitTime =
                Time.time + playerBalanceConfig.HitStunDurationS;
        }
        StartSurge();
    }

    // raised on every trap; its flag only says if it is the first trap of
    // the scene, so it is ignored here
    private void OnMonkeyTrapped(bool isFirstTrap) {
        if (isGuarding) {
            return;
        }
        StartSurge();
    }

    // guarding: hold still below the arena, the head poking up now & then
    private void UpdateGuard() {
        float peek = 0f;
        float t = Time.time - nextPeekTime;
        float upS = peekTimesS.x;
        float holdS = peekTimesS.y;
        float downS = peekTimesS.z;
        if (t >= 0f) {
            if (t < upS) {
                // springs up, overshooting a little before it settles
                peek = EaseOutBack(t / upS);
            } else if (t < upS + holdS) {
                // sways gently while it looks around
                peek = 1f + 0.05f * Mathf.Sin((t - upS) * 9f);
            } else if (t < upS + holdS + downS) {
                float d = (t - upS - holdS) / downS;
                peek = 1f - d * d * (3f - 2f * d);
            } else {
                nextPeekTime = Time.time + UnityEngine.Random.Range(
                    peekIntervalRangeS.x, peekIntervalRangeS.y
                );
            }
        }
        headHeight = guardHeadHeight;
        guardPeek = peek;
    }

    // a peek lifts the first stretch of neck off the coil in a smooth S:
    // it bows out from the trunk, then rises, the head on top; the bend
    // fades in along the neck, so it joins the coil without a kink
    private void ApplyGuardPeek(float arcStep) {
        if (guardPeek <= 0f || segmentPositions.Length < 2) {
            return;
        }
        int anchor = Mathf.Clamp(
            Mathf.CeilToInt(peekNeckU / Mathf.Max(0.01f, arcStep)),
            1,
            segmentPositions.Length - 1
        );
        Vector3 root = segmentPositions[anchor];
        Vector3 outward = root - pathCenter.position;
        outward.y = 0f;
        outward = outward.sqrMagnitude > 0.0001f
            ? outward.normalized
            : Vector3.forward;
        float sway = Mathf.Sin(Time.time * 2.3f) * 0.25f * guardPeek;
        Vector3 side = Vector3.Cross(Vector3.up, outward);

        for (int i = 0; i < anchor; i++) {
            float s = (anchor - i) / (float)anchor; // 0 at the coil, 1 head
            Vector3 raised = root
                + Vector3.up * peekRiseU * guardPeek * Mathf.Pow(s, 1.15f)
                + outward * peekCurlU * guardPeek * Mathf.Sin(s * Mathf.PI)
                + side * sway * s * s;
            float blend = Mathf.Clamp01(guardPeek) * (s * s * (3f - 2f * s));
            segmentPositions[i] = Vector3.Lerp(
                segmentPositions[i], raised, Mathf.Clamp01(blend * 1.6f)
            );
        }
    }

    private static float EaseOutBack(float x) {
        const float c1 = 1.4f;
        const float c3 = c1 + 1f;
        float m = x - 1f;
        return 1f + c3 * m * m * m + c1 * m * m;
    }

    // each mistake adds a stack and surge time, both capped by the config;
    // a frozen cobra ignores mistakes
    private void StartSurge() {
        if (
            cobraBalanceConfig == null
            || cobraBalanceConfig.CobraSurgePerMistakeS <= 0f
            || isFrozen
            // it holds still in the starting zone, so no surge & no cue
            || IsInStartingZone
        ) {
            return;
        }
        float remainingS = Mathf.Max(0f, surgeEndTime - Time.time);
        if (surgeStacks == 0) {
            remainingS = 0f;
        }
        surgeEndTime =
            Time.time
            + Mathf.Min(
                remainingS + cobraBalanceConfig.CobraSurgePerMistakeS,
                cobraBalanceConfig.CobraMaxSurgeS
            );
        surgeStacks++;
        CatchUpStarted?.Invoke(surgeStacks);
    }

    /// <summary>
    /// Clone the last body segment until neighbouring segments sit no more
    /// than <see cref="maxSegmentGapU"/> apart along the whole coil, so the
    /// loop round the trunk has no hole the monkey fits through.
    /// </summary>
    private void FillCoilSegments() {
        int neededCount = Mathf.CeilToInt(CoilLengthU / maxSegmentGapU) + 1;

        if (neededCount <= bodySegments.Length) {
            return;
        }

        Transform template = bodySegments[bodySegments.Length - 1];

        if (template == null) {
            Debug.LogError(
                "CobraMotor:\tlast body segment is empty, cannot fill coil",
                this
            );
            return;
        }

        int authoredCount = bodySegments.Length;
        Transform[] filledSegments = new Transform[neededCount];
        bodySegments.CopyTo(filledSegments, 0);

        for (int i = authoredCount; i < neededCount; i++) {
            Transform clone = Instantiate(template, template.parent);
            clone.name = $"Segment_{i:00}";
            filledSegments[i] = clone;
        }

        bodySegments = filledSegments;

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"CobraMotor:\tcloned {neededCount - authoredCount} segments, "
                    + $"{neededCount} in total for {coilTurns:F2} loops",
                this
            );
        }
    }

    /// <summary>
    /// Size every segment into a rounded bead, slightly tapered, flatten the
    /// head into a hood and give the head eyes. Each bead's capsule collider
    /// is stretched to reach its neighbours, so the visible gaps never become
    /// holes in the hit shape. Runs once, the shape along the body never
    /// changes. Colors are left to the segments' material.
    /// </summary>
    private void StyleBody() {
        float arcStep = SegmentArcStepU;
        float segmentLength = arcStep * segmentFill;
        MaterialPropertyBlock colorBlock = new();

        for (int i = 0; i < bodySegments.Length; i++) {
            Transform segment = bodySegments[i];

            if (segment == null) {
                continue;
            }

            float bodyFraction = (float)i / LastIndex;
            float thickness =
                maxThicknessU * Mathf.Max(
                    0.05f,
                    thicknessProfile.Evaluate(bodyFraction)
                );

            // capsule mesh: local y runs along the body, 2 U long at scale 1,
            // local x is width, local z is depth off the trunk
            if (i == 0) {
                segment.localScale = new Vector3(
                    thickness * headWidthRatio,
                    headLengthU * 0.5f,
                    thickness * 0.75f
                );
            } else {
                segment.localScale = new Vector3(
                    thickness,
                    segmentLength * 0.5f,
                    thickness
                );
            }

            StretchHitShape(segment, arcStep);
        }

        if (cartoonAppearance.enabled && bodySegments[0] != null
            && bodySegments[0].TryGetComponent(out Renderer source)) {
            cartoonVisual = new CartoonCobraVisual(transform, source.sharedMaterial, cartoonAppearance);
            cartoonVisual.BuildHead(bodySegments[0], maxThicknessU);
            cartoonRadii = new float[bodySegments.Length];
            cartoonOutward = new Vector3[bodySegments.Length];
            for (int i = 0; i < bodySegments.Length; i++) {
                if (bodySegments[i].TryGetComponent(out Renderer renderer)) {
                    renderer.enabled = false;
                }
                float t = i / (float)LastIndex;
                cartoonRadii[i] = maxThicknessU * 0.5f
                    * Mathf.Max(0.05f, thicknessProfile.Evaluate(t))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, t)));
            }
        } else if (eyeSizeU > 0f && bodySegments[0] != null) {
            AddEyes(bodySegments[0], colorBlock);
        }
    }

    /// <summary>
    /// Make a segment's capsule collider run along the body and span a bit
    /// more than the spacing to the next segment, whatever its visible size.
    /// </summary>
    private static void StretchHitShape(Transform segment, float arcStep) {
        if (!segment.TryGetComponent(out CapsuleCollider hitShape)) {
            return;
        }

        float lengthScale = Mathf.Max(0.0001f, segment.localScale.y);
        hitShape.direction = 1;
        hitShape.center = Vector3.zero;
        hitShape.height = Mathf.Max(
            2f * hitShape.radius,
            arcStep * 1.1f / lengthScale
        );
    }

    private void AddEyes(Transform head, MaterialPropertyBlock colorBlock) {
        Vector3 headScale = head.localScale;
        Renderer headRenderer = head.GetComponent<Renderer>();

        for (int side = -1; side <= 1; side += 2) {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = side < 0 ? "EyeLeft" : "EyeRight";

            // eyes are looks only, never part of the hit shape
            Destroy(eye.GetComponent<Collider>());

            eye.transform.SetParent(head, false);

            // head-local: toward the front (+y), off to each side (x), on the
            // face turned away from the trunk (-z)
            eye.transform.localPosition = new Vector3(
                side * 0.32f,
                0.55f,
                -0.3f
            );
            eye.transform.localScale = new Vector3(
                eyeSizeU / headScale.x,
                eyeSizeU / headScale.y,
                eyeSizeU / headScale.z
            );

            Renderer eyeRenderer = eye.GetComponent<Renderer>();

            if (headRenderer != null) {
                eyeRenderer.sharedMaterial = headRenderer.sharedMaterial;
            }

            SetColor(eyeRenderer, Color.black, colorBlock);
        }
    }

    private static void SetColor(
        Renderer target,
        Color color,
        MaterialPropertyBlock colorBlock
    ) {
        if (target == null || target.sharedMaterial == null) {
            return;
        }

        Material material = target.sharedMaterial;
        target.GetPropertyBlock(colorBlock);

        if (material.HasProperty(BASE_COLOR_ID)) {
            colorBlock.SetColor(BASE_COLOR_ID, color);
        } else if (material.HasProperty(COLOR_ID)) {
            colorBlock.SetColor(COLOR_ID, color);
        } else {
            return;
        }

        target.SetPropertyBlock(colorBlock);
        colorBlock.Clear();
    }

    private void UpdateBody() {
        float angleStep = SegmentAngleStepDeg;
        float heightStep = SegmentHeightStepU;
        float arcStep = SegmentArcStepU;
        // time spent frozen is skipped, so the wave resumes where it stopped
        float wavePhase =
            (Time.time - frozenTimeS) * waveSpeedHz * 2f * Mathf.PI;

        // the coil sits lower by the neck lift, so the raised head tip is
        // what reaches headHeight
        float coilTopHeight = headHeight - neckLiftU;

        for (int i = 0; i < bodySegments.Length; i++) {
            float bodyDistance = i * arcStep;
            float neckFraction = Mathf.Clamp01(bodyDistance / neckLengthU);

            float lift = neckLiftU * ChaseHeadRaiseBlend() * (1f - neckFraction) * (1f - neckFraction);
            float wave = waveAmplitudeU * neckFraction * Mathf.Sin(
                wavePhase - bodyDistance / waveLengthU * 2f * Mathf.PI
            );

            segmentPositions[i] = GetPathPosition(
                headAngle - Direction * angleStep * i,
                coilTopHeight - heightStep * i + lift + wave
            );
        }
        ApplyGuardPeek(arcStep);

        for (int i = 0; i < bodySegments.Length; i++) {
            Transform segment = bodySegments[i];

            if (segment == null) {
                continue;
            }

            segment.position = segmentPositions[i];

            if (bodySegments.Length < 2) {
                continue;
            }

            // lay the capsule along the body, pointing toward the head
            Vector3 alongBody = i == 0
                ? segmentPositions[0] - segmentPositions[1]
                : segmentPositions[i - 1] - segmentPositions[i];
            Vector3 awayFromTrunk = segmentPositions[i] - pathCenter.position;
            awayFromTrunk.y = 0f;

            if (alongBody.sqrMagnitude < 0.0001f
                || awayFromTrunk.sqrMagnitude < 0.0001f) {
                continue;
            }

            segment.rotation =
                Quaternion.LookRotation(alongBody, awayFromTrunk)
                * Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private void LateUpdate() {
        if (cartoonVisual == null) {
            return;
        }
        for (int i = 0; i < segmentPositions.Length; i++) {
            Vector3 radial = segmentPositions[i] - pathCenter.position;
            radial.y = 0f;
            cartoonOutward[i] = radial.normalized;
        }
        Vector3 travelDirection = segmentPositions[0] - segmentPositions[1];
        if (!isGuarding) travelDirection.y = 0f;
        travelDirection = Vector3.ProjectOnPlane(travelDirection, cartoonOutward[0]).normalized;
        if (travelDirection.sqrMagnitude < 0.001f) travelDirection = Vector3.Cross(cartoonOutward[0], Vector3.up) * Direction;
        Vector3 neckDirection = travelDirection;
        if (!isGuarding && player != null) {
            Vector3 toPlayer = player.position - segmentPositions[0];
            Vector3 towardPlayer = Vector3.ProjectOnPlane(toPlayer, cartoonOutward[0]);
            if (towardPlayer.sqrMagnitude > 0.001f) {
                neckDirection = Vector3.Slerp(travelDirection,towardPlayer.normalized,ChaseHeadRaiseBlend()).normalized;
            }
        }
        Quaternion desiredHeadRotation = Quaternion.LookRotation(cartoonOutward[0], neckDirection);
        if (!cartoonHeadRotationInitialized) {
            cartoonHeadRotation = desiredHeadRotation;
            cartoonHeadRotationInitialized = true;
        } else if (!isFrozen) {
            Quaternion followedRotation = Quaternion.Slerp(cartoonHeadRotation, desiredHeadRotation,
                1f - Mathf.Exp(-headFollowSharpness * Time.deltaTime));
            cartoonHeadRotation = Quaternion.RotateTowards(cartoonHeadRotation, followedRotation,
                headTurnSpeedDegrees * Time.deltaTime);
        }
        Quaternion headRotation = cartoonHeadRotation;
        Vector3 headPosition = segmentPositions[0];
        if (cartoonAppearance.referenceHead != null) {
            Bounds headBounds = cartoonAppearance.referenceHead.GetComponent<MeshFilter>().sharedMesh.bounds;
            float headSize = maxThicknessU * cartoonAppearance.headScale;
            float innerProjection = 0f;
            for (int corner = 0; corner < 8; corner++) {
                Vector3 point = headBounds.center + Vector3.Scale(headBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                innerProjection = Mathf.Min(innerProjection,
                    Vector3.Dot(headRotation * (point * headSize), cartoonOutward[0]));
            }
            // Keep the inward-facing muzzle clear of the trunk while the neck stays upright.
            headPosition += cartoonOutward[0]
                * Mathf.Max(0f, -innerProjection - maxThicknessU * 0.5f);
        } else {
            headPosition += cartoonOutward[0] * maxThicknessU * 0.6f
                + Vector3.up * maxThicknessU * 0.2f;
        }
        cartoonVisual.PoseHead(headPosition, headRotation);
        UpdateProximityHiss(headPosition);
        cartoonVisual.UpdateBody(segmentPositions, cartoonRadii, cartoonOutward, bellyInward: true);
    }

    private Vector3 GetPathPosition(float angleDegrees, float height) {
        float radians = angleDegrees * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            height,
            Mathf.Sin(radians) * orbitRadius
        );

        return pathCenter.position + offset;
    }
}
