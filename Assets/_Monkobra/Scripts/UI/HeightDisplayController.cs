using TMPro;
using UnityEngine;

/// <summary>
/// Shows the height the player has climbed since game start on a
/// <see cref="TMP_Text"/> label, read from
/// <see cref="RampedDifficultyService.CurrentPlayerClimbedYDistance"/>.
/// Rewrites the text only when the whole-unit value changes.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class HeightDisplayController: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("text before the number, e.g. Height: ")]
    private string prefix = "Height: ";

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        label = GetComponent<TMP_Text>();
        if (label == null) {
            Debug.LogError(
                "HeightDisplayController:\tfail to get Component: TMP_Text",
                this
            );
        }
    }

    private void OnEnable() {
        // force a first write
        shownHeightU = int.MinValue;
    }

    private void LateUpdate() {
        if (label == null || RampedDifficultyService.I == null) {
            return;
        }

        float climbedYU =
            RampedDifficultyService.I.CurrentPlayerClimbedYDistance;
        if (float.IsNaN(climbedYU) || float.IsInfinity(climbedYU)) {
            return;
        }

        int heightU = Mathf.Max(0, Mathf.FloorToInt(climbedYU));
        if (heightU == shownHeightU) {
            return;
        }
        shownHeightU = heightU;
        label.text = prefix + heightU.ToString("N0");
    }

    // private members  ########################################################
    private int shownHeightU; // last whole-unit height written to the label

    // cached references  ------------------------------------------------------
    private TMP_Text label;
}
