using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns a fixed pool of <see cref="TreeSegmentPrefabRoot"/> instances and
/// recycles them above/below the tracking Transform each frame, giving the
/// illusion of an endless tree with no runtime Instantiate/Destroy.
/// </summary>
public class TreeSegmentPoolManager: MonoBehaviour {
    // Public Members  #########################################################
    public static TreeSegmentPoolManager Instance { get; private set; }

    // Public Methods  #########################################################
    /// <summary>Adds a segment to the tracked pool.</summary>
    public void RegisterSegment(TreeSegmentPrefabRoot segment) {
        if (!activeSegments.Contains(segment)) {
            activeSegments.Add(segment);
        }
    }

    /// <summary>Removes a segment from the tracked pool.</summary>
    public void UnregisterSegment(TreeSegmentPrefabRoot segment) {
        activeSegments.Remove(segment);
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    private Transform trackingTransform;

    [SerializeField]
    private GameObject segmentPrefab;

    [SerializeField]
    private int poolCount = 5;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (trackingTransform == null) {
            Debug.LogWarning("must assign Inspector Field: Tracking Transform",
                              this);
        }
        if (segmentPrefab == null) {
            Debug.LogWarning("must assign Inspector Field: Segment Prefab",
                              this);
        }

        Instance = this;
    }

    private void Start() {
        SpawnPool();
    }

    private void Update() {
        RepositionSegments();
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    // private members  ########################################################
    private readonly List<TreeSegmentPrefabRoot> activeSegments = new();
    private float segmentHeight;

    // private methods  ########################################################
    private void SpawnPool() {
        for (int i = 0; i < poolCount; i++) {
            GameObject instance = Instantiate(segmentPrefab, transform);
            TreeSegmentPrefabRoot segment =
                instance.GetComponent<TreeSegmentPrefabRoot>();
            if (segment == null) {
                Debug.LogError(
                    "fail to get Component: TreeSegmentPrefabRoot", instance);
                continue;
            }
            if (i == 0) {
                segmentHeight = segment.GetHeight();
            }

            Vector3 spawnPosition = transform.position;
            spawnPosition.y =
                trackingTransform.position.y + i * segmentHeight;
            Quaternion spawnRotation =
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            instance.transform.SetPositionAndRotation(spawnPosition,
                                                        spawnRotation);
        }
    }

    private void RepositionSegments() {
        if (segmentHeight <= 0f || trackingTransform == null) {
            return;
        }

        float trackingY = trackingTransform.position.y;
        float poolSpan = segmentHeight * activeSegments.Count;

        foreach (var segment in activeSegments) {
            Transform segmentTransform = segment.transform;
            float offset = segmentTransform.position.y - trackingY;

            if (offset < -poolSpan / 2f) {
                segmentTransform.position += Vector3.up * poolSpan;
            } else if (offset > poolSpan / 2f) {
                segmentTransform.position -= Vector3.up * poolSpan;
            }
        }
    }
}
