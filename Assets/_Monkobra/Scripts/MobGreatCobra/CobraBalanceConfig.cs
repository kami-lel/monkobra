using UnityEngine;

/// <summary>
/// Balance of both cobras: the falling cobra's spawn chance, speeds, wake
/// distance and hit limits, and the great cobra's chase speed, surges and
/// starting zone exit boost. Consumed by the falling cobra spawner and the
/// great cobra's motor.
/// </summary>
[CreateAssetMenu(
    fileName = "CobraBalanceConfig",
    menuName = "Scriptable Objects/CobraBalanceConfig"
)]
public class CobraBalanceConfig: ScriptableObject {
    // Public Members  #########################################################
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

    /// <returns>3D range in which an alerted falling cobra may launch; u</returns>
    public float FallingCobraAttackDistanceU => fallingCobraAttackDistanceU;

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

    /// <returns>x: ramped difficulty 0~1; y: multiplier 0~2 of the monkey's
    /// top climb speed, 1 = that speed; the great cobra's base climb speed
    /// outside a surge</returns>
    public AnimationCurve CobraBaseSpeedCurve => cobraBaseSpeedCurve;

    /// <returns>x: gap the great cobra's coil top trails below the monkey,
    /// U; y: multiplier on its base climb speed, mux the base speed
    /// curve's</returns>
    public AnimationCurve CobraDistanceSpeedCurve => cobraDistanceSpeedCurve;

    /// <returns>surge time each mistake (hit or web) adds to the great
    /// cobra; 0 turns surges off; s</returns>
    public float CobraSurgePerMistakeS => cobraSurgePerMistakeS;

    /// <returns>ceiling on the stacked surge time left; s</returns>
    public float CobraMaxSurgeS => cobraMaxSurgeS;

    /// <returns>extra climb speed per mistake stacked in the current surge;
    /// u/s</returns>
    public float CobraSurgeSpeedPerMistakeU => cobraSurgeSpeedPerMistakeU;

    /// <returns>x: stack number 1, 2, 3, ~ of a surge; y: share 0~1 of
    /// the per-mistake surge speed that stack adds; holds its last value
    /// past its last key</returns>
    public AnimationCurve CobraSurgeStackShareCurve =>
        cobraSurgeStackShareCurve;

    /// <param name="stacks">mistakes stacked in the running surge</param>
    /// <returns>extra climb speed the surge adds over the base speed, each
    /// stack scaled by its share on the stack share curve; u/s</returns>
    public float GetSurgeSpeedBonusU(int stacks) {
        float bonusU = 0f;
        for (int i = 1; i <= stacks; i++) {
            bonusU += cobraSurgeSpeedPerMistakeU
                * Mathf.Max(0f, cobraSurgeStackShareCurve.Evaluate(i));
        }
        return bonusU;
    }

    /// <returns>ceiling on the great cobra's surging climb speed, may pass
    /// the monkey's top climb speed; u/s</returns>
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

    // Inspector Fields  #######################################################
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

    [SerializeField, Min(.1f)]
    [Tooltip("An alerted cobra stays perched until the monkey enters this 3D range; u")]
    private float fallingCobraAttackDistanceU = 6f;

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
        "x: ramped difficulty 0~1; y: multiplier 0~2 of the monkey's top "
            + "climb speed, 1 = that speed; mux the distance curve; surge & "
            + "exit boost add on top"
    )]
    private AnimationCurve cobraBaseSpeedCurve =
        AnimationCurve.Linear(0f, 0.567f, 1f, 0.667f);

    [SerializeField]
    [Tooltip(
        "x: gap the coil top trails below the monkey, U; y: multiplier on "
            + "the base speed curve's, mux the monkey's top climb speed; "
            + "surge & exit boost add on top"
    )]
    private AnimationCurve cobraDistanceSpeedCurve =
        AnimationCurve.Linear(0f, 0.5f, 100f, 1.2f);

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
    [Tooltip(
        "extra climb speed of the 1st mistake in a surge, scaled per stack "
            + "by the stack share curve; u/s"
    )]
    private float cobraSurgeSpeedPerMistakeU = 2.5f;

    [SerializeField]
    [Tooltip(
        "x: stack number 1, 2, 3, ~ of a surge; y: share 0~1 of the "
            + "per-mistake surge speed that stack adds, so later mistakes "
            + "can add less; holds its last value"
    )]
    private AnimationCurve cobraSurgeStackShareCurve = new AnimationCurve(
        new Keyframe(1f, 1f),
        new Keyframe(2f, 0.5f),
        new Keyframe(3f, 0.25f),
        new Keyframe(4f, 0.125f),
        new Keyframe(5f, 0.0625f)
    );

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
        fallingCobraAttackDistanceU = Mathf.Max(.1f, fallingCobraAttackDistanceU);
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
