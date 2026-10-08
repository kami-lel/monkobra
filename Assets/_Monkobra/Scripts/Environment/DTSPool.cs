using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless tree from a fixed pool of <see cref="DynamicTreeSegmentPrefabRoot"/>
/// segments. Put it on an empty parent, with the pooled segments as its
/// children, stacked end to end.
/// <para>
/// The pool holds <c>segmentCount</c> segments and never makes more. Each
/// time the player climbs past the top of the lowest one, only that one is
/// carried over the top of the stack and restarted there, so its branches
/// (and webs, through <see cref="SpiderWebSpawner"/>) are rolled again for
/// the new height. The pool thus climbs with the player forever.
/// </para>
/// </summary>
public class DTSPool: MonoBehaviour {
    // Inspector Fields  #######################################################
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

    [SerializeField]
    [Tooltip(
        "segments the pool keeps, no more; the lowest ones under this object "
            + "are used and the rest deactivated"
    )]
    private int segmentCount = 4;

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

        List<DynamicTreeSegmentPrefabRoot> found = new(
            GetComponentsInChildren<DynamicTreeSegmentPrefabRoot>()
        );
        if (found.Count == 0) {
            Debug.LogWarning(
                "DTSPool:\tno DynamicTreeSegmentPrefabRoot under this "
                    + "object, nothing to pool",
                this
            );
            enabled = false;
            return;
        }

        // lowest first, so the queue front is always the next to recycle
        found.Sort((a, b) => a.transform.position.y.CompareTo(
            b.transform.position.y
        ));
        if (found.Count < segmentCount) {
            Debug.LogWarning(
                $"DTSPool:\tsegmentCount {segmentCount} but only "
                    + $"{found.Count} segments under this object, using all",
                this
            );
        }

        // only the lowest segmentCount join the pool, the rest are dropped
        for (int i = 0; i < found.Count; i++) {
            if (i < segmentCount) {
                segments.Enqueue(found[i]);
                newestSegment = found[i];
            } else {
                found[i].gameObject.SetActive(false);
            }
        }
    }

    private void Start() {
        // found once, then cached for every frame
        GameObject player = GameObject.FindGameObjectWithTag(PLAYER_TAG);
        if (player != null) {
            target = player.transform;
        }
        if (target == null) {
            Debug.LogWarning(
                $"DTSPool:\tno object tagged {PLAYER_TAG} in scene, pool idle",
                this
            );
            enabled = false;
            return;
        }

        if (webSpawner == null) {
            webSpawner = FindFirstObjectByType<SpiderWebSpawner>();
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
    }

    // constants  ##############################################################
    private const string PLAYER_TAG = "Player";

    // private members  ########################################################
    private Transform target; // cached player, set in Start

    // FIFO of pooled segments: lowest at the front, highest at the back
    private readonly Queue<DynamicTreeSegmentPrefabRoot> segments = new();
    private DynamicTreeSegmentPrefabRoot newestSegment; // back of the queue

    // private methods  ########################################################
    /// <returns>if the player is above the lowest segment, which then moved
    /// to the top</returns>
    private bool TryRecycleLowest() {
        // FIFO: the oldest entry is the lowest, the newest the highest
        DynamicTreeSegmentPrefabRoot lowest = segments.Peek();
        DynamicTreeSegmentPrefabRoot highest = newestSegment;

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
