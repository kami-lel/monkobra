using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless tree from a fixed pool of <see cref="DynamicTreeSegmentPrefabRoot"/>
/// segments. Put it on an empty parent: it instantiates
/// <c>segmentCount</c> copies of <c>segmentPrefab</c> as its children,
/// stacked end to end upward, <c>segmentsBelowPlayer</c> of them under the
/// player's start height and the rest above. The stack never starts under
/// the pool's own height, so the ground holds.
/// <para>
/// The pool holds exactly <c>segmentCount</c> segments and never makes more.
/// Its own web pass covers the first stack, so keep it out from under a
/// <see cref="SpiderWebSpawner"/>, which would roll them a second time.
/// Whenever the stack top reaches less than <c>lookAheadU</c> above the
/// player, and the lowest segment lies wholly under the player, only that
/// lowest one is carried over the top and restarted there, so its branches
/// (and webs, through <see cref="SpiderWebSpawner"/>) are rolled again for
/// the new height. The pool thus climbs with the player forever.
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

    [Tooltip(
        "decorates a restarted segment with webs, dft the one in the scene; "
            + "empty for no webs"
    )]
    private SpiderWebSpawner webSpawner;

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
        "segments of the first stack placed under the player's start "
            + "height, the rest stand above; at most segmentCount - 1"
    )]
    private int segmentsBelowPlayer = 1;

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
        if (webSpawner == null) {
            webSpawner = FindFirstObjectByType<SpiderWebSpawner>();
        }

        // every segment has rolled its branches in its own Start by now
        if (webSpawner != null) {
            foreach (DynamicTreeSegmentPrefabRoot segment in segments) {
                webSpawner.PopulateSegment(segment);
            }
        }

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
        segmentsBelowPlayer = Mathf.Clamp(
            segmentsBelowPlayer,
            0,
            segmentCount - 1
        );
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
    // first. The stack base sits segmentsBelowPlayer segments under the
    // player, never under this object, so the stack cannot sink past the
    // ground
    private void BuildStack() {
        float baseY = float.NaN; // set once the first segment gives a height
        for (int i = 0; i < segmentCount; i++) {
            DynamicTreeSegmentPrefabRoot segment = Instantiate(
                segmentPrefab,
                transform
            );
            float height = segment.GetHeight();
            if (float.IsNaN(baseY)) {
                baseY = Mathf.Max(
                    transform.position.y,
                    target.position.y - segmentsBelowPlayer * height
                );
            }

            Vector3 position = transform.position;
            position.y = baseY + height * 0.5f;
            segment.transform.SetPositionAndRotation(
                position,
                transform.rotation
            );
            baseY += height;

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

        if (webSpawner != null) {
            webSpawner.PopulateSegment(lowest);
        }
        return true;
    }
}
