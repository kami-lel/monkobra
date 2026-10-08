using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless tree from a fixed pool of <see cref="DynamicTreeSegmentPrefabRoot"/>
/// segments. Put it on an empty parent: it instantiates
/// <c>segmentCount</c> copies of <c>segmentPrefab</c> as its children,
/// stacked end to end upward, the first (lowest) one created at world
/// height <c>firstSegmentY</c>.
/// <para>
/// The pool holds exactly <c>segmentCount</c> segments and never makes more.
/// Whenever the stack top reaches less than <c>lookAheadU</c> above the
/// player, and the lowest segment lies wholly under the player, only that
/// lowest one is carried over the top and restarted there, so its branches
/// are rolled again for the new height. The pool thus climbs with the player
/// forever.
/// </para>
/// <para>
/// The pool knows nothing of webs: a <see cref="SpiderWebSpawner"/> hears
/// each segment's <see cref="DynamicTreeSegmentPrefabRoot.BranchesGenerated"/>
/// and redoes its webs then, so it must stay off the deactivated mock tree.
/// </para>
/// </summary>
// after every segment's own Start, which rolls its branches
[DefaultExecutionOrder(50)]
public class DTSPool: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip("segment prefab instantiated segmentCount times")]
    private DynamicTreeSegmentPrefabRoot segmentPrefab;

    [SerializeField]
    [Tooltip(
        "mock tree standing in the scene to show where the pool lies; "
            + "deactivated at runtime. Must not hold the pool"
    )]
    private GameObject mockTree;

    [Header("Initial Stack")]
    [SerializeField]
    [Tooltip(
        "segments the pool instantiates and keeps, no more"
    )]
    private int segmentCount = 4;

    [SerializeField]
    [Tooltip(
        "world y where the first (lowest) segment is created, its pivot; u. "
            + "Set it under the player's start so the stack reaches below"
    )]
    private float firstSegmentY = 0f;

    [Header("Recycling")]
    [SerializeField]
    [Tooltip(
        "y offset: a segment is added only when the stack top is less than "
            + "this above the player; u"
    )]
    private float lookAheadU = 20f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // the mock tree is an editor-only stand-in, gone before play begins
        if (mockTree != null) {
            if (transform.IsChildOf(mockTree.transform)) {
                Debug.LogWarning(
                    "DTSPool:\tmockTree holds the pool, keep it active",
                    this
                );
            } else {
                // a web spawner left on the stand-in would go dark with it
                if (
                    mockTree.GetComponentInChildren<SpiderWebSpawner>(true)
                    != null
                ) {
                    Debug.LogWarning(
                        "DTSPool:\ta SpiderWebSpawner under mockTree is "
                            + "deactivated with it, no webs will spawn; move "
                            + "it off the mock tree",
                        mockTree
                    );
                }
                mockTree.SetActive(false);
            }
        }

        // Inspector Assignment Guard  -----------------------------------------
        if (segmentPrefab == null) {
            Debug.LogWarning(
                "DTSPool:\tmust assign Inspector Field: segmentPrefab",
                this
            );
            enabled = false;
            return;
        }

        // found once, then cached for every frame; needed now, the first
        // stack is laid out around the player
        GameObject player = GameObject.FindGameObjectWithTag(PLAYER_TAG);
        if (player == null) {
            Debug.LogWarning(
                $"DTSPool:\tno object tagged {PLAYER_TAG} in scene, pool idle",
                this
            );
            enabled = false;
            return;
        }
        target = player.transform;

        BuildStack();
    }

    private void Start() {
        if (Debug.isDebugBuild) {
            Debug.Log(
                $"DTSPool:\tready, {segments.Count} segments, "
                    + $"following {target.name}"
            );
        }
    }

    // after the monkey has moved this frame
    private void LateUpdate() {
        // one lap at most, so a mis-sized pool can never spin forever
        for (int i = 0; i < segments.Count; i++) {
            if (!TryRecycleLowest()) {
                break;
            }
        }
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        segmentCount = Mathf.Max(1, segmentCount);
        lookAheadU = Mathf.Max(0f, lookAheadU);
    }

    // constants  ##############################################################
    private const string PLAYER_TAG = "Player";

    // private members  ########################################################
    private Transform target; // cached player, set in Start

    // FIFO of pooled segments: lowest at the front, highest at the back
    private readonly Queue<DynamicTreeSegmentPrefabRoot> segments = new();
    private DynamicTreeSegmentPrefabRoot newestSegment; // back of the queue

    // private methods  ########################################################
    // segments stand end to end, pivots mid-segment, and join the FIFO lowest
    // first, the lowest pivot sitting at firstSegmentY
    private void BuildStack() {
        for (int i = 0; i < segmentCount; i++) {
            DynamicTreeSegmentPrefabRoot segment = Instantiate(
                segmentPrefab,
                transform
            );

            Vector3 position = transform.position;
            if (newestSegment == null) {
                position.y = firstSegmentY;
            } else {
                // pivots sit mid-segment, so each rests half of itself and
                // half of the one under it on their joint
                position.y =
                    newestSegment.transform.position.y
                    + (newestSegment.GetHeight() + segment.GetHeight()) * 0.5f;
            }
            segment.transform.SetPositionAndRotation(
                position,
                transform.rotation
            );

            segments.Enqueue(segment);
            newestSegment = segment;
        }

        // the segments' Start reads collider bounds, so push the poses
        Physics.SyncTransforms();
    }

    /// <returns>if the stack top was lower than lookAheadU above the player
    /// and the lowest segment, wholly under the player, moved to the top
    /// </returns>
    private bool TryRecycleLowest() {
        // FIFO: the oldest entry is the lowest, the newest the highest
        DynamicTreeSegmentPrefabRoot lowest = segments.Peek();
        DynamicTreeSegmentPrefabRoot highest = newestSegment;

        // enough stack ahead of the player already, add nothing
        float highestTopY =
            highest.transform.position.y + highest.GetHeight() * 0.5f;
        if (highestTopY >= target.position.y + lookAheadU) {
            return false;
        }

        // never lift the segment the player stands on or has not passed
        float lowestTopY =
            lowest.transform.position.y + lowest.GetHeight() * 0.5f;
        if (lowestTopY >= target.position.y) {
            return false;
        }

        // pivots sit mid-segment, so the new one rests half of each on the
        // joint b/t them
        Vector3 newPosition = lowest.transform.position;
        newPosition.y =
            highest.transform.position.y
            + (highest.GetHeight() + lowest.GetHeight()) * 0.5f;

        segments.Dequeue();
        lowest.Restart(newPosition);
        segments.Enqueue(lowest);
        newestSegment = lowest;
        return true;
    }
}
