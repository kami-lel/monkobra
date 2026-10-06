using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Attach to a detection trigger collider; fires a camera shake whenever it
/// touches a collider tagged <c>Branch</c>. Uses the
/// <c>CinemachineImpulseSource</c> on the same object.
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class HitBranchDetection: MonoBehaviour {
    // Public Members  #########################################################
    /// <summary>
    /// Raised with the branch collider whenever this trigger touches one.
    /// </summary>
    public event Action<Collider> BranchHit;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // Event Handlers  #########################################################
    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(BRANCH_TAG)) {
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log($"HitBranchDetection:\thit branch {other.name}");
        }
        impulseSource.GenerateImpulse();
        BranchHit?.Invoke(other);
    }

    // constants  ##############################################################
    private const string BRANCH_TAG = "Branch";

    // cached references  ------------------------------------------------------
    private CinemachineImpulseSource impulseSource;
}
