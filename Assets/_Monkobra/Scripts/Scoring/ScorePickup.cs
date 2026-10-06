using UnityEngine;

/// <summary>
/// Attach to a collectible root and assign its reward. This is deliberately
/// not a trigger handler: ArmRoot calls it only after a successful grab.
/// </summary>
[DisallowMultipleComponent]
public class ScorePickup: MonoBehaviour {
    [SerializeField]
    private ScoreReward reward;

    public bool TryCollect() {
        if (!isActiveAndEnabled || isCollected || Time.timeScale <= 0f
            || (GameController.I != null && GameController.I.IsGameOver)) {
            return false;
        }
        if (reward == null) {
            Debug.LogError("ScorePickup: assign reward in Inspector.", this);
            return false;
        }

        ScoreManager manager = ScoreManager.I;
        if (manager != null && !manager.CanScore) {
            return false;
        }

        // Claim before raising score events; two hands/colliders get one award.
        isCollected = true;
        if (manager != null) {
            manager.TryAddReward(reward);
        } else {
            // Preserve fruit grabbing in legacy/test scenes without scoring.
            Debug.LogWarning("ScorePickup: no ScoreManager; collected without points.", this);
        }
        gameObject.SetActive(false);
        return true;
    }

    private bool isCollected;

    private void OnEnable() {
        // A pooled item becomes collectible again when its root is reactivated.
        isCollected = false;
    }
}
