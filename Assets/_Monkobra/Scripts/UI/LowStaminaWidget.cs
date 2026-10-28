using TMPro;
using UnityEngine;

/// <summary>
/// Screen-space hint for the first low-stamina feedback since the scene
/// loaded. It listens to the same
/// <see cref="StaminaBarController.LowStaminaMoveAttempted"/> that drives the
/// bar shake and the camera cool-down, so it opens with the first cool-down
/// pulse and shares its threshold, never a second one. It hides after a set
/// time, at once on a game over, and yields to the web prompt: while a web
/// holds the monkey it is hidden and its time left waits for the escape.
/// <para>
/// Put it on an always-active Canvas child: it only toggles the label, so its
/// event subscriptions live on while the label is hidden. It drops its stamina
/// subscription after the first event; nothing is static, so the hint returns
/// on every scene load.
/// </para>
/// </summary>
public class LowStaminaWidget: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip("monkey's struggle in this scene; the hint hides while trapped")]
    private SpiderWebStruggle struggle;

    [SerializeField]
    [Tooltip("hint text on a child object, never this one, shown once per "
        + "scene load")]
    private TMP_Text promptLabel;

    [Header("Text")]
    [SerializeField]
    [Tooltip("hint shown on the first low-stamina feedback only")]
    private string promptMessage =
        "Low energy — eat bananas before you slow down!";

    [SerializeField]
    [Min(0f)]
    [Tooltip("time on screen, time trapped in a web excluded; s")]
    private float showDurationS = 4f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (promptLabel == null) {
            Debug.LogWarning(
                "LowStaminaWidget:\tmust assign Inspector Field: promptLabel",
                this
            );
            enabled = false;
            return;
        }
        if (struggle == null) {
            Debug.LogWarning(
                "LowStaminaWidget:\tmust assign Inspector Field: struggle",
                this
            );
        }

        promptLabel.text = promptMessage;
        SetVisible(false);
    }

    // the stamina bar and the game controller are singletons set in Awake,
    // so both are read once every Awake ran
    private void Start() {
        staminaBar = StaminaBarController.I;
        if (staminaBar == null) {
            Debug.LogError(
                "LowStaminaWidget:\tfail to get singleton: "
                    + "StaminaBarController",
                this
            );
            enabled = false;
            return;
        }
        staminaBar.LowStaminaMoveAttempted += OnLowStaminaMoveAttempted;

        gameController = GameController.I;
        if (gameController == null) {
            Debug.LogWarning(
                "LowStaminaWidget:\tfail to get singleton: GameController",
                this
            );
            return;
        }
        gameController.StateChanged += OnStateChanged;
    }

    private void OnDestroy() {
        UnsubscribeStamina();
        if (gameController != null) {
            gameController.StateChanged -= OnStateChanged;
            gameController = null;
        }
    }

    // counts down only while shown; a web trap hides the hint and holds the
    // time left, so the rest plays once the monkey breaks free
    private void Update() {
        if (remainingS <= 0f) {
            return;
        }

        bool isTrapped = struggle != null && struggle.IsTrapped;
        if (!isTrapped) {
            remainingS -= Time.deltaTime;
        }
        SetVisible(!isTrapped && remainingS > 0f);
    }

    // Event Handlers  #########################################################
    // the first cool-down pulse comes from this same event, so the hint opens
    // with it; later events no longer reach this script
    private void OnLowStaminaMoveAttempted() {
        UnsubscribeStamina();
        remainingS = showDurationS;

        if (Debug.isDebugBuild) {
            Debug.Log("LowStaminaWidget:\tfirst low stamina, hint shown", this);
        }
    }

    // a loss freezes time, so the countdown would stall with the hint showing
    // through the half-transparent end panel
    private void OnStateChanged(GameState state) {
        if (state != GameState.Lost) {
            return;
        }

        remainingS = 0f;
        SetVisible(false);
    }

    // private members  ########################################################
    private float remainingS; // time left on screen, 0 when hidden for good

    // cached references  ------------------------------------------------------
    private StaminaBarController staminaBar;
    private GameController gameController;

    // private methods  ########################################################
    private void SetVisible(bool isVisible) {
        promptLabel.gameObject.SetActive(isVisible);
    }

    private void UnsubscribeStamina() {
        if (staminaBar != null) {
            staminaBar.LowStaminaMoveAttempted -= OnLowStaminaMoveAttempted;
            staminaBar = null;
        }
    }
}
