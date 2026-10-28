using UnityEngine;

/// <summary>
/// Maps player height to a ramped difficulty value, always in 0~1.
/// </summary>
// run first so other LateUpdate readers see this frame's height
[DefaultExecutionOrder(-100)]
public class RampedDifficultyManager: MonoBehaviour {
    // Public Members  #########################################################
    public static RampedDifficultyManager I { get; private set; }
    // player y at game start; world u
    public float PlayerGameStartYPosition { get; private set; }
    // player y above start y, refreshed every frame; world u
    public float CurrentPlayerClimbedYDistance { get; private set; }
    // highest CurrentPlayerClimbedYDistance so far, never below 0; world u
    public float HighestPlayerClimbedYDistance { get; private set; }

    /// <summary>
    /// World y where the ramp sits at 0: the y of the starter zone exit
    /// trigger, falling back to <see cref="PlayerGameStartYPosition"/> while
    /// none is assigned. Climbed height for score and display is unaffected.
    /// </summary>
    public float RampBaseYPosition =>
        rampBase != null ? rampBase.position.y : PlayerGameStartYPosition;

    // Public Methods  #########################################################
    /// <summary>
    /// Height of <paramref name="y"/> above the player's game start y, the
    /// one baseline every climb height is measured from. A future world
    /// origin shift only has to move <see cref="PlayerGameStartYPosition"/>
    /// along with the world.
    /// </summary>
    /// <param name="y">any y position; world u</param>
    /// <returns>climbed height, negative below the start; world u</returns>
    public float GetClimbedYDistance(float y) {
        return y - PlayerGameStartYPosition;
    }

    /// <summary>
    /// Samples the ramp curve at the height above
    /// <see cref="RampBaseYPosition"/>.
    /// </summary>
    /// <param name="y">any y position; world u</param>
    /// <returns>ramped difficulty, clamped to 0~1</returns>
    public float GetRampedValueFromYPosition(float y) {
        if (balanceConfig == null) {
            return 0f;
        }
        float rampedY = y - RampBaseYPosition;
        return Mathf.Clamp01(
            balanceConfig.RampedDifficultyCurve.Evaluate(rampedY)
        );
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("game balance tuning; ramp curve")]
    private GameBalanceConfig balanceConfig;

    [SerializeField]
    [Tooltip("starter zone exit trigger; its y is where the ramp starts at 0")]
    private Transform rampBase;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (balanceConfig == null) {
            Debug.LogError(
                "RampedDifficulty:\tmust assign Inspector Field: "
                + "balanceConfig", this);
        }
        if (rampBase == null) {
            Debug.LogWarning(
                "RampedDifficulty:\tmust assign Inspector Field: rampBase, "
                + "ramp starts at player start y", this);
        }

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

    private void LateUpdate() {
        if (player == null) {
            return;
        }
        CurrentPlayerClimbedYDistance = GetClimbedYDistance(player.position.y);
        HighestPlayerClimbedYDistance = Mathf.Max(
            HighestPlayerClimbedYDistance,
            CurrentPlayerClimbedYDistance
        );
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }

    // constants  ##############################################################
    private const string PLAYER_TAG = "Player";

    // cached references  ######################################################
    private Transform player;
}
