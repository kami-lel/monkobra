using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prefab root for one dynamic tree segment. Procedurally decorates itself
/// with Branch With Fruit prefabs on creation, and again each time
/// <see cref="Restart"/> moves it to a new place.
/// </summary>
public class DynamicTreeSegmentPrefabRoot: MonoBehaviour {
    // Public Methods  #########################################################
    /// <returns>the segment's world-space height, read from its Collider
    /// bounds</returns>
    public float GetHeight() {
        return segmentCollider.bounds.size.y;
    }

    /// <summary>
    /// Moves the segment to <paramref name="worldPosition"/> and starts it
    /// over: everything spawned onto it (branches, webs) is removed and the
    /// branches are rolled again for the new height. Callers that add more
    /// decoration, e.g. <see cref="SpiderWebSpawner"/>, redo theirs after.
    /// </summary>
    public void Restart(Vector3 worldPosition) {
        if (!isReady) {
            return;
        }

        ClearDecorations();
        transform.position = worldPosition;
        // the collider bounds read for placement must follow the move
        Physics.SyncTransforms();

        if (TreeManager.I == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\tno TreeManager in scene, "
                    + "skip branch generation",
                this
            );
        } else {
            GenerateBranches();
        }
        hasGenerated = true;
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

    [SerializeField]
    [Tooltip("game balance tuning; branch attempts & chance")]
    private GameBalanceConfig balanceConfig;

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

        if (balanceConfig == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:	"
                    + "must assign Inspector Field: balanceConfig",
                this
            );
        }

        if (
            segmentCollider == null
            || branchWithFruitPrefab == null
            || placementConfig == null
            || balanceConfig == null
        ) {
            enabled = false;
            return;
        }

        // whatever sits under the root now is part of the prefab, keep it
        // through every restart
        foreach (Transform child in transform) {
            prefabChildren.Add(child);
        }
        isReady = true;
    }

    // wait for Start, so TreeManager.I is set whatever the Awake order
    private void Start() {
        if (!enabled || hasGenerated) {
            return;
        }
        hasGenerated = true;
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

    // private members  ########################################################
    private readonly HashSet<Transform> prefabChildren = new();
    private bool isReady; // if Awake passed the Inspector guard
    private bool hasGenerated; // if branches were rolled once already

    // private methods  ########################################################
    // inactive first, Destroy only lands at the end of the frame and the
    // dead branches must not block physics queries on the restart frame
    private void ClearDecorations() {
        for (int i = transform.childCount - 1; i >= 0; i--) {
            Transform child = transform.GetChild(i);
            if (prefabChildren.Contains(child)) {
                continue;
            }
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void GenerateBranches() {
        float segmentHeight = GetHeight();
        List<Vector3> placedPositions = new();

        // fixed attempt count, each attempt spawns a branch by the chance the
        // curve gives at this segment's ramped difficulty
        float difficulty = GetRampedDifficulty(
            segmentCollider.bounds.center.y
        );
        float spawnProbability = Mathf.Clamp01(
            balanceConfig.BranchSpawnProbabilityCurve.Evaluate(difficulty)
        );

        int attemptCount = balanceConfig.BranchGenerationAttemptCount;
        int branchCount = 0;
        for (int i = 0; i < attemptCount; i++) {
            if (Random.value < spawnProbability) {
                SpawnBranch(segmentHeight, placedPositions);
                branchCount++;
            }
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                "DynamicTreeSegmentPrefabRoot:\t"
                    + $"branches {branchCount}/{attemptCount} "
                    + $"(difficulty {difficulty:F2}, p {spawnProbability:F2})",
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

        // banana chance follows the difficulty at the branch's own height
        float bananaProbability = Mathf.Clamp01(
            balanceConfig.BananaSpawnProbabilityCurve.Evaluate(
                GetRampedDifficulty(branch.transform.position.y)
            )
        );
        BranchWithScriptRoot branchRoot =
            branch.GetComponent<BranchWithScriptRoot>();
        if (branchRoot == null) {
            Debug.LogWarning(
                "DynamicTreeSegmentPrefabRoot:\t"
                    + "branchWithFruitPrefab lacks BranchWithScriptRoot",
                this
            );
        } else {
            branchRoot.Initialize(bananaProbability);
        }
    }

    // 0 when no service in scene
    private float GetRampedDifficulty(float worldY) {
        return RampedDifficultyService.I != null
            ? RampedDifficultyService.I.GetRampedValueFromYPosition(worldY)
            : 0f;
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
