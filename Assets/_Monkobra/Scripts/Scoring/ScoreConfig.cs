using UnityEngine;

/// <summary>Shared distance scoring rules; sampled once when a run starts.</summary>
[CreateAssetMenu(fileName = "ScoreConfig", menuName = "Monkobra/Scoring/Score Config")]
public class ScoreConfig: ScriptableObject {
    public int PointsPerMeter => Mathf.Max(0, pointsPerMeter);
    public float UnitsPerMeter => Mathf.Max(0.001f, unitsPerMeter);

    [SerializeField, Min(0)]
    private int pointsPerMeter = 1;

    [SerializeField, Min(0.001f)]
    private float unitsPerMeter = 1f;

    private void OnValidate() {
        pointsPerMeter = Mathf.Max(0, pointsPerMeter);
        unitsPerMeter = Mathf.Max(0.001f, unitsPerMeter);
    }
}
