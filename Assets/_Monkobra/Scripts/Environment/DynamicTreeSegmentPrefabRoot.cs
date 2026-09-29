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

    [SerializeField]
    [Tooltip("shared branch placement tuning")]
    private BranchFruitPlacementConfig placementConfig;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (segmentCollider == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\t"
                    + "must assign Inspector Field: segmentCollider",
                this
            );
        }
        if (branchWithFruitPrefab == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\t"
                    + "must assign Inspector Field: branchWithFruitPrefab",
                this
            );
        }

        if (placementConfig == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\t"
                    + "must assign Inspector Field: placementConfig",
                this
            );
        }

        if (
            segmentCollider == null
            || branchWithFruitPrefab == null
            || placementConfig == null
        ) {
            enabled = false;
        }
    }

    // wait for Start, so TreeManager.I is set whatever the Awake order
    private void Start() {
        if (!enabled) {
            return;
        }
        if (TreeManager.I == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\tno TreeManager in scene, "
                    + "skip branch generation",
                this
            );
            return;
        }
        GenerateBranches();
    }

    // private methods  ########################################################
    private void GenerateBranches() {
        float segmentHeight = GetHeight();
        List<Vector3> placedPositions = new();

        // higher segment in tree → more branches, max count reached at
        // MaxCountProgress of the climb and kept above it
        int minBranchCount = placementConfig.MinBranchCount;
        int maxBranchCount = placementConfig.MaxBranchCount;
        float maxCountProgress = placementConfig.MaxCountProgress;

        float segmentY = segmentCollider.bounds.center.y;
        float progress = Mathf.InverseLerp(
            TreeManager.I.MinY,
            TreeManager.I.MaxY,
            segmentY
        );
        float countRatio =
            maxCountProgress <= 0f
                ? 1f
                : Mathf.Clamp01(progress / maxCountProgress);
        int branchCount = Mathf.RoundToInt(
            Mathf.Lerp(minBranchCount, maxBranchCount, countRatio)
        );

        for (int i = 0; i < branchCount; i++) {
            SpawnBranch(segmentHeight, placedPositions);
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"DynamicTreeSegmentPrefabRoot:\tsegment y {segmentY} "
                    + $"(climb progress {progress:F2}), "
                    + $"spawned {branchCount} branches "
                    + $"(range {minBranchCount}~{maxBranchCount})",
                this
            );
        }
    }

    private void SpawnBranch(float segmentHeight, List<Vector3> placedPositions) {
        Quaternion branchRotation = Quaternion.identity;
        Vector3 branchPosition = Vector3.zero;

        int maxAttempts = placementConfig.MaxPlacementAttempts;
        for (int attempt = 0; attempt < maxAttempts; attempt++) {
            branchRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float branchHeight = Random.Range(0f, segmentHeight);
            branchPosition =
                branchRotation * Vector3.forward * placementConfig.BranchRadius
                + Vector3.up * branchHeight;

            if (IsFarEnoughFromExisting(branchPosition, placedPositions)) {
                break;
            }
        }

        placedPositions.Add(branchPosition);

        GameObject branch = Instantiate(branchWithFruitPrefab, transform);
        branch.transform.SetLocalPositionAndRotation(
            branchPosition,
            branchRotation
        );
    }

    private bool IsFarEnoughFromExisting(
        Vector3 candidate,
        List<Vector3> placedPositions
    ) {
        foreach (Vector3 placed in placedPositions) {
            if (
                Vector3.Distance(candidate, placed)
                < placementConfig.MinBranchSeparation
            ) {
                return false;
            }
        }
        return true;
    }
}
