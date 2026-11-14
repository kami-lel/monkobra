using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The shot director. The climb camera is the default view; an event shot
/// (coconut track, later cobra alert and leap) takes over for a moment and
/// hands back. Each shot has a priority, a minimum hold, a hard duration cap
/// and a cooldown, all in <see cref="CameraConfig"/>, so events never chain
/// into rapid cuts: a shot of the same or a lower priority than the live one
/// is refused, a higher one preempts it, and a shot that just ended waits out
/// its cooldown. Swap is done by raising the live camera's
/// <see cref="CinemachineCamera.Priority"/>, the <c>CinemachineBrain</c> on
/// the main camera blends, with the per-pair times of its blender settings.
/// <para>
/// Reads no input: the grab framing lives in
/// <see cref="CameraClimbFraming"/>, so nothing here touches the shared
/// interact action.
/// </para>
/// </summary>
public class CameraRigManager: MonoBehaviour {
    // Public Members  #########################################################
    /// <summary>
    /// the event shots the director can cut to
    /// </summary>
    public enum ShotKind {
        Coconut,
        CobraAlert,
        Leap,
    }

    // Public Methods  #########################################################
    /// <summary>
    /// Asks for an event shot. Extends the shot if it is already live
    /// </summary>
    /// <param name="kind">which shot</param>
    /// <param name="holdS">how long to stay, capped by the shot's duration;
    /// infinite until <see cref="ReleaseShot"/>; s</param>
    /// <returns>if the shot is live now</returns>
    public bool RequestShot(ShotKind kind, float holdS = float.PositiveInfinity) {
        CameraConfig.ShotSettings settings = GetSettings(kind);
        if (config == null || settings == null || GetCamera(kind) == null) {
            return false;
        }

        float now = Time.time;
        ShotState state = states[(int)kind];
        if (state.isLive) {
            state.endTimeS = now + holdS;
            return true;
        }
        if (now < state.cooldownEndS) {
            return false;
        }
        if (liveKind.HasValue) {
            int livePriority = GetSettings(liveKind.Value).Priority;
            if (livePriority >= settings.Priority) {
                return false;
            }
            EndShot(liveKind.Value);
        }

        state.isLive = true;
        state.startTimeS = now;
        state.endTimeS = now + holdS;
        state.minHoldEndS = now + settings.MinHoldS;
        liveKind = kind;
        ApplyPriorities();
        if (Debug.isDebugBuild) {
            Debug.Log($"CameraRigManager:\tshot live: {kind}", this);
        }
        return true;
    }

    /// <param name="kind">which shot</param>
    /// <returns>if the shot is live now</returns>
    public bool IsShotLive(ShotKind kind) {
        return states[(int)kind].isLive;
    }

    /// <summary>
    /// Lets a live shot end soon, never before its minimum hold unless asked
    /// </summary>
    /// <param name="kind">which shot</param>
    /// <param name="extraHoldS">time to linger first; s</param>
    /// <param name="isIgnoringMinHold">whether to end at once even inside the
    /// minimum hold</param>
    public void ReleaseShot(
        ShotKind kind,
        float extraHoldS = 0f,
        bool isIgnoringMinHold = false
    ) {
        ShotState state = states[(int)kind];
        if (!state.isLive) {
            return;
        }
        float endS = Time.time + extraHoldS;
        state.endTimeS = isIgnoringMinHold
            ? endS
            : Mathf.Max(endS, state.minHoldEndS);
    }

    // Inspector Fields  #######################################################
    [Tooltip("shot priorities, holds, cooldowns and durations")]
    [SerializeField]
    private CameraConfig config;

    [Tooltip("the default view, looking up the tree behind the monkey")]
    [SerializeField]
    private CinemachineCamera climbCamera;

    [Tooltip("shot that follows a thrown coconut down")]
    [SerializeField]
    private CinemachineCamera coconutCamera;

    [Tooltip("point the coconut shot tracks, set to the thrown coconut")]
    [SerializeField]
    private CameraShotTarget coconutTarget;

    [Tooltip("shot that shows the whole cobra when it starts chasing")]
    [SerializeField]
    private CinemachineCamera cobraAlertCamera;

    [Tooltip("shot that looks up and pulls back during a pineapple leap")]
    [SerializeField]
    private CinemachineCamera leapCamera;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (config == null) {
            Debug.LogWarning(
                "CameraRigManager:\tmust assign Inspector Field: config",
                this
            );
        }
        if (climbCamera == null) {
            Debug.LogWarning(
                "CameraRigManager:\tmust assign Inspector Field: climbCamera",
                this
            );
        }
        if (coconutCamera == null) {
            Debug.LogWarning(
                "CameraRigManager:\tmust assign Inspector Field: "
                    + "coconutCamera",
                this
            );
        }
        if (coconutTarget == null) {
            Debug.LogWarning(
                "CameraRigManager:\tmust assign Inspector Field: "
                    + "coconutTarget",
                this
            );
        }
        if (cobraAlertCamera == null) {
            Debug.LogWarning(
                "CameraRigManager:\tmust assign Inspector Field: "
                    + "cobraAlertCamera",
                this
            );
        }
        if (leapCamera == null) {
            Debug.LogWarning(
                "CameraRigManager:\tmust assign Inspector Field: leapCamera",
                this
            );
        }

        for (int i = 0; i < states.Length; i++) {
            states[i] = new ShotState();
        }
        ApplyPriorities();
    }

    private void Start() {
        SubscribeCobra();
        game = GameController.I;
        if (game == null) {
            Debug.LogWarning(
                "CameraRigManager:\tfail to get singleton: GameController, "
                    + "no leap shot",
                this
            );
        } else {
            game.LeapChanged += OnLeapChanged;
        }
        leapController = FindFirstObjectByType<PineappleLeapController>();
        if (leapController != null) {
            leapController.Landed += OnLeapLanded;
        }
        coconuts = CoconutManager.I;
        if (coconuts == null) {
            Debug.LogWarning(
                "CameraRigManager:\tfail to get singleton: CoconutManager, "
                    + "no coconut shot",
                this
            );
            return;
        }
        coconuts.Thrown += OnCoconutThrown;
        coconuts.Landed += OnCoconutLanded;
    }

    // the cobra, found by type like CoconutManager does; all its events
    // count, none needs the cobra in the scene to be fatal
    private void OnEnable() {
        SubscribeCobra();
    }

    private void OnDisable() {
        UnsubscribeCobra();
    }

    private void OnDestroy() {
        UnsubscribeCobra();
        if (game != null) {
            game.LeapChanged -= OnLeapChanged;
        }
        if (leapController != null) {
            leapController.Landed -= OnLeapLanded;
        }
        if (coconuts != null) {
            coconuts.Thrown -= OnCoconutThrown;
            coconuts.Landed -= OnCoconutLanded;
        }
    }

    private void Update() {
        if (config == null) {
            return;
        }
        // the leap shot follows the leap itself, polled as well as heard, so
        // a missed event can never leave the view stuck up
        if (liveKind == ShotKind.Leap && game != null && !game.IsLeaping) {
            ReleaseShot(ShotKind.Leap);
        }
        if (!liveKind.HasValue) {
            return;
        }

        ShotKind kind = liveKind.Value;
        ShotState state = states[(int)kind];
        float now = Time.time;
        float capS = state.startTimeS + GetSettings(kind).DurationS;
        if (now >= state.endTimeS || now >= capS) {
            EndShot(kind);
        }
    }

    // Event Handlers  #########################################################
    private void OnCoconutThrown(Transform coconut) {
        if (coconutTarget != null) {
            coconutTarget.SetSubject(coconut);
        }
        RequestShot(ShotKind.Coconut);
    }

    // the shot ends at the landing, or at its duration if that comes first
    private void OnCoconutLanded() {
        ReleaseShot(ShotKind.Coconut, 0f, true);
    }

    // the leap shot lasts as long as the leap, capped by its duration
    private void OnLeapChanged(bool isLeaping) {
        if (isLeaping) {
            RequestShot(ShotKind.Leap);
        } else {
            ReleaseShot(ShotKind.Leap);
        }
    }

    // the view hands back the moment the body lands, not after the grace
    private void OnLeapLanded() {
        ReleaseShot(ShotKind.Leap);
    }

    // the one cobra event that gets a look: it first starts moving, once the
    // monkey is out of the starting zone
    private void OnCobraChaseStarted() {
        RequestCobraAlert();
    }

    // constants  ##############################################################
    private const int CLIMB_PRIORITY = 10;
    private const int SHOT_LIVE_PRIORITY = 20;
    private const int SHOT_STANDBY_PRIORITY = 0;

    // private members  ########################################################
    private class ShotState {
        public bool isLive;
        public float startTimeS;
        public float endTimeS;
        public float minHoldEndS;
        public float cooldownEndS;
    }

    private readonly ShotState[] states =
        new ShotState[System.Enum.GetValues(typeof(ShotKind)).Length];
    private ShotKind? liveKind;
    private CoconutManager coconuts;
    private GameController game;
    private PineappleLeapController leapController;
    private CobraMotor cobra;

    // private methods  ########################################################
    private void SubscribeCobra() {
        if (cobra == null) {
            cobra = FindFirstObjectByType<CobraMotor>();
            if (cobra != null) {
                cobra.ChaseStarted += OnCobraChaseStarted;
            }
        }
    }

    private void UnsubscribeCobra() {
        if (cobra != null) {
            cobra.ChaseStarted -= OnCobraChaseStarted;
            cobra = null;
        }
    }

    // shows the whole cobra, but not while it guards the boss arena
    private void RequestCobraAlert() {
        if (config == null || cobra == null || cobra.IsGuarding) {
            return;
        }
        if (RequestShot(ShotKind.CobraAlert, config.CobraAlertShot.DurationS)) cobra.PlayChaseAlertHiss();
    }

    private CameraConfig.ShotSettings GetSettings(ShotKind kind) {
        if (config == null) {
            return null;
        }
        return kind switch {
            ShotKind.Coconut => config.CoconutShot,
            ShotKind.CobraAlert => config.CobraAlertShot,
            ShotKind.Leap => config.LeapShot,
            _ => null,
        };
    }

    private CinemachineCamera GetCamera(ShotKind kind) {
        return kind switch {
            ShotKind.Coconut => coconutCamera,
            ShotKind.CobraAlert => cobraAlertCamera,
            ShotKind.Leap => leapCamera,
            _ => null,
        };
    }

    private void EndShot(ShotKind kind) {
        ShotState state = states[(int)kind];
        state.isLive = false;
        state.cooldownEndS = Time.time + GetSettings(kind).CooldownS;
        liveKind = null;
        ApplyPriorities();
        if (Debug.isDebugBuild) {
            Debug.Log($"CameraRigManager:\tshot ended: {kind}", this);
        }
    }

    // the climb view always stands ready, the live shot rises above it
    private void ApplyPriorities() {
        SetPriority(climbCamera, CLIMB_PRIORITY);
        foreach (ShotKind kind in System.Enum.GetValues(typeof(ShotKind))) {
            bool isLive = liveKind.HasValue && liveKind.Value == kind;
            SetPriority(
                GetCamera(kind),
                isLive ? SHOT_LIVE_PRIORITY : SHOT_STANDBY_PRIORITY
            );
        }
    }

    private static void SetPriority(CinemachineCamera view, int value) {
        if (view == null) {
            return;
        }
        view.Priority = new PrioritySettings {
            Enabled = true,
            Value = value,
        };
    }
}
