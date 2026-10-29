using System.Collections.Generic;
using UnityEngine;

/// <summary>One guaranteed tutorial band and height-ramped partial patches.
/// Patches belong to their tree segments and disappear when those recycle.</summary>
public class SlipperyBarkSpawner: MonoBehaviour {
    [SerializeField] private GameBalanceConfig balanceConfig;
    [SerializeField] private Material barkMaterial;
    [SerializeField] private SlipperyBarkZone tutorialZone;
    [SerializeField, Min(0.01f)] private float radiusU = 5.05f;
    [SerializeField] private Vector2 patchHeightRangeU = new(1.2f, 4.2f);
    [SerializeField, Min(0.1f)] private float tutorialHeightU = 2.6f;
    [SerializeField] private Vector2 patchArcRangeDeg = new(30f, 125f);
    [SerializeField, Min(0f)] private float patchSeparationU = 4f;
    [SerializeField, Min(0f)] private float starterClearanceU = 4f;
    [SerializeField, Min(1)] private int placementRetries = 12;
    [SerializeField, Min(0.1f)] private float obstacleClearanceU = 0.7f;
    private readonly List<SlipperyBarkZone> patches = new();

    private void Awake() {
        if (balanceConfig == null || barkMaterial == null || tutorialZone == null) {
            Debug.LogError("SlipperyBarkSpawner: assign balanceConfig, "
                + "barkMaterial and tutorialZone", this);
            enabled = false;
            return;
        }
        tutorialZone.Initialize(radiusU, tutorialHeightU, 0f, 360f,
            barkMaterial, true);
    }

    private void OnEnable() {
        TreeSegmentController.BranchesGenerated += PopulateSegment;
    }

    private void OnDisable() {
        TreeSegmentController.BranchesGenerated -= PopulateSegment;
    }

    private void PopulateSegment(TreeSegmentController segment) {
        RampedDifficultyManager difficulty = RampedDifficultyManager.I;
        if (!isActiveAndEnabled || difficulty == null || segment == null) {
            return;
        }
        // Segment.Restart already removes runtime children. Forget their
        // references here as well, including any awaiting end-of-frame Destroy.
        patches.RemoveAll(patch => patch == null
            || !patch.gameObject.activeInHierarchy);
        float chance = Mathf.Clamp01(balanceConfig.SlipperySpawnProbabilityCurve
            .Evaluate(difficulty.GetRampedValueFromYPosition(segment.transform.position.y)));
        Physics.SyncTransforms();
        for (int i = 0; i < balanceConfig.SlipperyAttemptCount; i++) {
            if (Random.value >= chance) {
                continue;
            }
            float height = Random.Range(patchHeightRangeU.x, patchHeightRangeU.y);
            float arc = Random.Range(patchArcRangeDeg.x, patchArcRangeDeg.y);
            float half = segment.GetHeight() * 0.5f - height;
            float minY = Mathf.Max(segment.transform.position.y - half,
                difficulty.RampBaseYPosition + starterClearanceU + height);
            float maxY = segment.transform.position.y + half;
            if (maxY <= minY) continue;
            for (int attempt = 0; attempt < placementRetries; attempt++) {
                Vector3 center = segment.transform.position;
                center.y = Random.Range(minY, maxY);
                float angle = Random.Range(0f, 360f);
                if (!IsClear(center, angle, height, arc)) {
                    continue;
                }
                GameObject patch = new("SlipperyBarkPatch");
                patch.transform.SetParent(segment.transform, false);
                patch.transform.position = center;
                SlipperyBarkZone zone = patch.AddComponent<SlipperyBarkZone>();
                zone.Initialize(radiusU, height, angle,
                    arc, barkMaterial, true, Random.Range(0, 100000));
                patches.Add(zone);
                break;
            }
        }
    }

    private bool IsClear(Vector3 center, float angle, float height, float arc) {
        foreach (SlipperyBarkZone patch in patches) {
            // Full-height separation keeps different arcs from combining
            // into another unavoidable ring above the teaching band.
            if (patch != null && Mathf.Abs(patch.transform.position.y - center.y)
                < Mathf.Max(patchSeparationU, height + patch.HeightU)) {
                return false;
            }
        }
        int steps = Mathf.Max(1, Mathf.CeilToInt(
            radiusU * arc * Mathf.Deg2Rad / obstacleClearanceU));
        for (int i = 0; i <= steps; i++) {
            float sample = (angle + (i / (float)steps - 0.5f) * arc)
                * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Sin(sample), 0f,
                Mathf.Cos(sample)) * radiusU;
            Collider[] nearby = Physics.OverlapCapsule(point - Vector3.up * height,
                point + Vector3.up * height, obstacleClearanceU,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            foreach (Collider hit in nearby) {
                if (hit.GetComponentInParent<BranchController>() != null
                    || hit.GetComponentInParent<SpiderWebTrap>() != null
                    || hit.GetComponentInParent<FallingCobraController>() != null) {
                    return false;
                }
            }
        }
        return true;
    }

    private void OnValidate() {
        patchHeightRangeU.x = Mathf.Max(0.2f, patchHeightRangeU.x);
        patchHeightRangeU.y = Mathf.Max(patchHeightRangeU.x, patchHeightRangeU.y);
        patchArcRangeDeg.x = Mathf.Clamp(patchArcRangeDeg.x, 10f, 150f);
        patchArcRangeDeg.y = Mathf.Clamp(patchArcRangeDeg.y, patchArcRangeDeg.x, 150f);
    }
}
