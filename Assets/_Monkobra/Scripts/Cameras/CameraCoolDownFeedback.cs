using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cool-down feedback on the whole view while stamina is low: a
/// circular blackdown from the URP Vignette plus a zoom-out from widening
/// the live <see cref="CinemachineCamera"/>'s field of view, both easing in
/// and back out. Two steps: a lighter one while stamina is low, a stronger
/// one once the bar is empty, both tuned in <see cref="CameraEfxConfig"/>.
/// Sits on the main camera, next to the <c>CinemachineBrain</c>. Pulses on
/// every <see cref="StaminaBarController.LowStaminaMoveAttempted"/>, and both
/// values return to rest when the pulse ends or this component is disabled.
/// </summary>
public class CameraCoolDownFeedback: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip("tuning for both cool-down steps and their timing")]
    [SerializeField]
    private CameraEfxConfig config;

    [Tooltip("global volume whose profile holds a Vignette override")]
    [SerializeField]
    private Volume volume;

    // Public Methods  #########################################################
    /// <summary>
    /// starts a pulse, or restarts a running one from rest weight
    /// </summary>
    /// <param name="isEmpty">whether to play the empty-bar step, the low
    /// step otherwise</param>
    public void Pulse(bool isEmpty) {
        if (vignette == null || config == null) {
            return;
        }

        if (!isPulsing) {
            baseIntensity = vignette.intensity.value;
            baseIsOverridden = vignette.intensity.overrideState;
            fovCamera = brain != null
                ? brain.ActiveVirtualCamera as CinemachineCamera
                : null;
            if (fovCamera != null) {
                baseFovDeg = fovCamera.Lens.FieldOfView;
            }
        }
        level = isEmpty ? config.EmptyLevel : config.LowLevel;
        isEmptyPulse = isEmpty;
        elapsedS = 0f;
        isPulsing = true;
        vignette.intensity.overrideState = true;

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"CameraCoolDownFeedback:\tpulse {(isEmpty ? "empty" : "low")}",
                this
            );
        }
    }

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (config == null) {
            Debug.LogWarning(
                "CameraCoolDownFeedback:\tmust assign Inspector Field: config",
                this
            );
        }
        if (volume == null) {
            Debug.LogWarning(
                "CameraCoolDownFeedback:\tmust assign Inspector Field: volume",
                this
            );
        }

        if (config == null) {
            enabled = false;
            return;
        }

        brain = GetComponent<CinemachineBrain>();
        if (brain == null) {
            Debug.LogWarning(
                "CameraCoolDownFeedback:\tfail to get Component: CinemachineBrain",
                this
            );
        }

        if (volume == null) {
            return;
        }

        // profile (not sharedProfile): edits stay off the shared asset
        if (!volume.profile.TryGet(out vignette)) {
            Debug.LogWarning(
                "CameraCoolDownFeedback:\tvolume profile has no Vignette override",
                this
            );
        }
    }

    // the stamina bar lives in the HUD, so it is read after every Awake ran
    private void Start() {
        staminaBar = StaminaBarController.I;
        if (staminaBar == null) {
            Debug.LogError(
                "CameraCoolDownFeedback:\tfail to get singleton: "
                    + "StaminaBarController",
                this
            );
            enabled = false;
            return;
        }

        staminaBar.LowStaminaMoveAttempted += OnLowStaminaMoveAttempted;
    }

    private void OnDisable() {
        Unsubscribe();
        ResetPose();
    }

    private void OnDestroy() {
        Unsubscribe();
    }

    private void Update() {
        if (!isPulsing) {
            return;
        }

        elapsedS += Time.unscaledDeltaTime;
        float weight = CalcWeight(elapsedS);
        vignette.intensity.value = Mathf.Lerp(
            baseIntensity,
            level.VignetteIntensity,
            weight
        );
        if (fovCamera != null) {
            fovCamera.Lens.FieldOfView =
                baseFovDeg + level.FovDeltaDeg * weight;
        }

        if (elapsedS >= config.EaseInS + config.EaseOutS) {
            ResetPose();
            nextPulseTimeS = Time.unscaledTime + config.RetriggerCooldownS;
        }
    }

    // Event Handlers  #########################################################
    // ignored while a pulse runs or cools down, a held key calls every step;
    // a low pulse in flight steps up to the empty one once the bar empties,
    // rising on from its current weight instead of dropping back to rest
    private void OnLowStaminaMoveAttempted() {
        bool isEmpty = !staminaBar.HasStamina;
        if (isPulsing && isEmpty && !isEmptyPulse) {
            level = config.EmptyLevel;
            isEmptyPulse = true;
            elapsedS = Mathf.Min(elapsedS, config.EaseInS);
            return;
        }
        if (isPulsing || Time.unscaledTime < nextPulseTimeS) {
            return;
        }

        Pulse(isEmpty);
    }

    // private members  ########################################################
    private bool isPulsing;
    private bool isEmptyPulse;
    private CameraEfxConfig.CoolDownLevel level;
    private float elapsedS;
    private float nextPulseTimeS;
    private float baseIntensity;
    private bool baseIsOverridden;
    private float baseFovDeg;

    // cached references  ------------------------------------------------------
    private Vignette vignette;
    private CinemachineBrain brain;
    private CinemachineCamera fovCamera;
    private StaminaBarController staminaBar;

    // private methods  ########################################################
    // 0 at rest, 1 at the peak; smoothstep eases both slopes
    private float CalcWeight(float timeS) {
        float easeInS = config.EaseInS;
        float easeOutS = config.EaseOutS;
        float t = timeS < easeInS
            ? (easeInS > 0f ? timeS / easeInS : 1f)
            : 1f - (easeOutS > 0f ? (timeS - easeInS) / easeOutS : 1f);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
    }

    private void ResetPose() {
        if (isPulsing && vignette != null) {
            vignette.intensity.value = baseIntensity;
            vignette.intensity.overrideState = baseIsOverridden;
            if (fovCamera != null) {
                fovCamera.Lens.FieldOfView = baseFovDeg;
            }
        }
        isPulsing = false;
        fovCamera = null;
    }

    private void Unsubscribe() {
        if (staminaBar != null) {
            staminaBar.LowStaminaMoveAttempted -= OnLowStaminaMoveAttempted;
            staminaBar = null;
        }
    }
}
