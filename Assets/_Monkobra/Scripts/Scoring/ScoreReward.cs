using UnityEngine;

/// <summary>One reusable reward definition per fruit or future score source.</summary>
[CreateAssetMenu(fileName = "ScoreReward", menuName = "Monkobra/Scoring/Score Reward")]
public class ScoreReward: ScriptableObject {
    public int Points => Mathf.Max(0, points);

    [SerializeField, Min(0)]
    [Tooltip("Points granted once per successful collection.")]
    private int points = 5;

    private void OnValidate() {
        points = Mathf.Max(0, points);
    }
}
