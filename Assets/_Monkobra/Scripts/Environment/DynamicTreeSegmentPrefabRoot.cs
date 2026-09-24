using System.Collections.Generic;
using UnityEngine;



/// <summary>
/// Prefab root for one dynamic tree segment. Procedurally decorates itself
/// with Branch With Fruit prefabs on creation.
/// </summary>
public class DynamicTreeSegmentPrefabRoot: MonoBehaviour {
    // Public Methods  #########################################################
    /// <returns>the segment's world-space height, read from its Collider
    /// bounds</returns>
    public float GetHeight() {
        return segmentCollider.bounds.size.y;
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("collider measuring segment height")]
    private Collider segmentCollider;

    [SerializeField]
    [Tooltip("branch prefab spawned on segment")]
    private GameObject branchWithFruitPrefab;

    [Header("Branch Placement")]
    [SerializeField]
    [Tooltip("min branches per segment")]
    private int minBranchCount = 1;

    [SerializeField]
    [Tooltip("max branches per segment")]
    private int maxBranchCount = 5;

    [SerializeField]
    [Tooltip("branch distance fr trunk axis; u")]
    private float branchRadius = 6.6f;

    [SerializeField]
    [Tooltip("min distance b/t branches; u")]
    private float minBranchSeparation = 3f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (segmentCollider == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\t"
                + "must assign Inspector Field: segmentCollider",
                this);
        }
        if (branchWithFruitPrefab == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\t"
                + "must assign Inspector Field: branchWithFruitPrefab",
                this);
        }

        if (segmentCollider == null || branchWithFruitPrefab == null) {
            return;
        }
        GenerateBranches();
    }

    // constants  ##############################################################
    private const int MAX_PLACEMENT_ATTEMPTS = 30;

    // private methods  ########################################################
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
