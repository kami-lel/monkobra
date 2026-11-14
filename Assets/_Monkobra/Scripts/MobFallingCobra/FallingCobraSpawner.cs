using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Perches dormant <see cref="FallingCobraController"/>s on the trunk of each
/// <see cref="TreeSegmentController"/> as soon as it has rolled its
/// branches, on its first Start and after every restart by
/// <see cref="DynamicTreeSegmentPool"/>, by listening to
/// <see cref="TreeSegmentController.BranchesGenerated"/>. Put one on
/// any object active from scene load, never under the pool's mock tree,
/// which is deactivated at runtime.
/// <para>
/// Rolled like branches: a fixed attempt count per segment, each attempt
/// against the falling cobra probability curve of
/// <see cref="CobraBalanceConfig"/> sampled at the segment's ramped
/// difficulty. Each cobra is a child of its segment, so a restart's clear
/// removes it.
/// </para>
/// <para>
/// A cobra wraps its resting body around an existing branch of the segment.
/// A segment without branches receives no cobra. Branches too close to
/// another cobra are retried, then skipped.
/// </para>
/// <para>
/// Ambush: with <see cref="CobraBalanceConfig.FallingCobraBananaAmbushChance"/>
/// a cobra instead hangs at the angle of a banana on its segment, a little
/// above it, so reaching for that banana is what wakes it. If the segment
/// has no banana, or no ambush spot fits, the cobra falls back to a random
/// spot.
/// </para>
/// </summary>
public class FallingCobraSpawner: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [FormerlySerializedAs("fallingCobraPrefab")]
    [Tooltip("falling cobra prefab, spawned dormant on a segment")]
    private FallingCobraController cobraPrefab;

    [SerializeField]
    [Tooltip("cobra balance tuning; cobra attempts, chance & speeds")]
    private CobraBalanceConfig cobraBalanceConfig;

    [Header("Cobra Position")]
    [SerializeField]
    [Tooltip(
        "ring radius about the trunk axis the cobra hangs and slides on, "
            + "about the monkey's orbit radius; u"
    )]
    private float spawnRadiusU = 5.5f;

    [SerializeField]
    [Tooltip("min distance b/t cobra pivots on one segment; u")]
    private float minCobraSeparationU = 4f;

    [SerializeField]
    [Tooltip("position retries per cobra b4 skipping it")]
    private int maxPlacementAttempts = 20;

    [Header("Banana Ambush")]
    [SerializeField]
    [Tooltip("lowest height of an ambush cobra's pivot above its banana; u")]
    private float ambushMinAboveBananaU = 3f;

    [SerializeField]
    [Tooltip("highest height of an ambush cobra's pivot above its banana; u")]
    private float ambushMaxAboveBananaU = 6f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (cobraPrefab == null) {
            Debug.LogWarning(
                "FallingCobraSpawner:\tmust assign Inspector Field: "
                    + "cobraPrefab",
                this
            );
        }
        if (cobraBalanceConfig == null) {
            Debug.LogWarning(
                "FallingCobraSpawner:\tmust assign Inspector Field: "
                    + "cobraBalanceConfig",
                this
            );
        }
        if (cobraPrefab == null || cobraBalanceConfig == null) {
            enabled = false;
        }
    }

    // scene OnEnables all run b4 any Start, so no segment rolls unheard.
    // First wins: a 2nd listener would roll every segment twice
    private void OnEnable() {
        if (listener != null && listener != this) {
            Debug.LogWarning(
                "FallingCobraSpawner:\tanother spawner already listens, this "
                    + "one stays idle",
                this
            );
            return;
        }
        listener = this;
        TreeSegmentController.BranchesGenerated += OnBranchesGenerated;
    }

    private void OnDisable() {
        if (listener != this) {
            return;
        }
        TreeSegmentController.BranchesGenerated -= OnBranchesGenerated;
        listener = null;
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        spawnRadiusU = Mathf.Max(0f, spawnRadiusU);
        minCobraSeparationU = Mathf.Max(0f, minCobraSeparationU);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
        ambushMinAboveBananaU = Mathf.Max(0f, ambushMinAboveBananaU);
        ambushMaxAboveBananaU = Mathf.Max(
            ambushMinAboveBananaU,
            ambushMaxAboveBananaU
        );
    }

    // Event Handlers  #########################################################
    private void OnBranchesGenerated(TreeSegmentController segment) {
        PopulateSegment(segment);
    }

    // constants  ##############################################################
    private const string FRUIT_TAG = "FruitCollider";

    // private members  ########################################################
    private static FallingCobraSpawner listener; // the one spawner subscribed

    private bool hasWarnedNoDifficulty; // missing service logged once
    private readonly List<FallingCobraController> spawnedCobras = new();

    // private methods  ########################################################
    // rolls this spawner's cobra attempts for segment, which already holds its
    // branches
    private void PopulateSegment(TreeSegmentController segment) {
        if (segment == null) {
            return;
        }
        RampedDifficultyManager difficulty = RampedDifficultyManager.I;
        if (difficulty == null) {
            if (!hasWarnedNoDifficulty) {
                Debug.LogWarning(
                    "FallingCobraSpawner:\tno RampedDifficultyManager in "
                        + "scene, skip cobras",
                    this
                );
                hasWarnedNoDifficulty = true;
            }
            return;
        }

        ClearCobras(segment);

        // pivots sit mid-segment, so this is the segment's middle difficulty
        float spawnProbability = Mathf.Clamp01(
            cobraBalanceConfig.FallingCobraSpawnProbabilityCurve.Evaluate(
                difficulty.GetRampedValueFromYPosition(
                    segment.transform.position.y
                )
            )
        );

        int attemptCount = cobraBalanceConfig.FallingCobraAttemptCount;
        for (int i = 0; i < attemptCount; i++) {
            if (Random.value < spawnProbability) {
                TrySpawnCobra(segment);
            }
        }
    }

    // a segment's previous cobras go b4 it rolls again: its restart has
    // already cleared them, any still active are destroyed here, so a
    // segment never carries 2 rolls
    private void ClearCobras(TreeSegmentController segment) {
        for (int i = spawnedCobras.Count - 1; i >= 0; i--) {
            FallingCobraController cobra = spawnedCobras[i];
            if (cobra != null && cobra.transform.parent != segment.transform) {
                continue;
            }
            // inactive first, Destroy only lands at the end of the frame
            if (cobra != null && cobra.gameObject.activeSelf) {
                cobra.gameObject.SetActive(false);
                Destroy(cobra.gameObject);
            }
            spawnedCobras.RemoveAt(i);
        }
    }

    private bool TrySpawnCobra(TreeSegmentController segment) {
        var branches = new List<BoxCollider>();
        var fruitBranches = new List<BoxCollider>();
        foreach (BranchController owner in segment.GetComponentsInChildren<BranchController>()) {
            foreach (BoxCollider box in owner.GetComponentsInChildren<BoxCollider>()) {
                if (!box.enabled || box.isTrigger || !box.CompareTag("Branch")) continue;
                branches.Add(box);
                if (owner.Kind != BranchKind.Empty) fruitBranches.Add(box);
            }
        }
        // Every waiting snake needs real wood beneath its coil. No branch means no spawn.
        if (branches.Count == 0) return false;
        List<BoxCollider> candidates = fruitBranches.Count > 0 && Random.value < cobraBalanceConfig.FallingCobraBananaAmbushChance
            ? fruitBranches : branches;
        FallingCobraController cobra = Instantiate(cobraPrefab, segment.transform);
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++) {
            BoxCollider branch = candidates[Random.Range(0, candidates.Count)];
            Vector3 branchCenter = branch.transform.TransformPoint(branch.center);
            Vector3 outward = branchCenter - segment.transform.position;
            outward.y = 0f; outward.Normalize();
            Vector3 position = new Vector3(segment.transform.position.x, branchCenter.y, segment.transform.position.z) + outward * spawnRadiusU;
            cobra.transform.SetPositionAndRotation(position, segment.transform.rotation);
            Physics.SyncTransforms();
            cobra.Initialize(cobraBalanceConfig, segment.transform.position);
            if (!cobra.AttachToBranch(branch) || !IsSpotFree(cobra.transform.position)) continue;
            RampedDifficultyManager difficulty = RampedDifficultyManager.I;
            if (difficulty != null && (difficulty.IsInBossZoneY(cobra.transform.position.y - cobra.TailBelowPivotU)
                || difficulty.IsInBossZoneY(cobra.transform.position.y)
                || difficulty.IsInBossZoneY(cobra.transform.position.y + cobra.LengthU - cobra.TailBelowPivotU))) continue;
            spawnedCobras.Add(cobra);
            return true;
        }
        cobra.gameObject.SetActive(false);
        Destroy(cobra.gameObject);
        return false;
    }

    private bool IsSpotFree(Vector3 position) {
        foreach (FallingCobraController other in spawnedCobras) {
            if (
                other != null
                && other.gameObject.activeInHierarchy
                && Vector3.Distance(other.transform.position, position)
                    < minCobraSeparationU
            ) {
                return false;
            }
        }
        return true;
    }
}
