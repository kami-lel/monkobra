using System.Collections.Generic;
using UnityEngine;

/// <summary>Creates broad, avoidable rough-bark patches after the score
/// threshold. Segment ownership handles recycling and cleanup.</summary>
public class RoughBarkSpawner: MonoBehaviour {
    public bool IsUnlocked => unlocked;
    [SerializeField] private GameBalanceConfig balanceConfig;
    [SerializeField] private Material barkMaterial;
    [SerializeField, Min(0.1f)] private float radiusU = 5.05f;
    [SerializeField] private Vector2 patchHeightRangeU = new(5f, 8.5f);
    [SerializeField] private Vector2 patchArcRangeDeg = new(110f, 170f);
    [SerializeField, Min(0f)] private float patchSeparationU = 1f;
    [SerializeField, Min(0f)] private float spawnAheadU = 10f;
    [SerializeField, Min(1)] private int placementRetries = 16;
    [SerializeField, Min(0.1f)] private float obstacleClearanceU = 0.7f;
    private readonly HashSet<TreeSegmentController> segments = new();
    private readonly List<RoughBarkZone> patches = new();
    private ScoreManager score;
    private Transform player;
    private bool unlocked;
    private bool hasSpawned;

    private void Awake() {
        if (balanceConfig == null || barkMaterial == null) {
            Debug.LogError("RoughBarkSpawner: assign balanceConfig and barkMaterial", this);
            enabled = false;
        }
    }

    private void OnEnable() {
        TreeSegmentController.BranchesGenerated += OnBranchesGenerated;
    }

    private void Start() {
        score = ScoreManager.I;
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        player = target != null ? target.transform : null;
        if (score == null || player == null) {
            Debug.LogError("RoughBarkSpawner: missing score manager or player", this);
            enabled = false;
            return;
        }
        score.ScoreChanged += OnScoreChanged;
        foreach (TreeSegmentController segment in FindObjectsByType<TreeSegmentController>(FindObjectsSortMode.None)) {
            segments.Add(segment);
        }
        OnScoreChanged(score.TotalScore);
    }

    private void OnDisable() {
        TreeSegmentController.BranchesGenerated -= OnBranchesGenerated;
        if (score != null) score.ScoreChanged -= OnScoreChanged;
    }

    private void OnScoreChanged(long total) {
        if (unlocked || total < balanceConfig.RoughBarkStartScore) return;
        unlocked = true;
        // The pool was built while the score was low. Populate its existing
        // forward segments now, rather than waiting for a full recycle.
        List<TreeSegmentController> forward = new(segments);
        forward.RemoveAll(segment => segment == null || !segment.gameObject.activeInHierarchy);
        forward.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        foreach (TreeSegmentController segment in forward) PopulateSegment(segment);
    }

    private void OnBranchesGenerated(TreeSegmentController segment) {
        segments.Add(segment);
        if (unlocked) PopulateSegment(segment);
    }

    private void PopulateSegment(TreeSegmentController segment) {
        if (!unlocked || player == null || segment == null || !segment.gameObject.activeInHierarchy) return;
        patches.RemoveAll(patch => patch == null || !patch.gameObject.activeInHierarchy);
        RampedDifficultyManager difficulty = RampedDifficultyManager.I;
        if (difficulty == null) return;
        float chance = Mathf.Clamp01(balanceConfig.RoughBarkSpawnProbabilityCurve.Evaluate(
            difficulty.GetRampedValueFromYPosition(segment.transform.position.y)));
        Physics.SyncTransforms();
        for (int i = 0; i < balanceConfig.RoughBarkAttemptCount; i++) {
            // The first feasible forward patch appears near the threshold;
            // later patches retain the shared height-driven roll model.
            if (hasSpawned && Random.value >= chance) continue;
            float height = Random.Range(patchHeightRangeU.x, patchHeightRangeU.y);
            float arc = Random.Range(patchArcRangeDeg.x, patchArcRangeDeg.y);
            float extent = RoughBarkZone.GetVerticalExtent(height);
            float half = segment.GetHeight() * 0.5f;
            // The upper end belongs to this segment. A tall patch can
            // extend down across neighboring segments, while its owner
            // stays in the pool until the player has passed the whole area.
            float minY = Mathf.Max(segment.transform.position.y - half - extent,
                Mathf.Max(difficulty.RampBaseYPosition, player.position.y + spawnAheadU) + extent);
            float maxY = segment.transform.position.y + half - extent;
            if (maxY <= minY) continue;
            for (int retry = 0; retry < placementRetries; retry++) {
                Vector3 center = segment.transform.position;
                center.y = Random.Range(minY, maxY);
                float angle = Random.Range(0f, 360f);
                if (!IsClear(center, angle, extent, arc)) continue;
                GameObject patch = new("RoughBarkPatch");
                patch.transform.SetParent(segment.transform, false);
                patch.transform.position = center;
                RoughBarkZone zone = patch.AddComponent<RoughBarkZone>();
                zone.Initialize(radiusU, height, angle, arc, barkMaterial, true, Random.Range(0, 100000));
                patches.Add(zone);
                hasSpawned = true;
                break;
            }
        }
    }

    private bool IsClear(Vector3 center, float angle, float extent, float arc) {
        foreach (RoughBarkZone patch in patches) {
            // Disjoint vertical bounds prevent neighboring arcs joining
            // into a complete ring. Each arc leaves at least 190 dry degrees.
            if (patch != null && Mathf.Abs(patch.transform.position.y - center.y)
                < extent + RoughBarkZone.GetVerticalExtent(patch.HeightU) + patchSeparationU) return false;
        }
        int steps = Mathf.Max(1, Mathf.CeilToInt(radiusU * arc * Mathf.Deg2Rad / obstacleClearanceU));
        for (int i = 0; i <= steps; i++) {
            float sample = (angle + (i / (float)steps - 0.5f) * arc) * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Sin(sample), 0f, Mathf.Cos(sample)) * radiusU;
            foreach (Collider hit in Physics.OverlapCapsule(point - Vector3.up * extent,
                point + Vector3.up * extent, obstacleClearanceU, Physics.AllLayers, QueryTriggerInteraction.Collide)) {
                if (hit.GetComponentInParent<BranchController>() != null
                    || hit.GetComponentInParent<SpiderWebTrap>() != null
                    || hit.GetComponentInParent<FallingCobraController>() != null) return false;
            }
        }
        return true;
    }

    private void OnValidate() {
        patchHeightRangeU.x = Mathf.Max(0.5f, patchHeightRangeU.x);
        patchHeightRangeU.y = Mathf.Max(patchHeightRangeU.x, patchHeightRangeU.y);
        patchArcRangeDeg.x = Mathf.Clamp(patchArcRangeDeg.x, 10f, 170f);
        patchArcRangeDeg.y = Mathf.Clamp(patchArcRangeDeg.y, patchArcRangeDeg.x, 170f);
    }
}
