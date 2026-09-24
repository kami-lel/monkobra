using UnityEngine;
using UnityEngine.UI;

// Todo progressbar add snake

/// <summary>
/// Drives a Canvas <see cref="Scrollbar"/> from the Player's y position.
/// </summary>
[RequireComponent(typeof(Scrollbar))]
public class ProgressBarRoot: MonoBehaviour {
    [Header("Progress Source")]
    [SerializeField]
    [Tooltip("Player y at 0% progress; u")]
    private float minY = 0f;

    [SerializeField]
    [Tooltip("Player y at 100% progress; u")]
    private float maxY = 100f;

    private void Awake() {
        progressBar = GetComponent<Scrollbar>();
        if (progressBar == null) {
            Debug.LogError(
                "ProgressBarRoot:\tfail to get Component: Scrollbar", this);
            enabled = false;
            return;
        }
        if (Debug.isDebugBuild) {
            Debug.Log(
                $"ProgressBarRoot:\tready, range y {minY}~{maxY}, "
                + $"handle size {progressBar.size}");
        }
    }

    private void Update() {
        // Player may spawn after this component, so retry until found
        if (player == null) {
            GameObject playerGO = GameObject.FindGameObjectWithTag(PLAYER_TAG);
            if (playerGO == null) {
                if (!hasWarnedNoPlayer) {
                    Debug.LogWarning(
                        "ProgressBarRoot:\tno GameObject tagged "
                        + $"'{PLAYER_TAG}' found, retrying each frame", this);
                    hasWarnedNoPlayer = true;
                }
                return;
            }
            player = playerGO.transform;
            if (Debug.isDebugBuild) {
                Debug.Log(
                    $"ProgressBarRoot:\tfound player '{playerGO.name}' "
                    + $"at y {player.position.y}");
            }
        }

        float progress = Mathf.InverseLerp(minY, maxY, player.position.y);
        progressBar.SetValueWithoutNotify(progress);

        // log per whole percent only, never per frame
        int percent = Mathf.RoundToInt(progress * 100f);
        if (percent != lastLoggedPercent) {
            lastLoggedPercent = percent;
            if (Debug.isDebugBuild) {
                Debug.Log(
                    $"ProgressBarRoot:\tprogress {percent}% "
                    + $"(player y {player.position.y})");
            }
        }
    }

    private const string PLAYER_TAG = "Player";

    private bool hasWarnedNoPlayer;
    private int lastLoggedPercent = -1;

    // cached references  ------------------------------------------------------
    private Scrollbar progressBar;
    private Transform player;
}
