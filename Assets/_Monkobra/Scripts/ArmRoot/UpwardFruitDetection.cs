using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which side of the character has fruit within reach, fed by 2
/// <see cref="DetectionZone"/> children that forward their trigger events.
/// Keeps every "FruitCollider" overlap per side, so ReachForLeft and
/// ReachForRight hold with several fruits at once.
/// </summary>
public class UpwardFruitDetection: MonoBehaviour {
    // Public Members  #########################################################
    public static UpwardFruitDetection I {
        get; private set;
    }

    public bool ReachForLeft => leftFruits.Count > 0;
    public bool ReachForRight => rightFruits.Count > 0;

    /// <summary>side to reach for: right iff only right has fruit</summary>
    public DetectionSide ReachSide =>
        ReachForRight && !ReachForLeft
            ? DetectionSide.Right
            : DetectionSide.Left;

    // Public Methods  #########################################################
    /// <returns>newest fruit in reach on <paramref name="side"/>, else null
    /// </returns>
    public Collider GetNewestFruit(DetectionSide side) {
        HashSet<Collider> fruits =
            side == DetectionSide.Left ? leftFruits : rightFruits;
        for (int i = fruitOrder.Count - 1; i >= 0; i--) {
            if (fruits.Contains(fruitOrder[i])) {
                return fruitOrder[i];
            }
        }
        return null;
    }

    /// <summary>
    /// forgets <paramref name="fruit"/> on both sides, for a fruit switched
    /// off inside a zone, whose trigger exit may never fire
    /// </summary>
    public void RemoveFruit(Collider fruit) {
        bool hadLeft = leftFruits.Remove(fruit);
        bool hadRight = rightFruits.Remove(fruit);
        fruitOrder.Remove(fruit);
        if (hadLeft && leftFruits.Count == 0) {
            LogReachChanged(DetectionSide.Left);
        }
        if (hadRight && rightFruits.Count == 0) {
            LogReachChanged(DetectionSide.Right);
        }
    }

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

        // drop only this duplicate component, the GameObject may hold more
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
        if (fruits.Add(other)) {
            fruitOrder.Add(other);
            if (fruits.Count == 1) {
                LogReachChanged(side);
            }
        }
    }

    private void OnZoneExited(DetectionSide side, Collider other) {
        HashSet<Collider> fruits =
            side == DetectionSide.Left ? leftFruits : rightFruits;
        if (fruits.Remove(other)) {
            fruitOrder.Remove(other);
            if (fruits.Count == 0) {
                LogReachChanged(side);
            }
        }
    }

    // Constants  ###############################################################
    private const string FRUIT_TAG = "FruitCollider";

    // Private Members  ########################################################
    private readonly HashSet<Collider> leftFruits = new HashSet<Collider>();
    private readonly HashSet<Collider> rightFruits = new HashSet<Collider>();

    // entry order across both sides, last is newest
    private readonly List<Collider> fruitOrder = new List<Collider>();

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
