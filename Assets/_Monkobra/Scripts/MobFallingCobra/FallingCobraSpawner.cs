using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Hangs dormant <see cref="FallingCobraController"/>s on the trunk of each
/// <see cref="DynamicTreeSegmentPrefabRoot"/> as soon as it has rolled its
/// branches, on its first Start and after every restart by
/// <see cref="DTSPool"/>, by listening to
/// <see cref="DynamicTreeSegmentPrefabRoot.BranchesGenerated"/>. Put one on
/// any object active from scene load, never under the pool's mock tree,
/// which is deactivated at runtime.
/// <para>
/// Rolled like branches: a fixed attempt count per segment, each attempt
/// against the falling cobra probability curve of
/// <see cref="GameBalanceConfig"/> sampled at the segment's ramped
/// difficulty. Each cobra is a child of its segment, so a restart's clear
/// removes it.
/// </para>
/// <para>
/// A cobra hangs at a random angle on a ring of <c>spawnRadiusU</c> about the
/// segment's pivot, which sits on the trunk axis, at a random height that
/// keeps the whole snake inside the segment. A spot too close to another
/// cobra of the segment is retried, and skipped when retries run out.
/// </para>
/// <para>
/// Ambush: with <see cref="GameBalanceConfig.FallingCobraBananaAmbushChance"/>
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
    [Tooltip("game balance tuning; cobra attempts, chance & speeds")]
    private GameBalanceConfig balanceConfig;

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
        if (balanceConfig == null) {
            Debug.LogWarning(
                "FallingCobraSpawner:\tmust assign Inspector Field: "
                    + "balanceConfig",
                this
            );
        }
        if (cobraPrefab == null || balanceConfig == null) {
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
        DynamicTreeSegmentPrefabRoot.BranchesGenerated += OnBranchesGenerated;
    }

    private void OnDisable() {
        if (listener != this) {
            return;
        }
        DynamicTreeSegmentPrefabRoot.BranchesGenerated -= OnBranchesGenerated;
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
    private void OnBranchesGenerated(DynamicTreeSegmentPrefabRoot segment) {
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
    private void PopulateSegment(DynamicTreeSegmentPrefabRoot segment) {
        if (segment == null) {
            return;
        }
        RampedDifficultyService difficulty = RampedDifficultyService.I;
        if (difficulty == null) {
            if (!hasWarnedNoDifficulty) {
                Debug.LogWarning(
                    "FallingCobraSpawner:\tno RampedDifficultyService in "
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
            balanceConfig.FallingCobraSpawnProbabilityCurve.Evaluate(
                difficulty.GetRampedValueFromYPosition(
                    segment.transform.position.y
                )
            )
        );

        int attemptCount = balanceConfig.FallingCobraAttemptCount;
        for (int i = 0; i < attemptCount; i++) {
            if (Random.value < spawnProbability) {
                TrySpawnCobra(segment);
            }
        }
    }

    // a segment's previous cobras go b4 it rolls again: its restart has
    // already cleared them, any still active are destroyed here, so a
    // segment never carries 2 rolls
    private void ClearCobras(DynamicTreeSegmentPrefabRoot segment) {
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

    private bool TrySpawnCobra(DynamicTreeSegmentPrefabRoot segment) {
        Transform segmentXfm = segment.transform;

        // the prefab builds its hit capsule in Awake, so its length is only
        // known on an instance
        FallingCobraController cobra = Instantiate(cobraPrefab, segmentXfm);

        // the trunk cylinder is centered on the segment pivot; keep the whole
        // snake, tail to head, inside the segment's own height
        float halfHeight = segment.GetHeight() * 0.5f;
        float tailU = cobra.TailBelowPivotU;
        float minLocalY = -halfHeight + tailU;
        float maxLocalY = halfHeight - (cobra.LengthU - tailU);
        if (maxLocalY < minLocalY) {
            Destroy(cobra.gameObject);
            return false;
        }

        // an ambush aims at one banana of the segment, picked once
        Vector3? bananaLocal = null;
        if (Random.value < balanceConfig.FallingCobraBananaAmbushChance) {
            bananaLocal = PickBananaLocal(segmentXfm);
        }

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++) {
            // above a banana, at its angle round the trunk, else anywhere
            Vector3 localPosition = Vector3.zero;
            bool isAmbush =
                bananaLocal.HasValue
                && TryAmbushLocal(
                    bananaLocal.Value,
                    minLocalY,
                    maxLocalY,
                    out localPosition
                );
            if (!isAmbush) {
                Quaternion yaw =
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                localPosition =
                    yaw * Vector3.forward * spawnRadiusU
                    + Vector3.up * Random.Range(minLocalY, maxLocalY);
            }
            Vector3 position = segmentXfm.TransformPoint(localPosition);
            if (!IsSpotFree(position)) {
                // a taken ambush spot falls back to random spots
                bananaLocal = null;
                continue;
            }

            cobra.transform.SetPositionAndRotation(
                position,
                segmentXfm.rotation
            );
            Physics.SyncTransforms();
            cobra.Initialize(balanceConfig, segmentXfm.position);
            spawnedCobras.Add(cobra);
            return true;
        }

        Destroy(cobra.gameObject);
        return false;
    }

    // a random banana still shown on the segment, in segment-local space,
    // or null when the segment bears none
    private static Vector3? PickBananaLocal(Transform segmentXfm) {
        List<Vector3> bananas = new();
        foreach (
            Collider fruit in segmentXfm.GetComponentsInChildren<Collider>()
        ) {
            if (fruit.CompareTag(FRUIT_TAG) && fruit.enabled) {
                // transform, not bounds: a just-placed branch has no
                // synced physics bounds yet
                bananas.Add(
                    segmentXfm.InverseTransformPoint(
                        fruit.transform.position
                    )
                );
            }
        }
        if (bananas.Count == 0) {
            return null;
        }
        return bananas[Random.Range(0, bananas.Count)];
    }

    // a spot on the cobra ring at the banana's angle, a random height above
    // it, iff that height keeps the snake inside the segment
    private bool TryAmbushLocal(
        Vector3 bananaLocal,
        float minLocalY,
        float maxLocalY,
        out Vector3 localPosition
    ) {
        localPosition = Vector3.zero;
        Vector3 flat = new Vector3(bananaLocal.x, 0f, bananaLocal.z);
        if (flat.sqrMagnitude < 0.0001f) {
            return false;
        }

        float lowY = Mathf.Max(
            minLocalY,
            bananaLocal.y + ambushMinAboveBananaU
        );
        float highY = Mathf.Min(
            maxLocalY,
            bananaLocal.y + ambushMaxAboveBananaU
        );
        if (highY < lowY) {
            return false;
        }

        localPosition =
            flat.normalized * spawnRadiusU
            + Vector3.up * Random.Range(lowY, highY);
        return true;
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
