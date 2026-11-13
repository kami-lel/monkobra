using UnityEngine;

/// <summary>
/// Maps player height to a ramped difficulty value: 0~1 from the starting
/// zone exit to the boss fight, above 1 after it.
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
    /// World y where the ramp sits at 0: the y of the starting zone exit
    /// trigger, falling back to <see cref="PlayerGameStartYPosition"/> while
    /// none is assigned. Climbed height for score and display is unaffected.
    /// </summary>
    public float RampBaseYPosition =>
        rampBase != null ? rampBase.position.y : PlayerGameStartYPosition;

    /// <returns>the player's transform, null if none was found</returns>
    public Transform PlayerTransform => player;

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

    /// <param name="y">any y position; world u</param>
    /// <returns>if <paramref name="y"/> lies below the starting zone exit,
    /// ie in the starting zone</returns>
    public bool IsInStartingZoneY(float y) {
        return y < RampBaseYPosition;
    }

    /// <param name="y">any y position; world u</param>
    /// <returns>if <paramref name="y"/> lies in the boss zone, from its start
    /// to its end in climbed height, where only the boss fight generates
    /// anything</returns>
    public bool IsInBossZoneY(float y) {
        // the endless tree has no boss zone, so no hazard-free gap
        if (worldBalanceConfig == null || IsEndless) {
            return false;
        }
        float climbedY = GetClimbedYDistance(y);
        bool isInCobraZone = climbedY >= worldBalanceConfig.BossZoneStartU
            && climbedY <= worldBalanceConfig.BossZoneEndU;
        // the Spider King's zone higher up is kept just as clear
        bool isInSpiderZone = climbedY >= worldBalanceConfig.SpiderZoneStartU
            && climbedY <= worldBalanceConfig.SpiderZoneEndU;
        return isInCobraZone || isInSpiderZone;
    }

    /// <summary>
    /// Maps a height to the ramped difficulty: 0 up to the starting zone exit,
    /// the pre boss curve from there to the boss zone start (0~1), a flat 1
    /// inside the boss zone, the post boss curve from the zone end (1 up).
    /// Zone heights are climbed heights, measured from the player's start y.
    /// </summary>
    /// <param name="y">any y position; world u</param>
    /// <returns>ramped difficulty, never below 0; above 1 past the boss
    /// zone</returns>
    public float GetRampedValueFromYPosition(float y) {
        if (worldBalanceConfig == null) {
            return 0f;
        }
        float climbedY = GetClimbedYDistance(y);
        float exitClimbedY = GetClimbedYDistance(RampBaseYPosition);
        if (IsEndless) {
            // endless opens at the post boss level at the exit and keeps
            // ramping from there
            return climbedY < exitClimbedY ? 0f : GetPostBossValue(
                climbedY - exitClimbedY + worldBalanceConfig.BossZoneEndU
            );
        }
        float zoneStartU = worldBalanceConfig.BossZoneStartU;
        if (climbedY < exitClimbedY || zoneStartU <= exitClimbedY) {
            // a zone start at or under the exit leaves no pre boss stretch
            return climbedY < zoneStartU ? 0f : GetPostBossValue(climbedY);
        }
        if (climbedY < zoneStartU) {
            float progress =
                (climbedY - exitClimbedY) / (zoneStartU - exitClimbedY);
            return Mathf.Max(
                0f,
                worldBalanceConfig.PreBossRampCurve.Evaluate(progress)
            );
        }
        return GetPostBossValue(climbedY);
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("world balance tuning; ramp curve")]
    private WorldBalanceConfig worldBalanceConfig;

    [SerializeField]
    [Tooltip("starting zone exit trigger; its y is where the ramp starts at 0")]
    private Transform rampBase;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (worldBalanceConfig == null) {
            Debug.LogError(
                "RampedDifficulty:\tmust assign Inspector Field: "
                + "worldBalanceConfig", this);
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
        if (
            worldBalanceConfig != null
            && worldBalanceConfig.BossZoneStartU
                <= GetClimbedYDistance(RampBaseYPosition)
        ) {
            Debug.LogWarning(
                "RampedDifficulty:\tboss zone starts at or below the starting "
                + "zone exit, the pre boss ramp is skipped", this);
        }
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

    // private methods  ########################################################
    // if this scene plays the endless run
    private bool IsEndless =>
        GameController.I != null && GameController.I.Mode == RunMode.Endless;

    // 1 through the boss zone, the post boss curve past its end
    private float GetPostBossValue(float climbedY) {
        float zoneEndU = worldBalanceConfig.BossZoneEndU;
        if (climbedY <= zoneEndU) {
            return 1f;
        }
        return Mathf.Max(
            1f,
            worldBalanceConfig.PostBossRampCurve.Evaluate(climbedY - zoneEndU)
        );
    }
}
