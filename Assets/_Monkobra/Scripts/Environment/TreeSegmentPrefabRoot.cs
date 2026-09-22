using UnityEngine;

/// <summary>
/// Prefab root for one tree segment. Registers itself with the
/// <see cref="TreeSegmentPoolManager"/> while enabled, so the pool tracks
/// every live instance without needing to know spawn order.
/// </summary>
public class TreeSegmentPrefabRoot: MonoBehaviour {
    // Public Methods  #########################################################
    /// <returns>the segment's world-space height, read from its Collider
    /// bounds</returns>
    public float GetHeight() {
        return segmentCollider.bounds.size.y;
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    private Collider segmentCollider;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (segmentCollider == null) {
            Debug.LogWarning("must assign Inspector Field: Segment Collider",
                              this);
        }
    }

    private void OnEnable() {
        if (TreeSegmentPoolManager.I != null) {
            TreeSegmentPoolManager.I.RegisterSegment(this);
        }
    }

    private void OnDisable() {
        if (TreeSegmentPoolManager.I != null) {
            TreeSegmentPoolManager.I.UnregisterSegment(this);
        }
    }
}
