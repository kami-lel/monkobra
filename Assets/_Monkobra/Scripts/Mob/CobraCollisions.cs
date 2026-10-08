using UnityEngine;

/// <summary>
/// Sits on the cobra root beside its kinematic Rigidbody, so every body
/// segment's trigger collider, head to tail, reports here. Any part of the
/// monkey touching any part of the cobra, side-on or by falling onto the coil,
/// loses the game.
/// </summary>
public class CobraCollisions: MonoBehaviour {
    // MonoBehaviour Lifecycle  ################################################
    private void OnTriggerEnter(Collider other) {
        CatchIfMonkey(other);
    }

    // also while overlapping, in case the monkey was already inside the coil
    // when an overlap began
    private void OnTriggerStay(Collider other) {
        CatchIfMonkey(other);
    }

    // private methods  ########################################################
    private static void CatchIfMonkey(Collider other) {
        if (!IsMonkeyPart(other)) {
            return;
        }

        if (GameController.I == null) {
            Debug.LogWarning(
                "CobraCollisions:\tGameController instance not found"
            );
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"CobraCollisions:\tplayer caught via {other.name}, "
                    + "losing game"
            );
        }

        GameController.I.LoseGame();
    }

    /// <returns>true for the monkey's physical parts: body, tail and hands;
    /// false for its sensing volumes (fruit and branch detection), which are
    /// triggers reaching far past the body</returns>
    private static bool IsMonkeyPart(Collider other) {
        // a hand is a trigger, but still a real part of the monkey
        if (other.GetComponentInParent<ArmRoot>() != null) {
            return true;
        }

        if (other.isTrigger) {
            return false;
        }

        // body and tail sit anywhere under the Player-tagged root
        for (Transform t = other.transform; t != null; t = t.parent) {
            if (t.CompareTag("Player")) {
                return true;
            }
        }

        return false;
    }
}
