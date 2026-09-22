using UnityEngine;

// BUG true randomness generate cluster of branches

/// <summary>
/// Prefab root for one tree segment. Registers itself with the
/// <see cref="TreeSegmentPoolManager"/> while enabled, so the pool tracks
/// every live instance without needing to know spawn order. Also
/// procedurally decorates itself with Branch With Fruit prefabs on
/// creation.
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

    [SerializeField]
    private GameObject branchWithFruitPrefab;

    [SerializeField]
    private int minBranchCount = 1;

    [SerializeField]
    private int maxBranchCount = 5;

    [SerializeField]
    private float branchRadius = 6.6f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (segmentCollider == null) {
            Debug.LogWarning("must assign Inspector Field: Segment Collider",
                              this);
        }
        if (branchWithFruitPrefab == null) {
            Debug.LogWarning(
                "must assign Inspector Field: Branch With Fruit Prefab",
                this);
            return;
        }

        GenerateBranches();
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

    // Private Methods  #########################################################
    private void GenerateBranches() {
        float segmentHeight = GetHeight();

        int branchCount = Random.Range(minBranchCount, maxBranchCount + 1);
        for (int i = 0; i < branchCount; i++) {
            SpawnBranch(segmentHeight);
        }
    }

    private void SpawnBranch(float segmentHeight) {
        Quaternion branchRotation =
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        float branchHeight = Random.Range(0f, segmentHeight);
        Vector3 branchPosition = branchRotation * Vector3.forward *
                                  branchRadius + Vector3.up * branchHeight;

        GameObject branch = Instantiate(branchWithFruitPrefab, transform);
        branch.transform.SetLocalPositionAndRotation(branchPosition,
                                                        branchRotation);
    }
}
