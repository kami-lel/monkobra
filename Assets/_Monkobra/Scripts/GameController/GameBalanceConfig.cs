using UnityEngine;

/// <summary>
/// Single home for the game balance knobs: stamina budget, climb speeds,
/// branch-hit penalty, score rules, branch generation, spider web
/// generation, falling cobras and the great cobra's catch-up.
/// </summary>
[CreateAssetMenu(
    fileName = "GameBalanceConfig",
    menuName = "Scriptable Objects/GameBalanceConfig"
)]
public class GameBalanceConfig: ScriptableObject {
    // Public Members  #########################################################
    /// <returns>stamina at level start, also the ceiling for restores</returns>
    public float MaxStamina => maxStamina;

    /// <returns>stamina lost per unit climbed upward</returns>
    public float StaminaUpwardDrainPerU => staminaUpwardDrainPerU;

    /// <returns>stamina lost per unit orbited round the trunk</returns>
    public float StaminaSidewayDrainPerU => staminaSidewayDrainPerU;

    /// <returns>stamina restored per grabbed fruit</returns>
    public float FruitStaminaRestore => fruitStaminaRestore;

    /// <returns>climb speed moving up; u/s</returns>
    public float UpSpeedU => upSpeedU;

    /// <returns>climb speed moving down; u/s</returns>
    public float DownSpeedU => downSpeedU;

    /// <returns>multiplier on upward climb speed while stamina is empty
    /// </returns>
    public float ExhaustedSpeedMultiplier => exhaustedSpeedMultiplier;

    /// <returns>stamina regained per s while the monkey gives no movement
    /// input</returns>
    public float IdleStaminaRecoveryPerS => idleStaminaRecoveryPerS;

    /// <returns>how long control is lost after a branch hit; s</returns>
    public float HitStunDurationS => hitStunDurationS;

    /// <returns>how far the monkey falls on a branch hit; u</returns>
    public float HitDropDistanceU => hitDropDistanceU;

    /// <returns>how fast the monkey falls on a branch hit; u/s</returns>
    public float HitDropSpeedU => hitDropSpeedU;

    /// <returns>points earned per meter of height</returns>
    public int PointsPerMeter => pointsPerMeter;

    /// <returns>Unity units per meter of height</returns>
    public float UnitsPerMeter => unitsPerMeter;

    /// <returns>points awarded per score reward pickup</returns>
    public int ScoreRewardPoints => scoreRewardPoints;

    /// <returns>x: player y above start y; world u; y: ramped difficulty
    /// 0~1</returns>
    public AnimationCurve RampedDifficultyCurve => rampedDifficultyCurve;

    /// <returns>branch creation attempts rolled per segment</returns>
    public int BranchGenerationAttemptCount => branchGenerationAttemptCount;

    /// <returns>no branch grows within this distance of the monkey while the
    /// starting zone lasts, so a restart never spawns the monkey into one;
    /// u</returns>
    public float StartingZoneBranchClearRadiusU =>
        startingZoneBranchClearRadiusU;

    /// <returns>x: ramped difficulty 0~1; y: chance 0~1 that one attempt
    /// creates a branch</returns>
    public AnimationCurve BranchSpawnProbabilityCurve =>
        branchSpawnProbabilityCurve;

    /// <returns>x: ramped difficulty 0~1; y: chance 0~1 that a generated
    /// branch bears a banana</returns>
    public AnimationCurve BananaSpawnProbabilityCurve =>
        bananaSpawnProbabilityCurve;

    /// <returns>web creation attempts rolled per segment</returns>
    public int WebAttemptCount => webAttemptCount;

    /// <returns>x: ramped difficulty 0~1; y: chance 0~1 that one attempt
    /// creates a web</returns>
    public AnimationCurve WebSpawnProbabilityCurve =>
        webSpawnProbabilityCurve;

    /// <returns>falling cobra creation attempts rolled per segment</returns>
    public int FallingCobraAttemptCount => fallingCobraAttemptCount;

    /// <returns>x: ramped difficulty 0~1; y: chance 0~1 that one attempt
    /// creates a falling cobra</returns>
    public AnimationCurve FallingCobraSpawnProbabilityCurve =>
        fallingCobraSpawnProbabilityCurve;

    /// <returns>vertical fall speed of a falling cobra; u/s</returns>
    public float FallingCobraFallSpeedU => fallingCobraFallSpeedU;

    /// <returns>speed a falling cobra slides round the trunk toward the
    /// monkey; u/s</returns>
    public float FallingCobraHorizontalSpeedU => fallingCobraHorizontalSpeedU;

    /// <returns>while the monkey reaches for a banana, a dormant falling
    /// cobra wakes within this 3D distance instead of its own; 0 turns it
    /// off; u</returns>
    public float FallingCobraReachWakeDistanceU =>
        fallingCobraReachWakeDistanceU;

    /// <returns>chance 0~1 that a falling cobra hangs above a banana of its
    /// segment instead of at a random spot</returns>
    public float FallingCobraBananaAmbushChance =>
        fallingCobraBananaAmbushChance;

    /// <returns>grace after the hit stun before the same falling cobra can
    /// drop the monkey again, so the monkey has time to dodge sideways;
    /// s</returns>
    public float FallingCobraRehitGraceS => fallingCobraRehitGraceS;

    /// <returns>most drops one falling cobra can cause in a single fall
    /// </returns>
    public int FallingCobraMaxHits => fallingCobraMaxHits;

    /// <returns>x: ramped difficulty 0~1; y: the great cobra's climb speed
    /// outside a surge; u/s</returns>
    public AnimationCurve CobraBaseSpeedCurve => cobraBaseSpeedCurve;

    /// <returns>surge time each mistake (hit or web) adds to the great
    /// cobra; 0 turns surges off; s</returns>
    public float CobraSurgePerMistakeS => cobraSurgePerMistakeS;

    /// <returns>ceiling on the stacked surge time left; s</returns>
    public float CobraMaxSurgeS => cobraMaxSurgeS;

    /// <returns>extra climb speed per mistake stacked in the current surge;
    /// u/s</returns>
    public float CobraSurgeSpeedPerMistakeU => cobraSurgeSpeedPerMistakeU;

    /// <returns>ceiling on the great cobra's surging climb speed, may pass
    /// <see cref="UpSpeedU"/>; u/s</returns>
    public float CobraMaxSurgeSpeedU => cobraMaxSurgeSpeedU;

    /// <returns>how fast the great cobra's climb speed eases toward its
    /// target; u/s²</returns>
    public float CobraSpeedChangeRateU => cobraSpeedChangeRateU;

    /// <returns>extra climb speed the great cobra gets over its base speed
    /// for a while after the monkey leaves the starting zone; 0 turns it
    /// off; u/s</returns>
    public float CobraExitBoostSpeedU => cobraExitBoostSpeedU;

    /// <returns>how long the great cobra's starting zone exit boost lasts;
    /// s</returns>
    public float CobraExitBoostDurationS => cobraExitBoostDurationS;

    /// <returns>fraction 0~1 of <see cref="MaxStamina"/> the monkey starts
    /// the level with, spent in the starting zone</returns>
    public float StartingZoneStartStaminaFraction =>
        startingZoneStartStaminaFraction;

    /// <returns>like <see cref="ExhaustedSpeedMultiplier"/>, but used while
    /// the starting zone lasts</returns>
    public float StartingZoneExhaustedSpeedMultiplier =>
        startingZoneExhaustedSpeedMultiplier;

    // Inspector Fields  #######################################################
    [Header("Stamina")]
    [SerializeField]
    [Tooltip("stamina at level start, also the ceiling for restores")]
    private float maxStamina = 100f;

    [SerializeField]
    [Tooltip(
        "stamina lost per unit climbed upward, the costliest move; "
            + "descending is free"
    )]
    private float staminaUpwardDrainPerU = 0.5f;

    [SerializeField]
    [Tooltip(
        "stamina lost per unit travelled sideways round the trunk, "
            + "keep below the upward rate"
    )]
    private float staminaSidewayDrainPerU = 0.3f;

    [SerializeField]
    [Tooltip("stamina restored per grabbed fruit, capped at max stamina")]
    private float fruitStaminaRestore = 30f;

    [Header("Movement")]
    [SerializeField]
    [Tooltip("climb speed moving up; u/s")]
    private float upSpeedU = 6f;

    [SerializeField]
    [Tooltip("climb speed moving down; u/s")]
    private float downSpeedU = 12f;

    [SerializeField]
    [Tooltip("upward climb speed multiplier while stamina is empty")]
    private float exhaustedSpeedMultiplier = 0.2f;

    [SerializeField]
    [Tooltip("stamina regained per s while no movement key is pressed")]
    private float idleStaminaRecoveryPerS = 2f;

    [Header("Starting Zone")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "fraction of max stamina at level start, so the starting zone "
            + "teaches the stamina limit; 0.3 = 30%"
    )]
    private float startingZoneStartStaminaFraction = 0.3f;

    [SerializeField]
    [Tooltip(
        "upward climb speed multiplier while stamina is empty, used "
            + "during the starting zone only"
    )]
    private float startingZoneExhaustedSpeedMultiplier = 0.5f;

    [Header("Branch Hit")]
    [SerializeField]
    [Tooltip("control lost after a branch hit; s")]
    private float hitStunDurationS = 0.75f;

    [SerializeField]
    [Tooltip("distance fallen on a branch hit; u")]
    private float hitDropDistanceU = 10f;

    [SerializeField]
    [Tooltip("fall speed on a branch hit; u/s")]
    private float hitDropSpeedU = 8f;

    [Header("Score")]
    [SerializeField]
    [Min(0)]
    [Tooltip("points earned per meter of height")]
    private int pointsPerMeter = 1;

    [SerializeField]
    [Min(0.001f)]
    [Tooltip("Unity units per meter of height")]
    private float unitsPerMeter = 0.1f;

    [SerializeField]
    [Min(0)]
    [Tooltip("points awarded per score reward pickup")]
    private int scoreRewardPoints = 100;

    [Header("Ramped Difficulty")]
    [SerializeField]
    [Tooltip("x: player y above start y; world u; y: ramped difficulty 0~1")]
    private AnimationCurve rampedDifficultyCurve =
        AnimationCurve.Linear(20f, 0f, 100f, 1f);

    [Header("Branch")]
    [SerializeField]
    [Min(0)]
    [Tooltip("branch creation attempts rolled per segment")]
    private int branchGenerationAttemptCount = 9;

    [SerializeField]
    [Min(0f)]
    [Tooltip(
        "no branch is placed within this distance of the monkey while the "
            + "starting zone lasts; 0 turns it off; u"
    )]
    private float startingZoneBranchClearRadiusU = 6f;

    [SerializeField]
    [Tooltip(
        "x: ramped difficulty 0~1; y: chance 0~1 that one attempt "
            + "creates a branch"
    )]
    private AnimationCurve branchSpawnProbabilityCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField]
    [Tooltip(
        "x: ramped difficulty 0~1; y: chance 0~1 that a generated branch "
            + "bears a banana"
    )]
    private AnimationCurve bananaSpawnProbabilityCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Spider Web")]
    [SerializeField]
    [Min(0)]
    [Tooltip("web creation attempts rolled per segment")]
    private int webAttemptCount = 2;

    [SerializeField]
    [Tooltip(
        "x: ramped difficulty 0~1; y: chance 0~1 that one attempt "
            + "creates a web"
    )]
    private AnimationCurve webSpawnProbabilityCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 0.5f);

    [Header("Falling Cobra")]
    [SerializeField]
    [Min(0)]
    [Tooltip("falling cobra creation attempts rolled per segment")]
    private int fallingCobraAttemptCount = 2;

    [SerializeField]
    [Tooltip(
        "x: ramped difficulty 0~1; y: chance 0~1 that one attempt "
            + "creates a falling cobra"
    )]
    private AnimationCurve fallingCobraSpawnProbabilityCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 0.5f);

    [SerializeField]
    [Tooltip("vertical fall speed of a falling cobra; u/s")]
    private float fallingCobraFallSpeedU = 6f;

    [SerializeField]
    [Tooltip(
        "speed a falling cobra slides round the trunk toward the monkey; u/s"
    )]
    private float fallingCobraHorizontalSpeedU = 2f;

    [SerializeField]
    [Tooltip(
        "while the monkey reaches for a banana, a dormant cobra wakes "
            + "within this 3D distance instead of its own; 0 = off; u"
    )]
    private float fallingCobraReachWakeDistanceU = 20f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "chance a falling cobra hangs above a banana of its segment "
            + "instead of at a random spot"
    )]
    private float fallingCobraBananaAmbushChance = 0.5f;

    [SerializeField]
    [Tooltip(
        "grace after the hit stun before the same falling cobra can drop "
            + "the monkey again, time to dodge sideways; s"
    )]
    private float fallingCobraRehitGraceS = 1f;

    [SerializeField]
    [Tooltip("most drops one falling cobra can cause in a single fall")]
    private int fallingCobraMaxHits = 2;

    [Header("Great Cobra")]
    [SerializeField]
    [Tooltip(
        "x: ramped difficulty 0~1; y: climb speed outside a surge, keep "
            + "below the monkey's up speed; u/s"
    )]
    private AnimationCurve cobraBaseSpeedCurve =
        AnimationCurve.Linear(0f, 3.4f, 1f, 4.0f);

    [SerializeField]
    [Tooltip(
        "surge time each mistake (hit or web) adds; mistakes stack while "
            + "a surge runs; 0 = off; s"
    )]
    private float cobraSurgePerMistakeS = 4f;

    [SerializeField]
    [Tooltip("ceiling on the stacked surge time left; s")]
    private float cobraMaxSurgeS = 10f;

    [SerializeField]
    [Tooltip("extra climb speed per mistake stacked in the surge; u/s")]
    private float cobraSurgeSpeedPerMistakeU = 2.5f;

    [SerializeField]
    [Tooltip(
        "ceiling on the surging climb speed; may pass the monkey's up "
            + "speed, so stacked mistakes can be fatal; u/s"
    )]
    private float cobraMaxSurgeSpeedU = 9f;

    [SerializeField]
    [Tooltip("how fast the climb speed eases toward its target; u/s²")]
    private float cobraSpeedChangeRateU = 6f;

    [SerializeField]
    [Min(0f)]
    [Tooltip(
        "extra climb speed over the base speed for a while after the monkey "
            + "leaves the starting zone; 0 turns it off; u/s"
    )]
    private float cobraExitBoostSpeedU = 3f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("how long the starting zone exit boost lasts; s")]
    private float cobraExitBoostDurationS = 5f;

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaUpwardDrainPerU = Mathf.Max(0f, staminaUpwardDrainPerU);
        staminaSidewayDrainPerU = Mathf.Max(0f, staminaSidewayDrainPerU);
        fruitStaminaRestore = Mathf.Max(0f, fruitStaminaRestore);
        upSpeedU = Mathf.Max(0f, upSpeedU);
        downSpeedU = Mathf.Max(0f, downSpeedU);
        exhaustedSpeedMultiplier = Mathf.Clamp01(exhaustedSpeedMultiplier);
        idleStaminaRecoveryPerS = Mathf.Max(0f, idleStaminaRecoveryPerS);
        hitStunDurationS = Mathf.Max(0f, hitStunDurationS);
        hitDropDistanceU = Mathf.Max(0f, hitDropDistanceU);
        hitDropSpeedU = Mathf.Max(0f, hitDropSpeedU);
        pointsPerMeter = Mathf.Max(0, pointsPerMeter);
        unitsPerMeter = Mathf.Max(0.001f, unitsPerMeter);
        scoreRewardPoints = Mathf.Max(0, scoreRewardPoints);
        branchGenerationAttemptCount = Mathf.Max(
            0,
            branchGenerationAttemptCount
        );
        webAttemptCount = Mathf.Max(0, webAttemptCount);
        fallingCobraAttemptCount = Mathf.Max(0, fallingCobraAttemptCount);
        fallingCobraFallSpeedU = Mathf.Max(0f, fallingCobraFallSpeedU);
        fallingCobraHorizontalSpeedU = Mathf.Max(
            0f,
            fallingCobraHorizontalSpeedU
        );
        fallingCobraReachWakeDistanceU = Mathf.Max(
            0f,
            fallingCobraReachWakeDistanceU
        );
        fallingCobraBananaAmbushChance = Mathf.Clamp01(
            fallingCobraBananaAmbushChance
        );
        fallingCobraRehitGraceS = Mathf.Max(0f, fallingCobraRehitGraceS);
        fallingCobraMaxHits = Mathf.Max(1, fallingCobraMaxHits);
        cobraSurgePerMistakeS = Mathf.Max(0f, cobraSurgePerMistakeS);
        cobraMaxSurgeS = Mathf.Max(cobraSurgePerMistakeS, cobraMaxSurgeS);
        cobraSurgeSpeedPerMistakeU = Mathf.Max(0f, cobraSurgeSpeedPerMistakeU);
        cobraMaxSurgeSpeedU = Mathf.Max(0f, cobraMaxSurgeSpeedU);
        cobraSpeedChangeRateU = Mathf.Max(0.01f, cobraSpeedChangeRateU);
        cobraExitBoostSpeedU = Mathf.Max(0f, cobraExitBoostSpeedU);
        cobraExitBoostDurationS = Mathf.Max(0f, cobraExitBoostDurationS);
    }
}
