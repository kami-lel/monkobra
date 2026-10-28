using System;
using UnityEngine;

/// <summary>
/// Shared tuning for the camera efx on the main camera. Today that is the
/// stamina cool-down of <see cref="CameraCoolDownFeedback"/>: a vignette blackdown
/// plus a zoom-out, in two steps, one for low stamina and a stronger one for
/// an empty bar. The hit shake is tuned on the two
/// <c>CinemachineImpulseSource</c> components that
/// <see cref="CameraHitShakeFeedback"/> fires instead, since Cinemachine owns
/// those values.
/// </summary>
[CreateAssetMenu(
    fileName = "CameraEfxConfig",
    menuName = "Scriptable Objects/CameraEfxConfig"
)]
public class CameraEfxConfig: ScriptableObject {
    // Public Members  #########################################################
    /// <summary>
    /// how strong one cool-down step is at its peak
    /// </summary>
    [Serializable]
    public class CoolDownLevel {
        /// <returns>vignette intensity at the peak; 0 none, 1 full black edge
        /// </returns>
        public float VignetteIntensity => vignetteIntensity;

        /// <returns>extra field of view at the peak; deg</returns>
        public float FovDeltaDeg => fovDeltaDeg;

        public CoolDownLevel(float vignetteIntensity, float fovDeltaDeg) {
            this.vignetteIntensity = vignetteIntensity;
            this.fovDeltaDeg = fovDeltaDeg;
        }

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("vignette intensity at the peak; 0 none, 1 full black edge")]
        private float vignetteIntensity;

        [SerializeField]
        [Tooltip("extra field of view at the peak, the zoom-out; deg")]
        private float fovDeltaDeg;
    }

    /// <returns>step for a move attempt below the low-stamina fraction, the
    /// bar not yet empty</returns>
    public CoolDownLevel LowLevel => lowLevel;

    /// <returns>step for a move attempt on an empty bar</returns>
    public CoolDownLevel EmptyLevel => emptyLevel;

    /// <returns>ease from rest to peak; s</returns>
    public float EaseInS => easeInS;

    /// <returns>ease from peak back to rest; s</returns>
    public float EaseOutS => easeOutS;

    /// <returns>wait after a pulse ends before the next may start; s</returns>
    public float RetriggerCooldownS => retriggerCooldownS;

    // Inspector Fields  #######################################################
    [Header("Cool-Down Steps")]
    [SerializeField]
    [Tooltip("move attempt below the low-stamina fraction, bar not empty")]
    private CoolDownLevel lowLevel = new CoolDownLevel(0.4f, 6f);

    [SerializeField]
    [Tooltip("move attempt on an empty bar, the stronger step")]
    private CoolDownLevel emptyLevel = new CoolDownLevel(0.7f, 12f);

    [Header("Cool-Down Timing")]
    [SerializeField]
    [Tooltip("ease from rest to peak; s")]
    private float easeInS = 0.1f;

    [SerializeField]
    [Tooltip("ease from peak back to rest; s")]
    private float easeOutS = 1.2f;

    [SerializeField]
    [Tooltip("wait after a pulse ends before the next may start; s")]
    private float retriggerCooldownS = 0.5f;

    // Editor Validation  ######################################################
    private void OnValidate() {
        easeInS = Mathf.Max(0f, easeInS);
        easeOutS = Mathf.Max(0f, easeOutS);
        retriggerCooldownS = Mathf.Max(0f, retriggerCooldownS);
    }
}
