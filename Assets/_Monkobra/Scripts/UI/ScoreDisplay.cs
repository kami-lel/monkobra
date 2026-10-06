using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class ScoreDisplay: MonoBehaviour {
    [SerializeField]
    private ScoreManager scoreManager;

    [SerializeField]
    [Tooltip("Text before the number, e.g. Score: or Final score:")]
    private string prefix = "Score: ";

    private TMP_Text label;

    private void Awake() {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable() {
        if (scoreManager == null) {
            Debug.LogError("ScoreDisplay: assign scoreManager in Inspector.", this);
            return;
        }
        scoreManager.ScoreChanged += Refresh;
        Refresh(scoreManager.TotalScore);
    }

    private void OnDisable() {
        if (scoreManager != null) {
            scoreManager.ScoreChanged -= Refresh;
        }
    }

    private void Refresh(long score) {
        label.text = prefix + score.ToString("N0");
    }
}
