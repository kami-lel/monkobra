using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes playtest analytics to a markdown file per session. A session is one
/// game launch: the file opens with an h1 timestamp, and every game over
/// appends one run entry. Scene restarts keep the same session, and the
/// <see cref="restartSessionKey"/> forces a fresh file mid-launch.
/// <para>
/// Attach to a root GameObject of the first scene. It is kept across scene
/// reloads, and the copy each reload brings in destroys itself. Files land in
/// <c>Application.persistentDataPath/Analytics/</c>.
/// </para>
/// </summary>
public class AnalyticsManager: MonoBehaviour {
    // Public Members  #########################################################
    public static AnalyticsManager I {
        get; private set;
    }

    /// <summary>
    /// full path of the current session file, empty until one opens
    /// </summary>
    public string SessionFilePath {
        get; private set;
    } = string.Empty;

    /// <summary>
    /// runs recorded in the current session
    /// </summary>
    public int RunCount {
        get; private set;
    }

    // Public Methods  #########################################################
    /// <summary>
    /// closes the current session and opens a new file, run numbering restarts
    /// </summary>
    public void StartNewSession() {
        RunCount = 0;

        DateTime now = DateTime.Now;
        string stamp = now.ToString(
            "yyyy-MM-dd_HH_mm_ss",
            CultureInfo.InvariantCulture
        );
        string dir = Path.Combine(Application.persistentDataPath, FOLDER_NAME);

        try {
            Directory.CreateDirectory(dir);
            SessionFilePath = Path.Combine(dir, "0" + stamp + ".md");
            string title = now.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            );
            File.WriteAllText(
                SessionFilePath,
                "# MonKobra Analytics: Session: " + title + "\n\n",
                new UTF8Encoding(false)
            );
        } catch (Exception e) {
            SessionFilePath = string.Empty;
            Debug.LogWarning(
                "Analytics:\tfail to open session file: " + e,
                this
            );
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log("Analytics:\tsession opened " + SessionFilePath);
        }
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("forces a new session file")]
    private Key restartSessionKey = Key.F9;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        if (I != null && I != this) {
            Debug.LogWarning("Analytics:\tduplicate instance destroyed", this);
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartNewSession();
        ObserveScene();
    }

    private void Update() {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) {
            return;
        }

        if (keyboard[restartSessionKey].wasPressedThisFrame) {
            Debug.LogWarning(
                "Analytics:\trestart session key pressed, closing session "
                    + SessionFilePath,
                this
            );
            StartNewSession();
        }
    }

    private void OnDestroy() {
        if (I != this) {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Observe(null, null, null);
        I = null;
    }

    // Event Handlers  #########################################################
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        // each reload builds fresh instances of every observed subject
        ObserveScene();
    }

    private void OnGameStateChanged(GameState state) {
        if (state == GameState.Playing) {
            runStartTime = Time.time;
            runStartClock = DateTime.Now;
            bananaClocks.Clear();
            webClocks.Clear();
        } else if (state == GameState.Lost) {
            AppendRun();
        }
    }

    private void OnBananaCollected() {
        bananaClocks.Add(DateTime.Now);
    }

    private void OnWebTrapped(bool _) {
        webClocks.Add(DateTime.Now);
    }

    // constants  ##############################################################
    private const string FOLDER_NAME = "Analytics";
    private const string CLOCK_FORMAT = "HH:mm:ss";

    // private members  ########################################################
    private float runStartTime;
    private DateTime runStartClock;
    private readonly List<DateTime> bananaClocks = new List<DateTime>();
    private readonly List<DateTime> webClocks = new List<DateTime>();

    // cached references  ------------------------------------------------------
    private GameController observedGame;
    private ScoreManager observedScore;
    private SpiderWebStruggle observedWeb;

    // private methods  ########################################################
    private void ObserveScene() {
        Observe(
            GameController.I,
            ScoreManager.I,
            FindFirstObjectByType<SpiderWebStruggle>()
        );
    }

    private void Observe(
        GameController game,
        ScoreManager score,
        SpiderWebStruggle web
    ) {
        if (observedGame != null) {
            observedGame.StateChanged -= OnGameStateChanged;
        }
        if (observedScore != null) {
            observedScore.BananaCollected -= OnBananaCollected;
        }
        if (observedWeb != null) {
            observedWeb.Trapped -= OnWebTrapped;
        }

        observedGame = game;
        observedScore = score;
        observedWeb = web;

        if (observedGame != null) {
            observedGame.StateChanged += OnGameStateChanged;
        }
        if (observedScore != null) {
            observedScore.BananaCollected += OnBananaCollected;
        }
        if (observedWeb != null) {
            observedWeb.Trapped += OnWebTrapped;
        }
    }

    private static string FormatClock(DateTime clock) {
        return clock.ToString(CLOCK_FORMAT, CultureInfo.InvariantCulture);
    }

    // "unit * count = total" when every banana paid the same, else total only
    private static string FormatBananaScore(long total, int count) {
        if (count > 0 && total % count == 0) {
            return (total / count) + " * " + count + " = " + total;
        }

        return total.ToString(CultureInfo.InvariantCulture);
    }

    private static void AppendClockList(
        StringBuilder sb,
        List<DateTime> clocks
    ) {
        for (int i = 0; i < clocks.Count; i++) {
            sb.Append(i + 1).Append(". ").Append(FormatClock(clocks[i]));
            sb.Append('\n');
        }
        if (clocks.Count > 0) {
            sb.Append('\n');
        }
    }

    private void AppendRun() {
        if (string.IsNullOrEmpty(SessionFilePath)) {
            return;
        }

        RunCount++;
        float duration = Time.time - runStartTime;
        int height = RampedDifficultyService.I != null
            ? Mathf.Max(
                0,
                Mathf.FloorToInt(
                    RampedDifficultyService.I.HighestPlayerClimbedYDistance
                )
            )
            : 0;
        ScoreManager scoreMgr = ScoreManager.I;
        long score = scoreMgr != null ? scoreMgr.TotalScore : 0;
        long heightScore = scoreMgr != null ? scoreMgr.DistanceScore : 0;
        long rewardScore = scoreMgr != null ? scoreMgr.RewardScore : 0;

        var sb = new StringBuilder();
        sb.Append("## Run ").Append(RunCount).Append("\n\n");
        sb.Append("- Final Height: ").Append(height).Append("\n\n");

        sb.Append("#### Run Duration: ");
        sb.Append(duration.ToString("F1", CultureInfo.InvariantCulture));
        sb.Append(" s\n\n");
        sb.Append("- Time Start: ").Append(FormatClock(runStartClock));
        sb.Append('\n');
        sb.Append("- Time Stop: ").Append(FormatClock(DateTime.Now));
        sb.Append("\n\n");

        sb.Append("#### Final Score: ").Append(score).Append("\n\n");
        sb.Append("- Score from Height: ").Append(heightScore);
        sb.Append('\n');
        sb.Append("- Score from Banana: ");
        sb.Append(FormatBananaScore(rewardScore, bananaClocks.Count));
        sb.Append("\n\n");

        sb.Append("#### Banana Count: ").Append(bananaClocks.Count);
        sb.Append("\n\n");
        AppendClockList(sb, bananaClocks);

        sb.Append("#### Web Encounter: ").Append(webClocks.Count);
        sb.Append("\n\n");
        AppendClockList(sb, webClocks);

        try {
            File.AppendAllText(
                SessionFilePath,
                sb.ToString(),
                new UTF8Encoding(false)
            );
        } catch (Exception e) {
            Debug.LogWarning("Analytics:\tfail to append run: " + e, this);
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log("Analytics:\trun " + RunCount + " appended");
        }
    }
}
