using UnityEngine;

/// <summary>
/// Maps player height to a ramped difficulty value, always in 0~1.
/// </summary>
public class RampedDifficultyService: MonoBehaviour {
    // Public Members
    public static RampedDifficultyService I { get; private set; }
    // player y at game start; world u
    public float PlayerGameStartYPosition { get; private set; }

    // Public Methods
    /// <summary>
    /// Samples the ramp curve at the height climbed since game start.
    /// </summary>
    /// <param name="y">player y position; world u</param>
    /// <returns>ramped difficulty, clamped to 0~1</returns>
    public float GetRampedValueFromYPosition(float y) {
        float climbedY = y - PlayerGameStartYPosition;
        return Mathf.Clamp01(rampCurve.Evaluate(climbedY));
    }

    /// <summary>
    /// Height the player has climbed since game start.
    /// </summary>
    /// <returns>current player y minus start y; 0 w/o player</returns>
    public float CalcPlayerClimbedYDistance() {
        if (player == null) {
            return 0f;
        }
        return player.position.y - PlayerGameStartYPosition;
    }

    // Inspector Fields
    [SerializeField]
    [Tooltip("x: player y above start y; y: ramped difficulty 0~1")]
    private AnimationCurve rampCurve = AnimationCurve.Linear(20f, 0f, 100f, 1f);

    // MonoBehaviour Lifecycle
    private void Awake() {
        if (I != null && I != this) {
            if (Debug.isDebugBuild) {
                Debug.Log("RampedDifficulty:\tduplicate removed");
            }
            Destroy(this);
            return;
        }
        I = this;

        GameObject playerObj = GameObject.FindGameObjectWithTag(PLAYER_TAG);
        if (playerObj == null) {
            Debug.LogError(
                "RampedDifficulty:\tfail to find GameObject tagged: "
                + PLAYER_TAG, this);
        } else {
            player = playerObj.transform;
        }
        if (Debug.isDebugBuild) {
            Debug.Log("RampedDifficulty:\tready");
        }
    }

    private void Start() {
        if (player == null) {
            return;
        }
        PlayerGameStartYPosition = player.position.y;
        if (Debug.isDebugBuild) {
            Debug.Log("RampedDifficulty:\tplayer start y: "
                + PlayerGameStartYPosition);
        }
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }

    // constants
    private const string PLAYER_TAG = "Player";

    // cached references
    private Transform player;
}
