using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera shake for monkey hits, kept apart from hit detection. Sits on the
/// main camera, next to the <c>CinemachineImpulseListener</c>. Listens to
/// <see cref="HitBranchSensor.Hit"/> and fires a light impulse for a
/// branch, a heavier one for a falling cobra. Each kind has its own
/// <see cref="CinemachineImpulseSource"/> on the same camera, so amplitude
/// and duration are tuned per kind in the Inspector.
/// </summary>
public class CameraHitShakeEfx: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip("impulse for a branch hit, the lighter shake")]
    [SerializeField]
    private CinemachineImpulseSource lightSource;

    [Tooltip("impulse for a falling cobra hit, the violent shake")]
    [SerializeField]
    private CinemachineImpulseSource heavySource;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (lightSource == null) {
            Debug.LogWarning(
                "CameraHitShakeEfx:\tmust assign Inspector Field: "
                    + "lightSource",
                this
            );
        }
        if (heavySource == null) {
            Debug.LogWarning(
                "CameraHitShakeEfx:\tmust assign Inspector Field: "
                    + "heavySource",
                this
            );
        }
    }

    // the sensor lives in the monkey, so it is read after every Awake ran
    private void Start() {
        sensor = HitBranchSensor.I;
        if (sensor == null) {
            Debug.LogError(
                "CameraHitShakeEfx:\tfail to get singleton: "
                    + "HitBranchSensor",
                this
            );
            enabled = false;
            return;
        }

        sensor.Hit += OnHit;
    }

    private void OnDisable() {
        Unsubscribe();
    }

    private void OnDestroy() {
        Unsubscribe();
    }

    // Event Handlers  #########################################################
    // a missing heavy source falls back to the light one, so a hit never
    // goes without a shake
    private void OnHit(Collider _, HitBranchSensor.HitKind kind) {
        CinemachineImpulseSource source =
            kind == HitBranchSensor.HitKind.FallingCobraController
                ? (heavySource != null ? heavySource : lightSource)
                : lightSource;
        if (source == null) {
            return;
        }

        source.GenerateImpulse();

        if (Debug.isDebugBuild) {
            Debug.Log($"CameraHitShakeEfx:\tshake {kind}", this);
        }
    }

    // private members  ########################################################
    private HitBranchSensor sensor;

    // private methods  ########################################################
    private void Unsubscribe() {
        if (sensor != null) {
            sensor.Hit -= OnHit;
            sensor = null;
        }
    }
}
