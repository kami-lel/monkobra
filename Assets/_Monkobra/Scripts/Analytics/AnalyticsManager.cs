using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes playtest analytics to a markdown file per session. A session is one
/// game launch: the file opens with an h1 timestamp, and every game over
/// appends one run entry. Scene restarts keep the same session, and the
/// <see cref="HackAction.RestartSession"/> key forces a fresh file
/// mid-launch.
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
        get {
            return session.Runs.Count;
        }
    }

    // Public Methods  #########################################################
    /// <summary>
    /// closes the current session and opens a new file, run numbering restarts
    /// </summary>
    public void StartNewSession() {
        // a run still open belongs to the session being closed
        CloseRun(RunStatus.Interrupted);
        DateTime now = DateTime.Now;
        session = new SessionData {
            StartClock = now,
            Title = now.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture
            ),
            AppVersion = Application.version
        };
        currentRun = null;

        string stamp = now.ToString(
            "yyyy-MM-dd_HH_mm_ss",
            CultureInfo.InvariantCulture
        );
        string dir = Path.Combine(Application.persistentDataPath, FOLDER_NAME);

        try {
            Directory.CreateDirectory(dir);
        } catch (Exception e) {
            SessionFilePath = string.Empty;
            Debug.LogWarning(
                "Analytics:\tfail to open session file: " + e,
                this
            );
            return;
        }

        SessionFilePath = Path.Combine(dir, "0" + stamp + ".md");
        if (!SaveSession()) {
            SessionFilePath = string.Empty;
            return;
        }

        if (Debug.isDebugBuild) {
            Debug.Log("Analytics:\tsession opened " + SessionFilePath);
        }
    }

    /// <summary>
    /// saves a mock session file beside the real ones, the live session is
    /// untouched
    /// </summary>
    public void SaveMockSession() {
        string path = MockAnalyticsGenerator.SaveMockSession(
            Path.Combine(Application.persistentDataPath, FOLDER_NAME),
            DateTime.Now,
            config.DeathCauseWindowS
        );
        if (string.IsNullOrEmpty(path)) {
            Debug.LogWarning("Analytics:\tfail to save mock session", this);
        } else if (Debug.isDebugBuild) {
            Debug.Log("Analytics:\tmock session saved " + path, this);
        }
    }

    /// <summary>
    /// hands the session report to the browser as a download; elsewhere it
    /// saves the session file instead. A run still open is rendered as it
    /// stands and keeps recording.
    /// </summary>
    public void DownloadReport() {
        string text = AnalyticsMarkdownWriter.BuildSession(session);
        string filename = "0" + session.StartClock.ToString(
            "yyyy-MM-dd_HH_mm_ss",
            CultureInfo.InvariantCulture
        ) + ".md";
        if (WebDownload.SaveText(filename, text)) {
            return;
        }

        if (SaveSession()) {
            if (Debug.isDebugBuild) {
                Debug.Log("Analytics:\treport saved " + SessionFilePath, this);
            }
        } else {
            Debug.LogWarning("Analytics:\tfail to save report", this);
        }
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("shared analytics tuning")]
    private AnalyticsConfig config;

    [SerializeField]
    [Tooltip("dev/hack keys, they only work in a development build")]
    private HackShortcutConfig hackShortcuts;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (config == null) {
            Debug.LogWarning(
                "Analytics:\tmust assign Inspector Field: config, "
                + "using defaults", this);
        }
        if (hackShortcuts == null) {
            Debug.LogWarning(
                "Analytics:\tmust assign Inspector Field: hackShortcuts",
                this);
        }

        if (I != null && I != this) {
            Debug.LogWarning("Analytics:\tduplicate instance destroyed", this);
            Destroy(gameObject);
            return;
        }

        I = this;
        if (config == null) {
            config = ScriptableObject.CreateInstance<AnalyticsConfig>();
        }
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        // static event, webs spawn at runtime so none can be looked up here
        SpiderWebTrap.Encountered += OnWebEncountered;
        StartNewSession();
        ObserveScene();
    }

    private void Update() {
        if (
            currentRun != null
            && observedGame != null
            && observedGame.State == GameState.Playing
        ) {
            if (Time.time >= nextSampleTime) {
                AddSample();
                nextSampleTime = Time.time + config.SampleIntervalS;
            }
            if (
                config.AutosaveIntervalS > 0f
                && Time.time >= nextAutosaveTime
            ) {
                SaveSession();
            }
        }

        UpdateHackKeys();
    }

    // covers Play mode stop in the Editor
    private void OnApplicationQuit() {
        CloseRun(RunStatus.Interrupted);
    }

    private void OnApplicationPause(bool isPaused) {
        if (isPaused) {
            CloseRun(RunStatus.Interrupted);
        }
    }

    private void OnDestroy() {
        if (I != this) {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SpiderWebTrap.Encountered -= OnWebEncountered;
        Observe(null, null, null);
        I = null;
    }

    // Private Helpers  ########################################################
    // dev/hack keys, off outside a development build
    private void UpdateHackKeys() {
        if (hackShortcuts == null || !HackShortcutConfig.IsActive) {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) {
            return;
        }

        Key restartKey = hackShortcuts.GetKey(HackAction.RestartSession);
        if (
            restartKey != Key.None
            && keyboard[restartKey].wasPressedThisFrame
        ) {
            Debug.LogWarning(
                "Analytics:\trestart session key pressed, closing session "
                    + SessionFilePath,
                this
            );
            StartNewSession();
        }

        Key downloadKey = hackShortcuts.GetKey(HackAction.DownloadReport);
        if (
            downloadKey != Key.None
            && keyboard[downloadKey].wasPressedThisFrame
        ) {
            DownloadReport();
        }

        Key mockKey = hackShortcuts.GetKey(HackAction.MockSession);
        if (mockKey != Key.None && keyboard[mockKey].wasPressedThisFrame) {
            SaveMockSession();
        }
    }

    // Event Handlers  #########################################################
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        // each reload builds fresh instances of every observed subject
        ObserveScene();
    }

    private void OnGameStateChanged(GameState state) {
        if (state == GameState.Playing) {
            // scene reloaded mid run, the old run never saw a game over
            CloseRun(RunStatus.Interrupted);
            currentRun = new RunData {
                StartTime = Time.time,
                StartClock = DateTime.Now
            };
            AddSample();
            nextSampleTime = Time.time + config.SampleIntervalS;
            session.Runs.Add(currentRun);
            SaveSession();
        } else if (state == GameState.Lost) {
            CloseRun(RunStatus.Lost);
        }
    }

    private void OnBananaCollected() {
        if (currentRun == null) {
            return;
        }

        currentRun.Bananas.Add(CreateStamp());
        SaveSession();
    }

    private void OnWebEncountered(SpiderWebTrap _) {
        if (currentRun != null) {
            currentRun.Webs.Add(CreateStamp());
            SaveSession();
        }
    }

    private void OnBranchHit(Collider _) {
        if (currentRun != null) {
            currentRun.Branches.Add(CreateStamp());
            SaveSession();
        }
    }

    // constants  ##############################################################
    private const string FOLDER_NAME = "Analytics";

    // private members  ########################################################
    private SessionData session = new SessionData();
    // run being played, null b/t a game over and the next start
    private RunData currentRun;
    private float nextSampleTime;
    private float nextAutosaveTime;

    // cached references  ------------------------------------------------------
    private GameController observedGame;
    private ScoreManager observedScore;
    private HitBranchDetection observedBranch;

    // private methods  ########################################################
    private void ObserveScene() {
        Observe(
            GameController.I,
            ScoreManager.I,
            FindFirstObjectByType<HitBranchDetection>()
        );
    }

    private void Observe(
        GameController game,
        ScoreManager score,
        HitBranchDetection branch
    ) {
        if (observedGame != null) {
            observedGame.StateChanged -= OnGameStateChanged;
        }
        if (observedScore != null) {
            observedScore.BananaCollected -= OnBananaCollected;
        }
        if (observedBranch != null) {
            observedBranch.BranchHit -= OnBranchHit;
        }

        observedGame = game;
        observedScore = score;
        observedBranch = branch;

        if (observedGame != null) {
            observedGame.StateChanged += OnGameStateChanged;
        }
        if (observedScore != null) {
            observedScore.BananaCollected += OnBananaCollected;
        }
        if (observedBranch != null) {
            observedBranch.BranchHit += OnBranchHit;
        }
    }

    // snapshot of the moment and the height climbed above the run start
    private static EventStamp CreateStamp() {
        RampedDifficultyService ramp = RampedDifficultyService.I;
        return new EventStamp(
            DateTime.Now,
            ramp != null ? ramp.CurrentPlayerClimbedYDistance : 0f
        );
    }

    // records height & the stamina bar now, stamina reads 0 w/o a bar in the
    // scene
    private void AddSample() {
        if (currentRun == null) {
            return;
        }

        StaminaBarController bar = StaminaBarController.I;
        currentRun.Samples.Add(new RunSample(
            Time.time - currentRun.StartTime,
            CreateStamp().Height,
            bar != null ? bar.CurrentStamina : 0f
        ));
    }

    // whether the whole session was written to its file; any save, event or
    // timed, restarts the timed save countdown
    private bool SaveSession() {
        nextAutosaveTime = Time.time + config.AutosaveIntervalS;
        if (string.IsNullOrEmpty(SessionFilePath)) {
            return false;
        }

        return AnalyticsMarkdownWriter.WriteFile(
            SessionFilePath,
            AnalyticsMarkdownWriter.BuildSession(session)
        );
    }

    // closes the open run w/ status & saves, the run joined the session at its
    // start; death cause is inferred only for a lost run
    private void CloseRun(RunStatus status) {
        if (string.IsNullOrEmpty(SessionFilePath) || currentRun == null) {
            return;
        }

        RunData run = currentRun;
        currentRun = null;

        // where & why the run ended, as opposed to the highest point reached
        run.End = CreateStamp();
        if (status == RunStatus.Lost) {
            run.Cause = DeathCauseResolver.Resolve(
                run,
                run.End.Clock,
                config.DeathCauseWindowS
            );
        }
        run.Status = status;
        run.DurationS = Time.time - run.StartTime;
        run.HighestHeight = RampedDifficultyService.I != null
            ? Mathf.Max(
                0f,
                RampedDifficultyService.I.HighestPlayerClimbedYDistance
            )
            : 0f;
        ScoreManager scoreMgr = ScoreManager.I;
        run.TotalScore = scoreMgr != null ? scoreMgr.TotalScore : 0;
        run.HeightScore = scoreMgr != null ? scoreMgr.DistanceScore : 0;
        run.RewardScore = scoreMgr != null ? scoreMgr.RewardScore : 0;

        // the end sample needs the run to stay current for a moment
        currentRun = run;
        AddSample();
        currentRun = null;

        if (SaveSession() && Debug.isDebugBuild) {
            Debug.Log("Analytics:\trun " + RunCount + " " + status);
        }
    }
}
