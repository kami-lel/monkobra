using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which side (left/right) of the character has fruit within reach,
/// driven by two <see cref="DetectionZone"/> children (their trigger
/// Colliders live on separate child GameObjects, so OnTriggerEnter/Exit
/// must be handled there and forwarded via events, not on this script).
/// Tracks overlapping colliders tagged "FruitCollider" per side so
/// ReachForLeft/ReachForRight stay accurate even when multiple fruits
/// overlap a side at once.
/// </summary>
public class UpwardFruitDetection: MonoBehaviour {
    // Public Members  #########################################################
    public static UpwardFruitDetection I {
        get; private set;
    }

    public bool ReachForLeft => leftFruits.Count > 0;
    public bool ReachForRight => rightFruits.Count > 0;

    // Inspector Fields  #######################################################
    [Tooltip("left-side detection zone, must report Side == Left")]
    [SerializeField]
    private DetectionZone leftZone;

    [Tooltip("right-side detection zone, must report Side == Right")]
    [SerializeField]
    private DetectionZone rightZone;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (leftZone == null || rightZone == null) {
            Debug.LogWarning(
                "UpwardFruitDetection:\tmust assign Inspector Fields: "
                    + "leftZone, rightZone",
                this
            );
        }

        // drop this duplicate component only, its GameObject may hold more
        if (I != null && I != this) {
            Debug.LogWarning(
                "UpwardFruitDetection:\tduplicate instance, removing",
                this
            );
            Destroy(this);
            return;
        }
        I = this;
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }

    private void OnEnable() {
        if (leftZone != null) {
            leftZone.Entered += OnZoneEntered;
            leftZone.Exited += OnZoneExited;
        }

        if (rightZone != null) {
            rightZone.Entered += OnZoneEntered;
            rightZone.Exited += OnZoneExited;
        }
    }

    private void OnDisable() {
        if (leftZone != null) {
            leftZone.Entered -= OnZoneEntered;
            leftZone.Exited -= OnZoneExited;
        }

        if (rightZone != null) {
            rightZone.Entered -= OnZoneEntered;
            rightZone.Exited -= OnZoneExited;
        }
    }

    // Event Handlers  #########################################################
    private void OnZoneEntered(DetectionSide side, Collider other) {
        if (!other.CompareTag(FRUIT_TAG)) {
            return;
        }

        HashSet<Collider> fruits =
            side == DetectionSide.Left ? leftFruits : rightFruits;
        if (fruits.Add(other) && fruits.Count == 1) {
            LogReachChanged(side);
        }
    }

    private void OnZoneExited(DetectionSide side, Collider other) {
        HashSet<Collider> fruits =
            side == DetectionSide.Left ? leftFruits : rightFruits;
        if (fruits.Remove(other) && fruits.Count == 0) {
            LogReachChanged(side);
        }
    }

    // Constants  ###############################################################
    private const string FRUIT_TAG = "FruitCollider";

    // Private Members  ########################################################
    private readonly HashSet<Collider> leftFruits = new HashSet<Collider>();
    private readonly HashSet<Collider> rightFruits = new HashSet<Collider>();

    // Private Methods  ########################################################
    private void LogReachChanged(DetectionSide side) {
        bool reach = side == DetectionSide.Left ? ReachForLeft : ReachForRight;
        if (Debug.isDebugBuild) {
            Debug.Log(
                $"UpwardFruitDetection:\treachFor{side} changed: {reach}",
                this
            );
        }
    }
}
