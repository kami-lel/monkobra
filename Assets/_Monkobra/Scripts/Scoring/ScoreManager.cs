using System;
using UnityEngine;
//记录高度分和奖励分，在分数变化时通知 UI，并在游戏结束后停止计分
public class ScoreManager: MonoBehaviour {
    public static ScoreManager I { get; private set; }
    public long TotalScore => DistanceScore + RewardScore;
    public long DistanceScore { get; private set; }
    public long RewardScore { get; private set; }
    public double HighestClimbedMeters => highestClimbedUnits / unitsPerMeter;
    public bool IsRunEnded => isRunEnded;
    public event Action<long> ScoreChanged;

    public bool CanScore => isActiveAndEnabled && hasStarted && !isRunEnded && Time.timeScale > 0f && (GameController.I == null || GameController.I.State == GameState.Playing);
    //必须满足：组件启用、已经开局、尚未结束、没有暂停，才允许计分
    [SerializeField]
    private ScoreConfig config;

    [SerializeField]
    private Transform player;
    //添加水果等奖励分
    public bool TryAddReward(ScoreReward reward) {
        if (!CanScore || reward == null) 
        {
            return false;
        }

        long oldScore = TotalScore;
        RewardScore += Math.Min((long)reward.Points, long.MaxValue - TotalScore);
        NotifyIfChanged(oldScore);
        return true;
    }

    public void EndRun() {
        if (!hasStarted || isRunEnded) 
        {
            return;
        }
        isRunEnded = true;
        SampleHeight();
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
    }

    private void Start() {
        pointsPerMeter = config.PointsPerMeter;
        unitsPerMeter = config.UnitsPerMeter;
        highestClimbedUnits = 0d;
        DistanceScore = 0;
        RewardScore = 0;
        isRunEnded = GameController.I != null
            && GameController.I.State == GameState.Lost;
        hasStarted = true;
        ScoreChanged?.Invoke(TotalScore);
    }
    //读取计分设置，记录出生高度，把分数和最高爬升距离清零
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

    private bool hasStarted;
    private bool isRunEnded;
    private double highestClimbedUnits;
    private double unitsPerMeter = 1d;
    private int pointsPerMeter;

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
