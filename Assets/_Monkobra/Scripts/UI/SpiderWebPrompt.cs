using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space UI for the spider web trap, driven by the monkey's
/// <see cref="SpiderWebStruggle"/> events. The first trap since the scene
/// loaded shows the prompt text and the progress bar, later traps show the
/// bar only, and breaking free or the game ending hides both.
/// <para>
/// Put it on an always-active Canvas child: it only toggles the text and bar
/// objects, so its event subscriptions live on while those are hidden.
/// </para>
/// </summary>
public class SpiderWebPrompt: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Wiring")]
    [SerializeField]
    [Tooltip("monkey's struggle in this scene, its events drive this UI")]
    private SpiderWebStruggle struggle;

    [SerializeField]
    [Tooltip("first-trap hint text, hidden on every later trap")]
    private TMP_Text promptLabel;

    [SerializeField]
    [Tooltip("progress bar root, background plus fill, shown while trapped")]
    private GameObject progressBar;

    [SerializeField]
    [Tooltip("fill image, set to Filled, Horizontal from the left at runtime")]
    private Image progressFill;

    [Header("Text")]
    [SerializeField]
    [Tooltip("hint shown on the first trap only")]
    private string promptMessage =
        "Trapped in a web! Press SPACE rapidly to escape!";

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (struggle == null) {
            Debug.LogWarning(
                "SpiderWebPrompt:\tmust assign Inspector Field: struggle",
                this
            );
        }
        if (promptLabel == null) {
            Debug.LogWarning(
                "SpiderWebPrompt:\tmust assign Inspector Field: promptLabel",
                this
            );
        }
        if (progressBar == null) {
            Debug.LogWarning(
                "SpiderWebPrompt:\tmust assign Inspector Field: progressBar",
                this
            );
        }
        if (progressFill == null) {
            Debug.LogWarning(
                "SpiderWebPrompt:\tmust assign Inspector Field: progressFill",
                this
            );
        }

        if (promptLabel != null) {
            promptLabel.text = promptMessage;
        }
        ConfigureFill();
        SetVisible(showPrompt: false, showBar: false);
    }

    private void OnEnable() {
        if (struggle != null) {
            struggle.Trapped += OnTrapped;
            struggle.ProgressChanged += OnProgressChanged;
            struggle.Escaped += OnEscaped;
        }
    }

    private void OnDisable() {
        if (struggle != null) {
            struggle.Trapped -= OnTrapped;
            struggle.ProgressChanged -= OnProgressChanged;
            struggle.Escaped -= OnEscaped;
        }
    }

    // the cobra can end the game on a held monkey, no Escaped follows, and
    // the hint would show through the half-transparent end panel
    private void Update() {
        if (
            isVisible
            && GameController.I != null
            && GameController.I.IsGameOver
        ) {
            SetVisible(showPrompt: false, showBar: false);
        }
    }

    // Event Handlers  #########################################################
    private void OnTrapped(bool isFirstTrap) {
        SetFill(0f);
        SetVisible(showPrompt: isFirstTrap, showBar: true);
    }

    private void OnProgressChanged(float progress) {
        SetFill(progress);
    }

    private void OnEscaped() {
        SetVisible(showPrompt: false, showBar: false);
    }

    // private members  ########################################################
    private bool isVisible; // text or bar shown right now

    // private methods  ########################################################
    // a Filled Image without a sprite draws a plain full quad and ignores
    // fillAmount, so fall back to a sprite made from Unity's own white texture
    private void ConfigureFill() {
        if (progressFill == null) {
            return;
        }

        if (progressFill.sprite == null) {
            Texture2D white = Texture2D.whiteTexture;
            progressFill.sprite = Sprite.Create(
                white,
                new Rect(0f, 0f, white.width, white.height),
                new Vector2(0.5f, 0.5f)
            );
        }
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressFill.fillAmount = 0f;
    }

    private void SetFill(float progress) {
        if (progressFill != null) {
            progressFill.fillAmount = Mathf.Clamp01(progress);
        }
    }

    private void SetVisible(bool showPrompt, bool showBar) {
        isVisible = showPrompt || showBar;
        if (promptLabel != null) {
            promptLabel.gameObject.SetActive(showPrompt);
        }
        if (progressBar != null) {
            progressBar.SetActive(showBar);
        }
    }
}
