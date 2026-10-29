using UnityEngine;

/// <summary>
/// Sits on a trigger collider GO. When the monkey enters it, the starting zone
/// ends and the run moves on to <see cref="GameState.Playing"/>.
/// <para>
/// The monkey is recognised by the <c>Player</c> tag on its root collider; the
/// untagged arms do not trip it. The collider must be a trigger.
/// </para>
/// </summary>
[RequireComponent(typeof(Collider))]
public class StartingZoneExitRelay: MonoBehaviour {
    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        Collider zone = GetComponent<Collider>();
        if (!zone.isTrigger) {
            Debug.LogWarning(
                "StartingZoneExitRelay:\tCollider must be a trigger", this);
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag(PLAYER_TAG)) {
            return;
        }
        if (GameController.I == null) {
            Debug.LogError(
                "StartingZoneExitRelay:\tfail to get Singleton: GameController",
                this);
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log("StartingZoneExitRelay:\tmonkey entered, end zone");
        }
        GameController.I.EndStartingZone();
    }

    // constants  ##############################################################
    private const string PLAYER_TAG = "Player";
}
