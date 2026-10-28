using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class ScoreWidget: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("score source")]
    private ScoreManager scoreManager;

    [SerializeField]
    [Tooltip("Text before the number, e.g. Score: or Final score:")]
    private string prefix = "Score: ";

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (scoreManager == null) {
            Debug.LogError(
                "ScoreWidget:\tmust assign Inspector Field: scoreManager",
                this);
        }
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable() {
        if (scoreManager == null) {
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

    // private members  ########################################################
    private TMP_Text label;

    // private methods  ########################################################
    private void Refresh(long score) {
        label.text = prefix + score.ToString("N0");
    }
}
