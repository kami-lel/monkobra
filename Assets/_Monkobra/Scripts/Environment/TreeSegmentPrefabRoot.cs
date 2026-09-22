using UnityEngine;

/// <summary>
/// Prefab root for one tree segment. Registers itself with the
/// <see cref="TreeSegmentPoolManager"/> while enabled, so the pool tracks
/// every live instance without needing to know spawn order. Also
/// procedurally decorates itself with Branch prefabs on creation, each
/// optionally bearing a Banana Stem + Banana bunch.
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
    private GameObject branchPrefab;

    [SerializeField]
    private GameObject bananaStemPrefab;

    [SerializeField]
    private GameObject bananaPrefab;

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
        if (branchPrefab == null || bananaStemPrefab == null ||
            bananaPrefab == null) {
            Debug.LogWarning("must assign Inspector Fields: Branch Prefab, " +
                              "Banana Stem Prefab, Banana Prefab", this);
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

    // Constants  ###############################################################
    private const float BANANA_SPAWN_PROBABILITY = 0.5f;
    private const float STEM_PLACEMENT_RANGE = 0.4f;
    private const float STEM_Y_SCALE_MIN = 0.5f;
    private const float STEM_Y_SCALE_MAX = 1.5f;

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

        GameObject branch = Instantiate(branchPrefab, transform);
        branch.transform.SetLocalPositionAndRotation(branchPosition,
                                                        branchRotation);

        if (Random.value <= BANANA_SPAWN_PROBABILITY) {
            SpawnBanana(branch.transform);
        }
    }

    private void SpawnBanana(Transform branch) {
        float stemOffset =
            Random.Range(-STEM_PLACEMENT_RANGE, STEM_PLACEMENT_RANGE);
        Vector3 stemPosition = new Vector3(stemOffset, 0.5f, 0f);

        GameObject stem = Instantiate(bananaStemPrefab, branch);
        stem.transform.SetLocalPositionAndRotation(stemPosition,
                                                      Quaternion.identity);

        // float stemYScale = Random.Range(STEM_Y_SCALE_MIN, STEM_Y_SCALE_MAX);
        // Vector3 stemScale = bananaStemPrefab.transform.localScale;
        // stem.transform.localScale =
        //     new Vector3(stemScale.x, stemScale.y * stemYScale, stemScale.z);

        GameObject banana = Instantiate(bananaPrefab, branch);
        banana.transform.SetLocalPositionAndRotation(
            stemPosition + Vector3.up, Quaternion.identity);
    }
}
