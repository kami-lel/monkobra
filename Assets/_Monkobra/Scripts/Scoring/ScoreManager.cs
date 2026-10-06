using System;
using UnityEngine;

/// <summary>
/// Scene-scoped run score. Only new maximum height earns distance points;
/// falling and re-climbing cannot farm points. Reload the scene for a new run.
/// </summary>
public class ScoreManager: MonoBehaviour {
    public static ScoreManager I { get; private set; }
    public long TotalScore => DistanceScore + RewardScore;
    public long DistanceScore { get; private set; }
    public long RewardScore { get; private set; }
    public double HighestClimbedMeters => highestClimbedUnits / unitsPerMeter;
    public bool IsRunEnded => isRunEnded;
    public event Action<long> ScoreChanged;

    public bool CanScore => isActiveAndEnabled && hasStarted && !isRunEnded
        && Time.timeScale > 0f
        && (GameController.I == null || !GameController.I.IsGameOver);

    [Header("Wiring")]
    [SerializeField]
    private ScoreConfig config;

    [SerializeField]
    [Tooltip("Monkey root, not a hand, the camera, or the moving tree.")]
    private Transform player;

    /// <summary>
    /// Future reward sources can call this after validating their own event.
    /// The caller owns deduplication; fruit uses ScorePickup for this.
    /// </summary>
    public bool TryAddReward(ScoreReward reward) {
        if (!CanScore || reward == null) {
            return false;
        }

        long oldScore = TotalScore;
        RewardScore += Math.Min((long)reward.Points, long.MaxValue - TotalScore);
        NotifyIfChanged(oldScore);
        return true;
    }

    /// <summary>Capture the final physical height, then freeze scoring.</summary>
    public void EndRun() {
        if (!hasStarted || isRunEnded) {
            return;
        }
        // Lock before publishing so event listeners cannot add a late reward.
        isRunEnded = true;
        SampleHeight();
    }

    /// <summary>
    /// If an endless-world system later shifts the PLAYER by a world Y delta,
    /// call this with that same delta in the same operation, before scoring.
    /// Example: player/world move down 1000 units => pass -1000.
    /// Do not call it when only recycling tree segments.
    /// </summary>
    public void ApplyWorldOriginShift(float worldDeltaY) {
        if (hasStarted && !float.IsNaN(worldDeltaY)
            && !float.IsInfinity(worldDeltaY)) {
            startY += worldDeltaY;
        }
    }

    private void Awake() {
        if (I != null && I != this) {
            enabled = false;
            Destroy(this);
            return;
        }
        if (config == null || player == null) {
            Debug.LogError("ScoreManager: assign config and player in Inspector.", this);
            enabled = false;
            return;
        }
        I = this;
        playerBody = player.GetComponent<Rigidbody>();
    }

    private void Start() {
        // All scene Awake methods have run; the player's spawn pose is ready.
        pointsPerMeter = config.PointsPerMeter;
        unitsPerMeter = config.UnitsPerMeter;
        startY = ReadPlayerY();
        highestClimbedUnits = 0d;
        DistanceScore = 0;
        RewardScore = 0;
        isRunEnded = GameController.I != null && GameController.I.IsGameOver;
        hasStarted = true;
        ScoreChanged?.Invoke(TotalScore);
    }

    private void FixedUpdate() {
        // Observe each completed physics step, including frames with several
        // physics steps. Rigidbody.position avoids render interpolation lag.
        if (CanScore) {
            SampleHeight();
        }
    }

    private void LateUpdate() {
        if (CanScore) {
            SampleHeight();
        }
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }

    private bool hasStarted;
    private bool isRunEnded;
    private Rigidbody playerBody;
    private double startY;
    private double highestClimbedUnits;
    private double unitsPerMeter = 1d;
    private int pointsPerMeter;

    private float ReadPlayerY() {
        return playerBody != null ? playerBody.position.y : player.position.y;
    }

    private void SampleHeight() {
        if (player == null) {
            return;
        }
        double climbedUnits = (double)ReadPlayerY() - startY;
        if (double.IsNaN(climbedUnits) || double.IsInfinity(climbedUnits)
            || climbedUnits <= highestClimbedUnits) {
            return;
        }

        long oldScore = TotalScore;
        highestClimbedUnits = climbedUnits;
        double points = Math.Floor(HighestClimbedMeters) * pointsPerMeter;
        long available = long.MaxValue - RewardScore;
        DistanceScore = points >= available ? available : (long)points;
        NotifyIfChanged(oldScore);
    }

    private void NotifyIfChanged(long oldScore) {
        if (TotalScore != oldScore) {
            ScoreChanged?.Invoke(TotalScore);
        }
    }
}
