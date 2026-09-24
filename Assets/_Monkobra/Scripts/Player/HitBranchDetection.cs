using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Attach to a detection trigger collider; fires a camera shake whenever it
/// touches a collider tagged <c>Branch</c>. Uses the
/// <c>CinemachineImpulseSource</c> on the same object.
/// </summary>
[RequireComponent(typeof(CinemachineImpulseSource))]
public class HitBranchDetection: MonoBehaviour {
    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // Event Handlers  #########################################################
    private void OnTriggerEnter(Collider other) {
        Debug.Log($"touched {other.name}"); // HACK rm
        if (!other.CompareTag(BRANCH_TAG)) {
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log($"HitBranchDetection:\thit branch {other.name}");
        }
        impulseSource.GenerateImpulse();
    }

    // constants  ##############################################################
    private const string BRANCH_TAG = "Branch";

    // cached references  ------------------------------------------------------
    private CinemachineImpulseSource impulseSource;
}
