using TMPro;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(Scrollbar))]
public class ProgressBarRoot: MonoBehaviour {
    [Header("Cobra Marker")]
    [SerializeField] private Transform cobraHead;
    [SerializeField] private RectTransform cobraMarker;

    [Header("Window")]
    // world-y span shown by the full bar, player pinned at top
    [SerializeField] private float windowSizeY = 20f;

    [Header("Catch-Up Cue")]
    // on each mistake that makes the cobra surge, a warning shows at the
    // screen center and fades out; the marker flashes while the surge lasts
    [SerializeField] private string catchUpText = "The cobra is speeding up!";
    // appended with the stack count from the 2nd stacked mistake on
    [SerializeField] private string catchUpStackFormat = " x{0}";
    [SerializeField] private Color catchUpColor = new Color(1f, 0.2f, 0.15f, 1f);
    [SerializeField] private float catchUpPulseHz = 3f;
    // text height as a share of the screen height, so it reads the same on
    // a 1080p and a 4K screen
    [SerializeField] private float catchUpFontScreenShare = 0.07f;
    // how long the warning stays, its last part fading out; s
    [SerializeField] private float catchUpShowS = 1.5f;
    [SerializeField] private float catchUpFadeS = 0.5f;
    // warning height on screen, 0 bottom ~ 1 top
    [SerializeField] private float catchUpScreenY = 0.65f;

    private const string PLAYER_TAG = "Player";

    private Scrollbar progressBar;
    private Transform player;

    private bool hasWarnedNoPlayer;

    private CobraClimb cobra;
    private Graphic markerGraphic;
    private Color markerBaseColor;
    private TextMeshProUGUI catchUpLabel;
    private float catchUpHideTime; // Time.time the warning is gone

    private void Awake() {
        progressBar = GetComponent<Scrollbar>();

        if (progressBar == null) {
            Debug.LogError(
                "ProgressBarRoot: failed to get Scrollbar.",
                this
            );

            enabled = false;
            return;
        }

        if (windowSizeY <= 0f) {
            Debug.LogWarning(
                "ProgressBarRoot: windowSizeY must be positive.",
                this
            );

            windowSizeY = 1f;
        }

        if (cobraHead != null) {
            cobra = cobraHead.GetComponentInParent<CobraClimb>();
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

    private void Update() {
        if (!TryFindPlayer()) {
            return;
        }

        // player stays at the far end of the bar
        progressBar.SetValueWithoutNotify(1f);

        UpdateCobraProgress();
        UpdateCatchUpCue();
    }

    private bool TryFindPlayer() {
        if (player != null) {
            return true;
        }

        GameObject playerObject =
            GameObject.FindGameObjectWithTag(PLAYER_TAG);

        if (playerObject == null) {
            if (!hasWarnedNoPlayer) {
                Debug.LogWarning(
                    "ProgressBarRoot: no Player found.",
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
