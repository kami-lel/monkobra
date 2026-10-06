using UnityEngine;

[CreateAssetMenu(fileName = "ScoreReward", menuName = "Monkobra/Scoring/Score Reward")]
public class ScoreReward: ScriptableObject {
    public int Points => Mathf.Max(0, points);

    [SerializeField, Min(0)]
    private int points = 5;

    private void OnValidate() {
        points = Mathf.Max(0, points);
    }
}
