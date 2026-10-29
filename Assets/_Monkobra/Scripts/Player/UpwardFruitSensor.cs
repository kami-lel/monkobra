using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks which side of the character has fruit within reach, fed by 2
/// <see cref="DetectionZoneRelay"/> children that forward their trigger events.
/// Keeps every "FruitCollider" overlap per side, so ReachForLeft and
/// ReachForRight hold with several fruits at once. Also relays which fruit a
/// hand currently overlaps, so a release would grab it.
/// </summary>
public class UpwardFruitSensor: MonoBehaviour {
    // Public Members  #########################################################
    public static UpwardFruitSensor I {
        get; private set;
    }

    public bool ReachForLeft => leftFruits.Count > 0;
    public bool ReachForRight => rightFruits.Count > 0;

    /// <summary>
    /// side of the fruit closest to the monkey across both zones, left when
    /// none is in reach
    /// </summary>
    public DetectionSide ReachSide => FindClosestFruit(out DetectionSide side)
        != null ? side : DetectionSide.Left;

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

    /// <returns>fruit in reach on <paramref name="side"/> nearest to the
    /// monkey, the older entry on a tie, else null</returns>
    public Collider GetClosestFruit(DetectionSide side) {
        HashSet<Collider> fruits =
            side == DetectionSide.Left ? leftFruits : rightFruits;
        Collider closest = null;
        float closestSqrDistance = float.MaxValue;
        for (int i = 0; i < fruitOrder.Count; i++) {
            Collider fruit = fruitOrder[i];
            if (!fruits.Contains(fruit)) {
                continue;
            }

            float sqrDistance = (fruit.bounds.center - transform.position)
                .sqrMagnitude;
            // strict lt keeps the older entry on a tie
            if (sqrDistance < closestSqrDistance) {
                closestSqrDistance = sqrDistance;
                closest = fruit;
            }
        }
        return closest;
    }

    /// <summary>
    /// forgets <paramref name="fruit"/> on both sides, for a fruit switched
    /// off inside a zone, whose trigger exit may never fire
    /// </summary>
    public void RemoveFruit(Collider fruit) {
        bool wasInReach = IsFruitInReach(fruit);
        leftFruits.Remove(fruit);
        rightFruits.Remove(fruit);
        fruitOrder.Remove(fruit);
        if (wasInReach) {
            FruitReachChanged?.Invoke(fruit, false);
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
    private DetectionZoneRelay leftZone;

    [Tooltip("right-side detection zone, must report Side == Right")]
    [SerializeField]
    private DetectionZoneRelay rightZone;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (leftZone == null || rightZone == null) {
            Debug.LogWarning(
                "UpwardFruitSensor:\tmust assign Inspector Fields: "
                    + "leftZone, rightZone",
                this
            );
        }

        // drop only this duplicate component, the GameObject may hold more
        if (I != null && I != this) {
            Debug.LogWarning(
                "UpwardFruitSensor:\tduplicate instance, removing",
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

    private void FixedUpdate() {
        // the closest fruit changes as the monkey climbs, not just on events
        RefreshReachTarget();
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
    // closest fruit across both zones and the side it sits on; a fruit in both
    // zones takes the side of the monkey's right vector it lies on
    private Collider FindClosestFruit(out DetectionSide side) {
        side = DetectionSide.Left;
        Collider left = GetClosestFruit(DetectionSide.Left);
        Collider right = GetClosestFruit(DetectionSide.Right);
        if (left == null && right == null) {
            return null;
        }

        Collider closest = left;
        if (left == null || right != null && IsCloser(right, left)) {
            closest = right;
        }

        if (leftFruits.Contains(closest) && rightFruits.Contains(closest)) {
            float sideDot = Vector3.Dot(
                closest.bounds.center - transform.position,
                transform.right
            );
            side = sideDot > 0f ? DetectionSide.Right : DetectionSide.Left;
        } else {
            side = rightFruits.Contains(closest)
                ? DetectionSide.Right
                : DetectionSide.Left;
        }
        return closest;
    }

    // strict, so an equal distance keeps the left candidate
    private bool IsCloser(Collider a, Collider b) {
        Vector3 origin = transform.position;
        return (a.bounds.center - origin).sqrMagnitude
            < (b.bounds.center - origin).sqrMagnitude;
    }

    // pinned fruit wins, else the closest fruit across both zones
    private void RefreshReachTarget() {
        Collider next = lockedTarget != null
            ? lockedTarget
            : FindClosestFruit(out DetectionSide _);
        if (next == reachTarget) {
            return;
        }

        Collider previous = reachTarget;
        reachTarget = next;
        ReachTargetChanged?.Invoke(previous, next);
    }
}
