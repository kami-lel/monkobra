using System.Collections.Generic;
using UnityEngine;

/// <summary>One guaranteed tutorial band, plus height-ramped partial patches.
/// Patches belong to their tree segments, vanish when those recycle</summary>
public class SlipperyBarkSpawner: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("References")]
    [SerializeField]
    [Tooltip("source of slippery spawn curve & attempt count")]
    private GameBalanceConfig balanceConfig;

    [SerializeField]
    [Tooltip("wet look of every patch & the tutorial band")]
    private Material barkMaterial;

    [SerializeField]
    [Tooltip("scene band at trunk base; always present, full ring")]
    private SlipperyBarkZone tutorialZone;

    [Header("Trunk")]
    [SerializeField, Min(0.01f)]
    [Tooltip("trunk surface radius; u")]
    private float radiusU = 5.05f;

    [SerializeField, Min(0.1f)]
    [Tooltip("tutorial band height; u")]
    private float tutorialHeightU = 2.6f;

    [Header("Patches")]
    [SerializeField]
    [Tooltip("random patch height, min~max; u")]
    private Vector2 patchHeightRangeU = new(1.2f, 4.2f);

    [SerializeField]
    [Tooltip("random patch arc, min~max; deg")]
    private Vector2 patchArcRangeDeg = new(30f, 125f);

    [SerializeField, Min(0f)]
    [Tooltip("min vertical gap b/t patches; u")]
    private float patchSeparationU = 4f;

    [SerializeField, Min(0f)]
    [Tooltip("no patch w/i this height above ramp base; u")]
    private float starterClearanceU = 4f;

    [SerializeField, Min(1)]
    [Tooltip("placement tries per spawn attempt")]
    private int placementRetries = 12;

    [SerializeField, Min(0.1f)]
    [Tooltip("min gap b/t patch & branch, web, cobra; u")]
    private float obstacleClearanceU = 0.7f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        bool hasMissingField = false;
        if (balanceConfig == null) {
            Debug.LogError("SlipperyBark:\tmust assign Inspector Field: "
                + "balanceConfig", this);
            hasMissingField = true;
        }
        if (barkMaterial == null) {
            Debug.LogError("SlipperyBark:\tmust assign Inspector Field: "
                + "barkMaterial", this);
            hasMissingField = true;
        }
        if (tutorialZone == null) {
            Debug.LogError("SlipperyBark:\tmust assign Inspector Field: "
                + "tutorialZone", this);
            hasMissingField = true;
        }
        if (hasMissingField) {
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

    private void OnValidate() {
        patchHeightRangeU.x = Mathf.Max(0.2f, patchHeightRangeU.x);
        patchHeightRangeU.y = Mathf.Max(
            patchHeightRangeU.x, patchHeightRangeU.y);
        patchArcRangeDeg.x = Mathf.Clamp(patchArcRangeDeg.x, 10f, 150f);
        patchArcRangeDeg.y = Mathf.Clamp(
            patchArcRangeDeg.y, patchArcRangeDeg.x, 150f);
    }

    // Event Handlers  #########################################################
    private void PopulateSegment(TreeSegmentController segment) {
        RampedDifficultyManager difficulty = RampedDifficultyManager.I;
        if (!isActiveAndEnabled || difficulty == null || segment == null) {
            return;
        }
        // Segment.Restart rm runtime children already, so drop their refs
        // too, incl any awaiting end-of-frame Destroy
        patches.RemoveAll(patch => patch == null
            || !patch.gameObject.activeInHierarchy);
        float segmentY = segment.transform.position.y;
        float chance = Mathf.Clamp01(
            balanceConfig.SlipperySpawnProbabilityCurve.Evaluate(
                difficulty.GetRampedValueFromYPosition(segmentY)));
        Physics.SyncTransforms();
        for (int i = 0; i < balanceConfig.SlipperyAttemptCount; i++) {
            if (Random.value >= chance) {
                continue;
            }
            float height = Random.Range(
                patchHeightRangeU.x, patchHeightRangeU.y);
            float arc = Random.Range(patchArcRangeDeg.x, patchArcRangeDeg.y);
            float half = segment.GetHeight() * 0.5f - height;
            float minY = Mathf.Max(segmentY - half,
                difficulty.RampBaseYPosition + starterClearanceU + height);
            float maxY = segmentY + half;
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

    // private members  ########################################################
    private readonly List<SlipperyBarkZone> patches = new();

    // private methods  ########################################################
    private bool IsClear(Vector3 center, float angle, float height, float arc) {
        foreach (SlipperyBarkZone patch in patches) {
            // full-height gap keeps arcs from merging into another
            // unavoidable ring above the tutorial band
            if (patch != null
                && Mathf.Abs(patch.transform.position.y - center.y)
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
            Collider[] nearby = Physics.OverlapCapsule(
                point - Vector3.up * height, point + Vector3.up * height,
                obstacleClearanceU, Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            foreach (Collider hit in nearby) {
                if (hit.GetComponentInParent<BranchController>() != null
                    || hit.GetComponentInParent<SpiderWebTrap>() != null
                    || hit.GetComponentInParent<FallingCobraController>()
                        != null) {
                    return false;
                }
            }
        }
        return true;
    }
}
