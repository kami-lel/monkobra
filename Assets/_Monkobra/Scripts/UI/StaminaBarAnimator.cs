using UnityEngine;

/// <summary>
/// drives the stamina bar's feedback motion: a scale punch each time a fruit
/// is consumed, a position shake when the player tries to move on an empty
/// bar. Reacts to <see cref="StaminaBarController"/> events, holds no stamina
/// state itself
/// </summary>
[RequireComponent(typeof(StaminaBarController))]
[RequireComponent(typeof(RectTransform))]
public class StaminaBarAnimator: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Punch")]
    [SerializeField]
    [Tooltip("peak extra scale; 0.3 = grow to 130%, then back")]
    private float punchStrength = 0.3f;

    [SerializeField]
    [Tooltip("punch grow-and-shrink length; s")]
    private float punchDurationS = 0.25f;

    [Header("Shake")]
    [SerializeField]
    [Tooltip("peak offset per axis; px")]
    private Vector2 shakeMagnitudePx = new Vector2(8f, 4f);

    [SerializeField]
    [Tooltip("shake length, offset fades out over it; s")]
    private float shakeDurationS = 0.3f;

    [SerializeField]
    [Tooltip("wait after a shake ends before the next may start; s")]
    private float shakeCooldownS = 0.2f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        controller = GetComponent<StaminaBarController>();
        rect = (RectTransform)transform;
        restScale = rect.localScale;
        restPosition = rect.anchoredPosition;
    }

    private void OnEnable() {
        controller.FruitConsumed += OnFruitConsumed;
        controller.ExhaustedMoveAttempted += OnExhaustedMoveAttempted;
    }

    private void OnDisable() {
        controller.FruitConsumed -= OnFruitConsumed;
        controller.ExhaustedMoveAttempted -= OnExhaustedMoveAttempted;
        ResetPose();
    }

    private void Update() {
        UpdatePunch();
        UpdateShake();
    }

    // Event Handlers  #########################################################
    // restarts a running punch, so rapid pickups each read as a hit
    private void OnFruitConsumed() {
        punchElapsedS = 0f;
        isPunching = true;

        if (Debug.isDebugBuild) {
            Debug.Log("StaminaBarAnimator:\tpunch", this);
        }
    }

    // ignored while a shake runs or cools down, a held key calls every step
    private void OnExhaustedMoveAttempted() {
        if (isShaking || Time.time < nextShakeTimeS) {
            return;
        }

        shakeElapsedS = 0f;
        isShaking = true;

        if (Debug.isDebugBuild) {
            Debug.Log("StaminaBarAnimator:\tshake", this);
        }
    }

    // private members  ########################################################
    private Vector3 restScale;
    private Vector2 restPosition;
    private bool isPunching;
    private float punchElapsedS;
    private bool isShaking;
    private float shakeElapsedS;
    private float nextShakeTimeS;

    // cached references  ------------------------------------------------------
    private StaminaBarController controller;
    private RectTransform rect;

    // private methods  ########################################################
    private void UpdatePunch() {
        if (!isPunching) {
            return;
        }

        punchElapsedS += Time.deltaTime;
        float t = punchDurationS > 0f
            ? Mathf.Clamp01(punchElapsedS / punchDurationS)
            : 1f;
        // sine half-wave: 0 → peak → 0, ends exactly at rest scale
        rect.localScale = restScale
            * (1f + punchStrength * Mathf.Sin(t * Mathf.PI));

        if (t >= 1f) {
            isPunching = false;
        }
    }

    private void UpdateShake() {
        if (!isShaking) {
            return;
        }

        shakeElapsedS += Time.deltaTime;
        float t = shakeDurationS > 0f
            ? Mathf.Clamp01(shakeElapsedS / shakeDurationS)
            : 1f;
        float fade = 1f - t;
        rect.anchoredPosition = restPosition + new Vector2(
            Random.Range(-1f, 1f) * shakeMagnitudePx.x * fade,
            Random.Range(-1f, 1f) * shakeMagnitudePx.y * fade
        );

        if (t >= 1f) {
            isShaking = false;
            nextShakeTimeS = Time.time + shakeCooldownS;
        }
    }

    private void ResetPose() {
        isPunching = false;
        isShaking = false;
        rect.localScale = restScale;
        rect.anchoredPosition = restPosition;
    }
}
