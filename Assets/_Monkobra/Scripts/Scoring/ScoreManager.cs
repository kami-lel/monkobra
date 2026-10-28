using System;
using UnityEngine;
//记录高度分和奖励分，在分数变化时通知 UI，并在游戏结束后停止计分
public class ScoreManager: MonoBehaviour {
    // Public Members  #########################################################
    public static ScoreManager I { get; private set; }
    public long TotalScore => DistanceScore + RewardScore;
    public long DistanceScore { get; private set; }
    public long RewardScore { get; private set; }
    public int BananaCollectedCount { get; private set; }
    public double HighestClimbedMeters => highestClimbedUnits / unitsPerMeter;
    public bool IsRunEnded => isRunEnded;
    public event Action<long> ScoreChanged;
    // raised once per banana pickup that scored
    public event Action BananaCollected;

    //必须满足：组件启用、已经开局、尚未结束、没有暂停，才允许计分
    public bool CanScore => isActiveAndEnabled && hasStarted && !isRunEnded && Time.timeScale > 0f && (GameController.I == null || GameController.I.IsRunLive);

    // Public Methods  #########################################################
    //添加水果等奖励分
    public bool TryAddReward() {
        if (!CanScore) {
            return false;
        }

        long oldScore = TotalScore;
        RewardScore += Math.Min((long)balanceConfig.ScoreRewardPoints, long.MaxValue - TotalScore);
        // every reward is a banana pickup
        BananaCollectedCount++;
        NotifyIfChanged(oldScore);
        BananaCollected?.Invoke();
        return true;
    }

    public void EndRun() {
        if (!hasStarted || isRunEnded) {
            return;
        }
        isRunEnded = true;
        SampleHeight();
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("scoring rates & reward points")]
    private GameBalanceConfig balanceConfig;

    [SerializeField]
    [Tooltip("monkey; height source")]
    private Transform player;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (balanceConfig == null) {
            Debug.LogError(
                "ScoreManager:\tmust assign Inspector Field: balanceConfig",
                this);
        }
        if (player == null) {
            Debug.LogError(
                "ScoreManager:\tmust assign Inspector Field: player", this);
        }
        if (I != null && I != this) {
            enabled = false;
            Destroy(this);
            return;
        }
        if (balanceConfig == null || player == null) {
            enabled = false;
            return;
        }
        I = this;
    }

    //读取计分设置，记录出生高度，把分数和最高爬升距离清零
    private void Start() {
        pointsPerMeter = balanceConfig.PointsPerMeter;
        unitsPerMeter = balanceConfig.UnitsPerMeter;
        highestClimbedUnits = 0d;
        DistanceScore = 0;
        RewardScore = 0;
        BananaCollectedCount = 0;
        isRunEnded = GameController.I != null
            && GameController.I.State == GameState.Lost;
        hasStarted = true;
        ScoreChanged?.Invoke(TotalScore);
    }

    private void FixedUpdate() {
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

    // private members  ########################################################
    private bool hasStarted;
    private bool isRunEnded;
    private double highestClimbedUnits;
    private double unitsPerMeter = 1d;
    private int pointsPerMeter;

    // private methods  ########################################################
    private void SampleHeight() {
        if (RampedDifficultyService.I == null) {
            return;
        }
        double climbedUnits =
            RampedDifficultyService.I.CurrentPlayerClimbedYDistance;
        //如果数值无效，或者没超过之前的最高高度，就直接退出。不能刷分
        if (double.IsNaN(climbedUnits) || double.IsInfinity(climbedUnits)
            || climbedUnits <= highestClimbedUnits) {
            return;
        }

        long oldScore = TotalScore;
        highestClimbedUnits = climbedUnits;
        double points = Math.Floor(HighestClimbedMeters) * pointsPerMeter;
        //高度分 = 最高爬升米数向下取整 × 每米分值
        long available = long.MaxValue - RewardScore;
        DistanceScore = points >= available ? available : (long)points;
        NotifyIfChanged(oldScore);
    }

    private void NotifyIfChanged(long oldScore) {
        if (TotalScore != oldScore) {
            ScoreChanged?.Invoke(TotalScore);
        }//通知 UI
    }
}
