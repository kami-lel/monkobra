using UnityEngine;

public class WinZoneHandler: MonoBehaviour {
    // Event Handlers  #########################################################
    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(PLAYER_TAG)) {
            return;
        }

        if (GameController.I != null) {
            GameController.I.WinGame();
        } else {
            Debug.LogWarning(
                "WinZoneHandler:\tGameController instance not found",
                this
            );
        }
    }

    // Constants  ###############################################################
    private const string PLAYER_TAG = "Player";
}
