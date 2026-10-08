using UnityEngine;

public class ScreensManager: MonoBehaviour {
    // Public Members  #########################################################
    public static ScreensManager I {
        get; private set;
    }

    // Public Methods  #########################################################
    /// <summary>Shows the screen of the given state, hides the others.</summary>
    public void ShowScreenFor(GameState state) {
        SetScreens(
            tutorialActive: state == GameState.Tutorial,
            loseActive: state == GameState.Lost
        );
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("tutorial screen panel")]
    private GameObject tutorialScreen;

    [SerializeField]
    [Tooltip("lose screen panel")]
    private GameObject loseScreen;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (tutorialScreen == null) {
            Debug.LogError(
                "ScreensManager:\tmust assign Inspector Field: Tutorial Screen",
                this
            );
        }
        if (loseScreen == null) {
            Debug.LogError(
                "ScreensManager:\tmust assign Inspector Field: Lose Screen",
                this
            );
        }

        if (I != null && I != this) {
            Debug.LogWarning(
                "ScreensManager:\tduplicate instance destroyed",
                this
            );
            Destroy(gameObject);
            return;
        }
        I = this;
    }

    // GameController may set its first state before this singleton exists
    private void Start() {
        if (GameController.I != null) {
            ShowScreenFor(GameController.I.State);
        }
    }

    // private methods  ########################################################
    private void SetScreens(bool tutorialActive, bool loseActive) {
        if (tutorialScreen != null) {
            tutorialScreen.SetActive(tutorialActive);
        }
        if (loseScreen != null) {
            loseScreen.SetActive(loseActive);
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"ScreensManager:\ttutorial={tutorialActive}, lose={loseActive}"
            );
        }
    }
}
