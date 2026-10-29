using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

public class GameController: MonoBehaviour {
    // Public Members  #########################################################
    public static GameController I {
        get; private set;
    }

    public GameState State {
        get; private set;
    }

    public event Action<GameState> StateChanged;

    /// <summary>
    /// if the run is live, ie climbing and scoring work, whether or not the
    /// monkey is still protected
    /// </summary>
    public bool IsRunLive =>
        State == GameState.Playing || State == GameState.StartingZone;

    /// <summary>if the starting zone is under way</summary>
    public bool IsInStartingZone => State == GameState.StartingZone;

    /// <summary>
    /// whether cobra collisions are ignored entirely, a debug cheat; warns
    /// each time it is switched on
    /// </summary>
    public bool CobraNoClip {
        get => cobraNoClip;
        set {
            cobraNoClip = value;
            WarnIfCobraNoClip();
        }
    }

    // Public Methods  #########################################################
    public void EndStartingZone() {
        if (State == GameState.StartingZone) {
            SetState(GameState.Playing);
        }
    }

    public void LoseGame() {
        if (!IsRunLive) {
            return;
        }

        // settle score first
        if (ScoreManager.I != null) {
            ScoreManager.I.EndRun();
        }

        SetState(GameState.Lost);
        if (Debug.isDebugBuild) {
            Debug.Log("GameController:\tgame lost", this);
        }
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("shared input tuning, its interact action restarts the scene on "
        + "the lose screen")]
    private MonkeyConfig monkeyConfig;

    [SerializeField]
    [Min(0f)]
    [Tooltip("unscaled seconds the lose screen ignores restart input")]
    private float restartInputDelay = 0.5f;

    [SerializeField]
    [Tooltip("debug cheat: cobra collisions never kill the monkey")]
    private bool cobraNoClip;

    [SerializeField]
    [Tooltip("dev/hack keys, its no-clip key works in a development build "
        + "only")]
    private HackShortcutConfig hackShortcuts;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (monkeyConfig == null) {
            Debug.LogError(
                "GameController:\tmust assign Inspector Field: Monkey Config",
                this
            );
        } else if (monkeyConfig.InteractAction == null) {
            Debug.LogError(
                "GameController:\tmust assign MonkeyConfig field: "
                    + "interactAction",
                monkeyConfig
            );
        }

        if (I != null && I != this) {
            Debug.LogWarning(
                "GameController:\tduplicate instance destroyed",
                this
            );

            Destroy(gameObject);
            return;
        }

        I = this;
        WarnIfCobraNoClip();
        SetState(GameState.Tutorial);
        anyPressSub = InputSystem.onAnyButtonPress.CallOnce(OnAnyPressed);
    }

    private void Update() {
        UpdateNoClipKey();

        if (State != GameState.Lost || InteractAction == null) {
            return;
        }

        if (Time.unscaledTime - lostTime < restartInputDelay) {
            return;
        }

        if (InteractAction.action.WasPressedThisFrame()) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnDestroy() {
        anyPressSub?.Dispose();

        if (I == this) {
            I = null;
            Time.timeScale = 1f;
        }
    }

    // Event Handlers  #########################################################
    private void OnAnyPressed(InputControl _) {
        anyPressSub = null;
        if (State == GameState.Tutorial) {
            SetState(GameState.StartingZone);
        }
    }

    // private methods  ########################################################
    // dev/hack key toggling the no-clip cheat
    private void UpdateNoClipKey() {
        if (hackShortcuts == null || !HackShortcutConfig.IsActive) {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Key noClipKey = hackShortcuts.GetKey(HackAction.NoClip);
        if (
            keyboard == null
            || noClipKey == Key.None
            || !keyboard[noClipKey].wasPressedThisFrame
        ) {
            return;
        }

        CobraNoClip = !CobraNoClip;
        if (Debug.isDebugBuild) {
            Debug.Log("GameController:\tcobra no clip " + CobraNoClip, this);
        }
    }

    private void WarnIfCobraNoClip() {
        if (cobraNoClip) {
            Debug.LogWarning(
                "GameController:\tCobra No Clip is set, cobra collisions "
                    + "cannot kill the monkey",
                this
            );
        }
    }

    private void SetState(GameState next) {
        State = next;
        Time.timeScale = next == GameState.Playing
            || next == GameState.StartingZone ? 1f : 0f;

        if (next == GameState.Lost) {
            lostTime = Time.unscaledTime;
            // a web trap may have paused interact; restart needs it live
            if (InteractAction != null) {
                InteractAction.action.Enable();
            }
        }

        StateChanged?.Invoke(next);
    }

    // private members  ########################################################
    private IDisposable anyPressSub;
    private float lostTime;

    private InputActionReference InteractAction =>
        monkeyConfig != null ? monkeyConfig.InteractAction : null;
}
