using TMPro;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(Scrollbar))]
public class ProgressBarRoot: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Cobra Marker")]
    [SerializeField]
    [Tooltip("cobra head; its height sets the marker")]
    private Transform cobraHead;

    [SerializeField]
    [Tooltip("marker moved along the bar")]
    private RectTransform cobraMarker;

    [Header("Window")]
    [SerializeField]
    [Tooltip("world-y span of the full bar, player pinned at top; u")]
    private float windowSizeY = 20f;

    [Header("Catch-Up Cue")]
    [SerializeField]
    [Tooltip("warning at screen center on each cobra surge")]
    private string catchUpText = "The cobra is speeding up!";

    [SerializeField]
    [Tooltip("appended w/ stack count from 2nd stacked mistake on")]
    private string catchUpStackFormat = " x{0}";

    [SerializeField]
    [Tooltip("warning & marker flash color")]
    private Color catchUpColor = new Color(1f, 0.2f, 0.15f, 1f);

    [SerializeField]
    [Tooltip("marker flashes per s while surging")]
    private float catchUpPulseHz = 3f;

    [SerializeField]
    [Tooltip("text height as share of screen height")]
    private float catchUpFontScreenShare = 0.07f;

    [SerializeField]
    [Tooltip("warning stays this long, last part fading; s")]
    private float catchUpShowS = 1.5f;

    [SerializeField]
    [Tooltip("fade-out part of the show time; s")]
    private float catchUpFadeS = 0.5f;

    [SerializeField]
    [Tooltip("warning height on screen, 0 bottom ~ 1 top")]
    private float catchUpScreenY = 0.65f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (cobraHead == null) {
            Debug.LogWarning(
                "ProgressBarRoot:\tmust assign Inspector Field: cobraHead",
                this);
        }
        if (cobraMarker == null) {
            Debug.LogWarning(
                "ProgressBarRoot:\tmust assign Inspector Field: cobraMarker",
                this);
        }

        progressBar = GetComponent<Scrollbar>();

        if (progressBar == null) {
            Debug.LogError(
                "ProgressBarRoot:\tfail to get Component: Scrollbar",
                this
            );

            enabled = false;
            return;
        }

        if (windowSizeY <= 0f) {
            Debug.LogWarning(
                "ProgressBarRoot:\twindowSizeY must be positive",
                this
            );

            windowSizeY = 1f;
        }

        if (cobraHead != null) {
            cobra = cobraHead.GetComponentInParent<CobraMotor>();
        }

        if (cobraMarker != null) {
            markerGraphic = cobraMarker.GetComponent<Graphic>();
            if (markerGraphic != null) {
                markerBaseColor = markerGraphic.color;
            }
            catchUpLabel = CreateCatchUpLabel();
        }

        if (cobra != null) {
            cobra.CatchUpStarted += OnCatchUpStarted;
        }
    }

    private void OnDestroy() {
        if (cobra != null) {
            cobra.CatchUpStarted -= OnCatchUpStarted;
        }
    }

    private void Update() {
        if (!TryFindPlayer()) {
            return;
        }

        // player stays at the far end of the bar
        progressBar.SetValueWithoutNotify(1f);

        UpdateCobraProgress();
        UpdateCatchUpCue();
    }

    // Event Handlers  #########################################################
    private void OnCatchUpStarted(int stacks) {
        catchUpHideTime = Time.time + catchUpShowS;
        if (catchUpLabel == null) {
            return;
        }

        catchUpLabel.text = stacks > 1
            ? catchUpText + string.Format(catchUpStackFormat, stacks)
            : catchUpText;

        // re-sized on every show, in case the window size changed
        Canvas canvas = catchUpLabel.canvas;
        float scaleFactor = canvas != null && canvas.scaleFactor > 0f
            ? canvas.scaleFactor
            : 1f;
        float fontSize = Screen.height * catchUpFontScreenShare / scaleFactor;
        catchUpLabel.fontSize = fontSize;
        catchUpLabel.rectTransform.sizeDelta = new Vector2(0f, fontSize * 1.6f);
    }

    // constants  ##############################################################
    private const string PLAYER_TAG = "Player";

    // private members  ########################################################
    private Scrollbar progressBar;
    private Transform player;

    private bool hasWarnedNoPlayer;

    private CobraMotor cobra;
    private Graphic markerGraphic;
    private Color markerBaseColor;
    private TextMeshProUGUI catchUpLabel;
    private float catchUpHideTime; // Time.time the warning is gone

    // private methods  ########################################################
    private bool TryFindPlayer() {
        if (player != null) {
            return true;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(PLAYER_TAG);

        if (playerObject == null) {
            if (!hasWarnedNoPlayer) {
                Debug.LogWarning(
                    "ProgressBarRoot:\tfail to find GameObject tagged: Player",
                    this
                );

                hasWarnedNoPlayer = true;
            }

            return false;
        }

        player = playerObject.transform;
        return true;
    }

    private void UpdateCobraProgress() {
        if (cobraHead == null || cobraMarker == null) {
            return;
        }

        // gap below player, clamped so a far cobra sticks at bar bottom
        float gap = Mathf.Max(0f, player.position.y - cobraHead.position.y);
        float cobraProgress = 1f - Mathf.Clamp01(gap / windowSizeY);

        Vector2 anchor = new Vector2(0.5f, 0.5f);

        switch (progressBar.direction) {
            case Scrollbar.Direction.LeftToRight:
                anchor.x = cobraProgress;
                break;

            case Scrollbar.Direction.RightToLeft:
                anchor.x = 1f - cobraProgress;
                break;

            case Scrollbar.Direction.BottomToTop:
                anchor.y = cobraProgress;
                break;

            case Scrollbar.Direction.TopToBottom:
                anchor.y = 1f - cobraProgress;
                break;
        }

        cobraMarker.anchorMin = anchor;
        cobraMarker.anchorMax = anchor;
        cobraMarker.anchoredPosition = Vector2.zero;
    }

    // the marker flashes while the cobra catches up; the center warning
    // shows once per catch-up, then fades out, only during a run
    private void UpdateCatchUpCue() {
        bool isPlaying =
            GameController.I == null
            || GameController.I.State == GameState.Playing;
        bool isCatchingUp = isPlaying && cobra != null && cobra.IsCatchingUp;

        // 0~1 pulse, frozen with the game clock
        float pulse =
            0.5f + 0.5f * Mathf.Sin(Time.time * catchUpPulseHz * 2f * Mathf.PI);

        if (markerGraphic != null) {
            markerGraphic.color = isCatchingUp
                ? Color.Lerp(markerBaseColor, catchUpColor, pulse)
                : markerBaseColor;
        }

        if (catchUpLabel == null) {
            return;
        }

        float remainingS = catchUpHideTime - Time.time;
        bool show = isPlaying && remainingS > 0f;
        catchUpLabel.gameObject.SetActive(show);
        if (show) {
            Color labelColor = catchUpColor;
            labelColor.a = catchUpFadeS > 0f
                ? Mathf.Clamp01(remainingS / catchUpFadeS)
                : 1f;
            catchUpLabel.color = labelColor;
        }
    }

    // a full-width label built at runtime on the root canvas, at the screen
    // center
    private TextMeshProUGUI CreateCatchUpLabel() {
        Canvas canvas = cobraMarker.GetComponentInParent<Canvas>();
        if (canvas == null) {
            return null;
        }

        GameObject labelObject =
            new GameObject("CatchUpWarning", typeof(RectTransform));
        labelObject.transform.SetParent(canvas.rootCanvas.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, catchUpScreenY);
        labelRect.anchorMax = new Vector2(1f, catchUpScreenY);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 100f);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = catchUpText;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.color = catchUpColor;

        labelObject.SetActive(false);
        return label;
    }
}
