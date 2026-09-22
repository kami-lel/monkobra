using System.Collections.Generic;
using UnityEngine;


// fixme improve branch placement randomness
//

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

    [SerializeField]
    private float minBranchSeparation = 3f;

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

    // Constants  ###############################################################
    private const int MAX_PLACEMENT_ATTEMPTS = 30;

    // Private Methods  #########################################################
    private void GenerateBranches() {
        float segmentHeight = GetHeight();
        List<Vector3> placedPositions = new();

        int branchCount = Random.Range(minBranchCount, maxBranchCount + 1);
        for (int i = 0; i < branchCount; i++) {
            SpawnBranch(segmentHeight, placedPositions);
        }
    }

    private void SpawnBranch(float segmentHeight,
                              List<Vector3> placedPositions) {
        Quaternion branchRotation = Quaternion.identity;
        Vector3 branchPosition = Vector3.zero;

        for (int attempt = 0; attempt < MAX_PLACEMENT_ATTEMPTS; attempt++) {
            branchRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float branchHeight = Random.Range(0f, segmentHeight);
            branchPosition = branchRotation * Vector3.forward *
                              branchRadius + Vector3.up * branchHeight;

            if (IsFarEnoughFromExisting(branchPosition, placedPositions)) {
                break;
            }
        }

        placedPositions.Add(branchPosition);

        GameObject branch = Instantiate(branchWithFruitPrefab, transform);
        branch.transform.SetLocalPositionAndRotation(branchPosition,
                                                        branchRotation);
    }

    private bool IsFarEnoughFromExisting(Vector3 candidate,
                                          List<Vector3> placedPositions) {
        foreach (Vector3 placed in placedPositions) {
            if (Vector3.Distance(candidate, placed) < minBranchSeparation) {
                return false;
            }
        }
        return true;
    }
}
