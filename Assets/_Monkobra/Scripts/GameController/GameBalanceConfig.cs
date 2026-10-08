using UnityEngine;

/// <summary>
/// Single home for the game balance knobs: stamina budget, climb speeds,
/// branch-hit penalty, score rules and branch generation.
/// </summary>
[CreateAssetMenu(
    fileName = "GameBalanceConfig",
    menuName = "Scriptable Objects/GameBalanceConfig"
)]
public class GameBalanceConfig: ScriptableObject {
    // Public Members  #########################################################
    /// <returns>stamina at level start, also the ceiling for restores</returns>
    public float MaxStamina => maxStamina;

    /// <returns>stamina lost per drain tick</returns>
    public float StaminaDecreaseAmount => staminaDecreaseAmount;

    /// <returns>time between drain ticks; s</returns>
    public float StaminaDecreaseIntervalS => staminaDecreaseIntervalS;

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

    /// <returns>branch creation attempts rolled per segment</returns>
    public int BranchGenerationAttemptCount => branchGenerationAttemptCount;

    /// <returns>x: ramped difficulty 0~1; y: chance 0~1 that one attempt
    /// creates a branch</returns>
    public AnimationCurve BranchSpawnProbabilityCurve =>
        branchSpawnProbabilityCurve;

    /// <returns>x: ramped difficulty 0~1; y: chance 0~1 that a generated
    /// branch bears a banana</returns>
    public AnimationCurve BananaSpawnProbabilityCurve =>
        bananaSpawnProbabilityCurve;

    // Inspector Fields  #######################################################
    [Header("Stamina")]
    [SerializeField]
    [Tooltip("stamina at level start, also the ceiling for restores")]
    private float maxStamina = 100f;

    [SerializeField]
    [Tooltip("stamina lost per drain tick")]
    private float staminaDecreaseAmount = 10f;

    [SerializeField]
    [Tooltip("time between drain ticks; s")]
    private float staminaDecreaseIntervalS = 10f;

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

    [Header("Branch")]
    [SerializeField]
    [Min(0)]
    [Tooltip("branch creation attempts rolled per segment")]
    private int branchGenerationAttemptCount = 9;

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

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaDecreaseAmount = Mathf.Max(0f, staminaDecreaseAmount);
        staminaDecreaseIntervalS = Mathf.Max(0.01f, staminaDecreaseIntervalS);
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
    }
}
