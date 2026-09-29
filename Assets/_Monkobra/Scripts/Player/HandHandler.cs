using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to a hand's own trigger Collider, a child of the paired arm's
/// <see cref="MonkeyArm"/> GameObject. Tracks every overlapping collider
/// tagged Branch as a reachable target, newest overlap last. A single
/// click action toggles the paired arm's reach: not reaching, a click aims
/// it at the newest reachable Branch; already reaching, a click cancels
/// back to normal hand-over-hand climbing.
/// </summary>
public class HandHandler: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Tooltip("this hand's arm, toggled into/out of reach on click")]
    [SerializeField]
    private MonkeyArm arm;

    [Tooltip("click action, each press toggles reach on/off")]
    [SerializeField]
    private InputActionReference clickAction;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (arm == null) {
            Debug.LogWarning(
                "HandHandler:\tmust assign Inspector Field: arm", this
            );
        }
        if (clickAction == null) {
            Debug.LogWarning(
                "HandHandler:\tmust assign Inspector Field: clickAction",
                this
            );
        }
    }

    private void OnEnable() {
        if (clickAction != null) {
            clickAction.action.Enable();
            clickAction.action.performed += OnClickPerformed;
        }
    }

    private void OnDisable() {
        if (clickAction != null) {
            clickAction.action.performed -= OnClickPerformed;
            clickAction.action.Disable();
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(REACHABLE_TAG)) {
            return;
        }
        if (reachables.Add(other)) {
            reachableOrder.Add(other);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (reachables.Remove(other)) {
            reachableOrder.Remove(other);
        }
    }

    // Event Handlers  #########################################################
    // 1 click toggles: not reaching -> aim at the newest Branch in reach;
    // already reaching, w/ or w/o a Branch still overlapping -> cancel
    private void OnClickPerformed(InputAction.CallbackContext context) {
        if (arm == null) {
            return;
        }

        if (arm.IsReaching) {
            arm.CancelReach();
            if (Debug.isDebugBuild) {
                Debug.Log("HandHandler:\treach cancelled", this);
            }
            return;
        }

        Transform target = NewestReachable;
        if (target == null) {
            if (Debug.isDebugBuild) {
                Debug.Log(
                    "HandHandler:\tclick ignored, nothing in reach", this
                );
            }
            return;
        }

        arm.SetReachTarget(target);
        if (Debug.isDebugBuild) {
            Debug.Log($"HandHandler:\treaching for {target.name}", this);
        }
    }

    // Constants  ###############################################################
    private const string REACHABLE_TAG = "Branch";

    // Private Members  ########################################################
    private readonly HashSet<Collider> reachables = new HashSet<Collider>();

    // entry order across overlaps; last elem is newest, drives NewestReachable
    private readonly List<Collider> reachableOrder = new List<Collider>();

    private Transform NewestReachable =>
        reachableOrder.Count > 0
            ? reachableOrder[reachableOrder.Count - 1].transform
            : null;
}
