using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sticks <see cref="SpiderWebTrap"/> webs flat on the trunk of every
/// <see cref="DynamicTreeSegmentPrefabRoot"/> below this object, from
/// <c>startProgress</c> of the climb upward. Put it on the tree root.
/// <para>
/// A web lies on its segment's own stretch of bark facing outward: the
/// prefab's strands sit in its local YZ plane, so local +X, the face normal,
/// is turned away from the trunk axis. A candidate spot is dropped if it is
/// below the start height, too close to another web, or its trigger volume
/// overlaps any branch or fruit. Out of retries, the web is skipped rather
/// than placed overlapping.
/// </para>
/// <para>
/// Every segment is placed in the scene, not generated during the climb, so
/// one pass at load covers the tree. The pass runs after the default
/// execution order, once each segment has spawned its branches in its own
/// Start. A segment created later at runtime is decorated by calling
/// <see cref="PopulateSegment"/> once its branches are in.
/// </para>
/// </summary>
[DefaultExecutionOrder(100)]
public class SpiderWebSpawner: MonoBehaviour {
    // Public Methods  #########################################################
    /// <summary>
    /// Rolls this spawner's web slots for <paramref name="segment"/>, which
    /// must already hold its branches.
    /// </summary>
    public void PopulateSegment(DynamicTreeSegmentPrefabRoot segment) {
        if (segment == null || prefabTrigger == null) {
            return;
        }
        if (TreeManager.I == null) {
            Debug.LogWarning(
                "SpiderWebSpawner:\tno TreeManager in scene, skip webs",
                this
            );
            return;
        }

        // branches were posed through their transforms this frame, push the
        // poses into physics so the overlap test can see them
        Physics.SyncTransforms();

        int spawnedCount = 0;
        for (int slot = 0; slot < maxWebsPerSegment; slot++) {
            if (Random.value < spawnChance && TrySpawnWeb(segment)) {
                spawnedCount++;
            }
        }

        if (Debug.isDebugBuild && spawnedCount > 0) {
            Debug.Log(
                $"SpiderWebSpawner:\tspawned {spawnedCount} web(s) on "
                    + segment.name,
                segment
            );
        }
    }

    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip(
        "web prefab, its trigger BoxCollider is the volume kept clear of "
            + "branches and fruit"
    )]
    private SpiderWebTrap webPrefab;

    [Header("Web Count")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip(
        "climb progress below which no web spawns; 0~1. Set 0 to test webs "
            + "from the tree base"
    )]
    private float startProgress = 0.5f;

    [SerializeField]
    [Tooltip("web slots rolled per segment")]
    private int maxWebsPerSegment = 2;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("chance each slot gets a web; 0~1")]
    private float spawnChance = 0.5f;

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

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (webPrefab == null) {
            Debug.LogWarning(
                "SpiderWebSpawner:\tmust assign Inspector Field: webPrefab",
                this
            );
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

    // default-order Starts have run by now, so every segment has branches
    private void Start() {
        foreach (
            DynamicTreeSegmentPrefabRoot segment
            in GetComponentsInChildren<DynamicTreeSegmentPrefabRoot>()
        ) {
            PopulateSegment(segment);
        }
    }

    // trigger volume of every spawned web, plus a ring at the start height,
    // visible in the Scene view, and in the Game view with Gizmos on
    private void OnDrawGizmos() {
        if (prefabTrigger == null) {
            return;
        }

        Gizmos.color = GIZMO_WEB_COLOR;
        foreach (Transform web in spawnedWebs) {
            if (web == null) {
                continue;
            }
            Gizmos.matrix = web.localToWorldMatrix;
            Gizmos.DrawWireCube(prefabTrigger.center, prefabTrigger.size);
        }
        Gizmos.matrix = Matrix4x4.identity;

        if (TreeManager.I != null) {
            float startY = Mathf.Lerp(
                TreeManager.I.MinY,
                TreeManager.I.MaxY,
                startProgress
            );
            Gizmos.color = GIZMO_START_COLOR;
            DrawRing(
                new Vector3(transform.position.x, startY, transform.position.z),
                trunkRadiusU + GIZMO_RING_PADDING_U
            );
        }
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxWebsPerSegment = Mathf.Max(0, maxWebsPerSegment);
        trunkRadiusU = Mathf.Max(0f, trunkRadiusU);
        surfaceOffsetU = Mathf.Max(0f, surfaceOffsetU);
        minWebSeparationU = Mathf.Max(0f, minWebSeparationU);
        clearanceU = Mathf.Max(0f, clearanceU);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
    }

    // constants  ##############################################################
    private const string BRANCH_TAG = "Branch";
    private const string FRUIT_TAG = "FruitCollider";
    private const int GIZMO_RING_SEGMENTS = 48;
    private const float GIZMO_RING_PADDING_U = 1f; // ring sits off the bark
    private static readonly Color GIZMO_WEB_COLOR = Color.yellow;
    private static readonly Color GIZMO_START_COLOR = Color.cyan;

    // private members  ########################################################
    private Vector3 webTriggerCenter; // web-local, prefab scale applied
    private Vector3 webTriggerHalfSize; // web-local, prefab scale applied
    private readonly List<Transform> spawnedWebs = new();

    // cached references  ------------------------------------------------------
    private BoxCollider prefabTrigger;

    // private methods  ########################################################
    private bool TrySpawnWeb(DynamicTreeSegmentPrefabRoot segment) {
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
            if (!IsSpotClear(position, rotation)) {
                continue;
            }

            SpiderWebTrap web = Instantiate(
                webPrefab,
                position,
                rotation,
                segmentXfm
            );
            spawnedWebs.Add(web.transform);
            return true;
        }

        return false;
    }

    private bool IsSpotClear(Vector3 position, Quaternion rotation) {
        float progress = Mathf.InverseLerp(
            TreeManager.I.MinY,
            TreeManager.I.MaxY,
            position.y
        );
        if (progress < startProgress) {
            return false;
        }

        foreach (Transform web in spawnedWebs) {
            if (
                web != null
                && Vector3.Distance(web.position, position) < minWebSeparationU
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
