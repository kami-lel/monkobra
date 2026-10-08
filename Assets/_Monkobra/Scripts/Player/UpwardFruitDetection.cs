using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which side of the character has fruit within reach, fed by 2
/// <see cref="DetectionZone"/> children that forward their trigger events.
/// Keeps every "FruitCollider" overlap per side, so ReachForLeft and
/// ReachForRight hold with several fruits at once. Also relays which fruit a
/// hand currently overlaps, so a release would grab it.
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

    /// <summary>raised w/ (fruit, isInReach) when a fruit enters or leaves
    /// the reach of both zones combined</summary>
    public event Action<Collider, bool> FruitReachChanged;

    /// <summary>fruit a hand overlaps now, so release grabs it, else null
    /// </summary>
    public Collider GrabbableFruit => grabbableFruit;

    /// <summary>raised w/ (previous, current) when the grabbable fruit
    /// changes</summary>
    public event Action<Collider, Collider> GrabbableFruitChanged;

    /// <summary>the single fruit a reach would aim at now, else null</summary>
    public Collider ReachTarget => reachTarget;

    /// <summary>raised w/ (previous, current) when the reach target changes
    /// </summary>
    public event Action<Collider, Collider> ReachTargetChanged;

    // Public Methods  #########################################################
    /// <summary>
    /// pins the reach target to <paramref name="fruit"/> while an arm reaches,
    /// null to release the pin
    /// </summary>
    public void SetLockedTarget(Collider fruit) {
        lockedTarget = fruit;
        RefreshReachTarget();
    }

    /// <summary>
    /// sets the fruit a hand overlaps, null for none; raises
    /// <see cref="GrabbableFruitChanged"/> iff it differs
    /// </summary>
    public void SetGrabbableFruit(Collider fruit) {
        if (fruit == grabbableFruit) {
            return;
        }

        Collider previous = grabbableFruit;
        grabbableFruit = fruit;
        if (Debug.isDebugBuild) {
            Debug.Log(
                "UpwardFruitDetection:\tgrabbable fruit changed: "
                    + (fruit != null ? fruit.name : "none"),
                this
            );
        }
        GrabbableFruitChanged?.Invoke(previous, fruit);
    }

    /// <returns>if <paramref name="fruit"/> sits in either zone</returns>
    public bool IsFruitInReach(Collider fruit) {
        return leftFruits.Contains(fruit) || rightFruits.Contains(fruit);
    }

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
        bool wasInReach = IsFruitInReach(fruit);
        bool hadLeft = leftFruits.Remove(fruit);
        bool hadRight = rightFruits.Remove(fruit);
        fruitOrder.Remove(fruit);
        if (wasInReach) {
            FruitReachChanged?.Invoke(fruit, false);
        }
        if (hadLeft && leftFruits.Count == 0) {
            LogReachChanged(DetectionSide.Left);
        }
        if (hadRight && rightFruits.Count == 0) {
            LogReachChanged(DetectionSide.Right);
        }
        if (fruit == grabbableFruit) {
            SetGrabbableFruit(null);
        }
        if (fruit == lockedTarget) {
            lockedTarget = null;
        }
        RefreshReachTarget();
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
        bool wasInReach = IsFruitInReach(other);
        if (fruits.Add(other)) {
            fruitOrder.Add(other);
            if (fruits.Count == 1) {
                LogReachChanged(side);
            }
            if (!wasInReach) {
                FruitReachChanged?.Invoke(other, true);
            }
            RefreshReachTarget();
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
            // the other zone may still hold it
            if (!IsFruitInReach(other)) {
                FruitReachChanged?.Invoke(other, false);
            }
            RefreshReachTarget();
        }
    }

    // Constants  ###############################################################
    private const string FRUIT_TAG = "FruitCollider";

    // Private Members  ########################################################
    private readonly HashSet<Collider> leftFruits = new HashSet<Collider>();
    private readonly HashSet<Collider> rightFruits = new HashSet<Collider>();

    // entry order across both sides, last is newest
    private readonly List<Collider> fruitOrder = new List<Collider>();

    private Collider grabbableFruit;
    private Collider reachTarget;
    private Collider lockedTarget; // fruit an arm is reaching for, else null

    // Private Methods  ########################################################
    // pinned fruit wins, else the one ArmRoot would pick on the reach side
    private void RefreshReachTarget() {
        Collider next =
            lockedTarget != null ? lockedTarget : GetNewestFruit(ReachSide);
        if (next == reachTarget) {
            return;
        }

        Collider previous = reachTarget;
        reachTarget = next;
        ReachTargetChanged?.Invoke(previous, next);
    }

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
