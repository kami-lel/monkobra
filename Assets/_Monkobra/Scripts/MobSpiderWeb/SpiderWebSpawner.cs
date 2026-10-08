using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sticks <see cref="SpiderWebTrap"/> webs flat on the trunk of each
/// <see cref="DynamicTreeSegmentPrefabRoot"/> as soon as it has rolled its
/// branches, on its first Start and after every restart by
/// <see cref="DTSPool"/>, by listening to
/// <see cref="DynamicTreeSegmentPrefabRoot.BranchesGenerated"/>. Put one on
/// any object active from scene load, never under the pool's mock tree,
/// which is deactivated at runtime.
/// <para>
/// The spawn rate comes from the Spider Web group of
/// <see cref="GameBalanceConfig"/>: no web below the start climb, then each
/// slot's chance, read at the segment's middle, follows the chance curve up
/// to the max chance at the full-chance climb and stays there. A climb
/// height is a spot's height above the player's start y, from
/// <see cref="RampedDifficultyService"/>, so it is the distance the monkey
/// climbs to meet the web, whatever the world y.
/// </para>
/// <para>
/// A web lies on its segment's own stretch of bark facing outward: the
/// prefab's strands sit in its local YZ plane, so local +X, the face normal,
/// is turned away from the trunk axis. A candidate spot is dropped if it is
/// below the start climb, too close to another web, or its trigger volume
/// overlaps any branch or fruit. Out of retries, the web is skipped rather
/// than placed overlapping.
/// </para>
/// <para>
/// Webs are children of their segment, so a restart's clear removes them.
/// The spawner also drops a segment's previous webs before rolling it again,
/// so none linger and none double up.
/// </para>
/// </summary>
public class SpiderWebSpawner: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip(
        "web prefab, its trigger BoxCollider is the volume kept clear of "
            + "branches and fruit"
    )]
    private SpiderWebTrap webPrefab;

    [SerializeField]
    [Tooltip("game balance tuning; web slots, start climb & chance ramp")]
    private GameBalanceConfig balanceConfig;

    [Header("Web Position")]
    [SerializeField]
    [Tooltip(
        "trunk radius the web sits on, half the TreeSegment cylinder's "
            + "X scale; u"
    )]
    private float trunkRadiusU = 5f;

    [SerializeField]
    [Tooltip("gap b/t bark and web plane, keeps strands out of the trunk; u")]
    private float surfaceOffsetU = 0.02f;

    [SerializeField]
    [Tooltip("min distance b/t web centers; u")]
    private float minWebSeparationU = 4f;

    [SerializeField]
    [Tooltip(
        "padding around the web trigger that must also be free of branches "
            + "and fruit; u"
    )]
    private float clearanceU = 0.3f;

    [SerializeField]
    [Tooltip("position retries per web b4 skipping it")]
    private int maxPlacementAttempts = 30;

    [Header("Testing")]
    [SerializeField]
    [Tooltip(
        "replace the balance start climb and chance ramp w/ the 2 test "
            + "values below. Tick it b4 Play to cover the first stack, and "
            + "leave the scene unsaved"
    )]
    private bool useTestOverride = false;

    [SerializeField]
    [Tooltip(
        "start climb above the player's start y while testing; u. 0 may put "
            + "a web on the monkey's start spot"
    )]
    private float testStartClimbU = 5f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("chance 0~1 that one slot gets a web while testing, any height")]
    private float testSpawnChance = 1f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (webPrefab == null) {
            Debug.LogWarning(
                "SpiderWebSpawner:\tmust assign Inspector Field: webPrefab",
                this
            );
        }
        if (balanceConfig == null) {
            Debug.LogWarning(
                "SpiderWebSpawner:\tmust assign Inspector Field: "
                    + "balanceConfig",
                this
            );
        }
        if (webPrefab == null || balanceConfig == null) {
            enabled = false;
            return;
        }

        // the prefab's trigger, scaled by its root, as each web will carry it
        prefabTrigger = webPrefab.GetComponent<BoxCollider>();
        Vector3 prefabScale = webPrefab.transform.localScale;
        webTriggerCenter = Vector3.Scale(prefabTrigger.center, prefabScale);
        webTriggerHalfSize =
            Vector3.Scale(prefabTrigger.size, prefabScale) * 0.5f;
    }

    // scene OnEnables all run b4 any Start, so no segment rolls unheard.
    // First wins: a 2nd listener would roll every segment twice
    private void OnEnable() {
        if (listener != null && listener != this) {
            Debug.LogWarning(
                "SpiderWebSpawner:\tanother spawner already listens, this "
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

    // trigger volume of every spawned web, plus a ring at the start climb
    // once the run's start y is known, visible in the Scene view, and in the
    // Game view with Gizmos on
    private void OnDrawGizmos() {
        if (prefabTrigger == null) {
            return;
        }

        Gizmos.color = GIZMO_WEB_COLOR;
        foreach (SpiderWebTrap web in spawnedWebs) {
            if (web == null) {
                continue;
            }
            Gizmos.matrix = web.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(prefabTrigger.center, prefabTrigger.size);
        }
        Gizmos.matrix = Matrix4x4.identity;

        if (balanceConfig != null && RampedDifficultyService.I != null) {
            float startY =
                RampedDifficultyService.I.PlayerGameStartYPosition
                + StartClimbU;
            Gizmos.color = GIZMO_START_COLOR;
            DrawRing(
                new Vector3(transform.position.x, startY, transform.position.z),
                trunkRadiusU + GIZMO_RING_PADDING_U
            );
        }
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        trunkRadiusU = Mathf.Max(0f, trunkRadiusU);
        surfaceOffsetU = Mathf.Max(0f, surfaceOffsetU);
        minWebSeparationU = Mathf.Max(0f, minWebSeparationU);
        clearanceU = Mathf.Max(0f, clearanceU);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
        testStartClimbU = Mathf.Max(0f, testStartClimbU);
    }

    // Event Handlers  #########################################################
    private void OnBranchesGenerated(DynamicTreeSegmentPrefabRoot segment) {
        PopulateSegment(segment);
    }

    // constants  ##############################################################
    private const string BRANCH_TAG = "Branch";
    private const string FRUIT_TAG = "FruitCollider";
    private const int GIZMO_RING_SEGMENTS = 48;
    private const float GIZMO_RING_PADDING_U = 1f; // ring sits off the bark
    private static readonly Color GIZMO_WEB_COLOR = Color.yellow;
    private static readonly Color GIZMO_START_COLOR = Color.cyan;

    // private members  ########################################################
    private static SpiderWebSpawner listener; // the one spawner subscribed

    private Vector3 webTriggerCenter; // web-local, prefab scale applied
    private Vector3 webTriggerHalfSize; // web-local, prefab scale applied
    private bool hasWarnedNoDifficulty; // missing service logged once
    private readonly List<SpiderWebTrap> spawnedWebs = new();

    // start climb in force, the test one while overriding; u
    private float StartClimbU =>
        useTestOverride ? testStartClimbU : balanceConfig.WebStartClimbU;

    // cached references  ------------------------------------------------------
    private BoxCollider prefabTrigger;

    // private methods  ########################################################
    // rolls this spawner's web slots for segment, which already holds its
    // branches
    private void PopulateSegment(DynamicTreeSegmentPrefabRoot segment) {
        if (segment == null || prefabTrigger == null) {
            return;
        }
        RampedDifficultyService difficulty = RampedDifficultyService.I;
        if (difficulty == null) {
            if (!hasWarnedNoDifficulty) {
                Debug.LogWarning(
                    "SpiderWebSpawner:\tno RampedDifficultyService in scene, "
                        + "skip webs",
                    this
                );
                hasWarnedNoDifficulty = true;
            }
            return;
        }

        ClearWebs(segment);

        // pivots sit mid-segment, so this is the segment's middle climb
        float climbedU =
            difficulty.GetClimbedYDistance(segment.transform.position.y);
        float chance = GetSpawnChance(climbedU);
        if (chance <= 0f) {
            return;
        }

        // branches were posed through their transforms this frame, push the
        // poses into physics so the overlap test can see them
        Physics.SyncTransforms();

        int slotCount = balanceConfig.WebSlotsPerSegment;
        int spawnedCount = 0;
        for (int slot = 0; slot < slotCount; slot++) {
            if (Random.value < chance && TrySpawnWeb(segment, difficulty)) {
                spawnedCount++;
            }
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"SpiderWebSpawner:\twebs {spawnedCount}/{slotCount} on "
                    + $"{segment.name} (climb {climbedU:F0}u, p {chance:F2})",
                segment
            );
        }
    }

    // a segment's previous webs go b4 it rolls again: its restart has
    // already cleared them, any still active are destroyed here, so a
    // segment never carries 2 rolls. Destroyed webs, e.g. broken free, are
    // forgotten on the way
    private void ClearWebs(DynamicTreeSegmentPrefabRoot segment) {
        for (int i = spawnedWebs.Count - 1; i >= 0; i--) {
            SpiderWebTrap web = spawnedWebs[i];
            if (web != null && web.transform.parent != segment.transform) {
                continue;
            }
            // inactive first, Destroy only lands at the end of the frame
            if (web != null && web.gameObject.activeSelf) {
                web.gameObject.SetActive(false);
                Destroy(web.gameObject);
            }
            spawnedWebs.RemoveAt(i);
        }
    }

    // chance 0~1 that one slot gets a web at climbedU above the start y
    private float GetSpawnChance(float climbedU) {
        if (climbedU < StartClimbU) {
            return 0f;
        }
        if (useTestOverride) {
            return testSpawnChance;
        }

        float startU = balanceConfig.WebStartClimbU;
        float fullU = balanceConfig.WebFullChanceClimbU;
        // a zero-length ramp is at its top as soon as it starts
        float progress = fullU > startU
            ? Mathf.InverseLerp(startU, fullU, climbedU)
            : 1f;
        return Mathf.Clamp01(
            balanceConfig.WebMaxSpawnChance
                * balanceConfig.WebSpawnChanceCurve.Evaluate(progress)
        );
    }

    private bool TrySpawnWeb(
        DynamicTreeSegmentPrefabRoot segment,
        RampedDifficultyService difficulty
    ) {
        Transform segmentXfm = segment.transform;

        // the trunk cylinder is centered on the segment pivot, so this keeps
        // the whole web on the segment's own bark, never past the tree top
        float webHalfHeight =
            webTriggerHalfSize.y + Mathf.Abs(webTriggerCenter.y);
        float maxLocalY = Mathf.Max(
            0f,
            segment.GetHeight() * 0.5f - webHalfHeight
        );

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++) {
            Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Vector3 localPosition =
                yaw * Vector3.forward * (trunkRadiusU + surfaceOffsetU)
                + Vector3.up * Random.Range(-maxLocalY, maxLocalY);
            // -90 yaw turns local +X onto forward, then yaw turns forward
            // onto the outward direction, so the face looks away from trunk
            Quaternion localRotation = yaw * Quaternion.Euler(0f, -90f, 0f);

            Vector3 position = segmentXfm.TransformPoint(localPosition);
            Quaternion rotation = segmentXfm.rotation * localRotation;
            if (!IsSpotClear(position, rotation, difficulty)) {
                continue;
            }

            SpiderWebTrap web = Instantiate(
                webPrefab,
                position,
                rotation,
                segmentXfm
            );
            spawnedWebs.Add(web);
            return true;
        }

        return false;
    }

    private bool IsSpotClear(
        Vector3 position,
        Quaternion rotation,
        RampedDifficultyService difficulty
    ) {
        if (difficulty.GetClimbedYDistance(position.y) < StartClimbU) {
            return false;
        }

        // a web already cleared off a restarted segment is inactive and
        // never counts
        foreach (SpiderWebTrap web in spawnedWebs) {
            if (
                web != null
                && web.gameObject.activeInHierarchy
                && Vector3.Distance(web.transform.position, position)
                    < minWebSeparationU
            ) {
                return false;
            }
        }

        // triggers too, the fruit's grab volume is one
        Collider[] hits = Physics.OverlapBox(
            position + rotation * webTriggerCenter,
            webTriggerHalfSize + Vector3.one * clearanceU,
            rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide
        );
        foreach (Collider hit in hits) {
            if (IsBranchOrFruit(hit)) {
                return false;
            }
        }
        return true;
    }

    // only branches and their fruit block a web. The web hugs the trunk, so
    // the trunk's own collider overlaps every candidate and must never count,
    // nor must anything else such as the monkey
    private static bool IsBranchOrFruit(Collider hit) {
        return hit.CompareTag(BRANCH_TAG)
            || hit.CompareTag(FRUIT_TAG)
            || hit.GetComponentInParent<BranchWithScriptRoot>() != null;
    }

    private static void DrawRing(Vector3 center, float radius) {
        Vector3 previous = center + Vector3.forward * radius;
        for (int i = 1; i <= GIZMO_RING_SEGMENTS; i++) {
            float angleRad = i * 2f * Mathf.PI / GIZMO_RING_SEGMENTS;
            Vector3 next =
                center
                + new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad))
                    * radius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
