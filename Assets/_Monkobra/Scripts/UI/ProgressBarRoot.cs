using UnityEngine;
using UnityEngine.UI;

// Todo progressbar add snake

/// <summary>
/// Drives a Canvas <see cref="Scrollbar"/> from the Player's y position.
/// </summary>
[RequireComponent(typeof(Scrollbar))]
public class ProgressBarRoot: MonoBehaviour {
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
                $"ProgressBarRoot:\tready, handle size {progressBar.size}");
        }
    }

    private void Update() {
        if (TreeManager.I == null) {
            if (!hasWarnedNoTreeManager) {
                Debug.LogWarning(
                    "ProgressBarRoot:\tno TreeManager in scene, "
                    + "retrying each frame", this);
                hasWarnedNoTreeManager = true;
            }
            return;
        }

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

        float progress = Mathf.InverseLerp(
            TreeManager.I.MinY, TreeManager.I.MaxY, player.position.y);
        progressBar.SetValueWithoutNotify(progress);

        // log per whole percent only, never per frame
        int percent = Mathf.RoundToInt(progress * 100f);
        if (percent != lastLoggedPercent) {
            lastLoggedPercent = percent;
        }
    }

    private const string PLAYER_TAG = "Player";

    private bool hasWarnedNoTreeManager;
    private bool hasWarnedNoPlayer;
    private int lastLoggedPercent = -1;

    // cached references  ------------------------------------------------------
    private Scrollbar progressBar;
    private Transform player;
}
