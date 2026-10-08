using TMPro;
using UnityEngine;

/// <summary>
/// Fills the lose screen's final height, final score, and banana collected
/// labels each time the screen is enabled. Attach to the lose screen panel.
/// <para>
/// Height is the highest point reached, read from
/// <see cref="RampedDifficultyService.HighestPlayerClimbedYDistance"/>, shown
/// as whole units. Score is <see cref="ScoreManager.TotalScore"/>, bananas
/// <see cref="ScoreManager.BananaCollectedCount"/>.
/// </para>
/// </summary>
public class LoseScreenController: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("label for the final height")]
    private TMP_Text finalHeightText;

    [SerializeField]
    [Tooltip("label for the final score")]
    private TMP_Text finalScoreText;

    [SerializeField]
    [Tooltip("label for the banana collected count")]
    private TMP_Text bananaCollectedText;

    [SerializeField]
    [Tooltip("text before the final height number")]
    private string heightPrefix = "Final Height: ";

    [SerializeField]
    [Tooltip("text before the final score number")]
    private string scorePrefix = "Final Score: ";

    [SerializeField]
    [Tooltip("text before the banana collected number")]
    private string bananaPrefix = "Banana Collected: ";

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (finalHeightText == null) {
            Debug.LogError(
                "LoseScreenController:\tmust assign Inspector Field: "
                    + "Final Height Text",
                this
            );
        }
        if (finalScoreText == null) {
            Debug.LogError(
                "LoseScreenController:\tmust assign Inspector Field: "
                    + "Final Score Text",
                this
            );
        }
        if (bananaCollectedText == null) {
            Debug.LogError(
                "LoseScreenController:\tmust assign Inspector Field: "
                    + "Banana Collected Text",
                this
            );
        }
    }

    private void OnEnable() {
        RefreshFinalHeight();
        RefreshFinalScore();
        RefreshBananaCollected();
    }

    // private methods  ########################################################
    private void RefreshFinalHeight() {
        if (finalHeightText == null) {
            return;
        }
        if (RampedDifficultyService.I == null) {
            Debug.LogWarning(
                "LoseScreenController:\tRampedDifficultyService not found",
                this
            );
            return;
        }

        int heightU = Mathf.Max(
            0,
            Mathf.FloorToInt(
                RampedDifficultyService.I.HighestPlayerClimbedYDistance
            )
        );
        finalHeightText.text = heightPrefix + heightU.ToString("N0");

        if (Debug.isDebugBuild) {
            Debug.Log($"LoseScreenController:\tfinal height {heightU}");
        }
    }

    private void RefreshFinalScore() {
        if (finalScoreText == null) {
            return;
        }
        if (ScoreManager.I == null) {
            Debug.LogWarning(
                "LoseScreenController:\tScoreManager not found",
                this
            );
            return;
        }

        long score = ScoreManager.I.TotalScore;
        finalScoreText.text = scorePrefix + score.ToString("N0");

        if (Debug.isDebugBuild) {
            Debug.Log($"LoseScreenController:\tfinal score {score}");
        }
    }

    private void RefreshBananaCollected() {
        if (bananaCollectedText == null) {
            return;
        }
        if (ScoreManager.I == null) {
            Debug.LogWarning(
                "LoseScreenController:\tScoreManager not found",
                this
            );
            return;
        }

        int count = ScoreManager.I.BananaCollectedCount;
        bananaCollectedText.text = bananaPrefix + count.ToString("N0");

        if (Debug.isDebugBuild) {
            Debug.Log($"LoseScreenController:\tbanana collected {count}");
        }
    }
}
