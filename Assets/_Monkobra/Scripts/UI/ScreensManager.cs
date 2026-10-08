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

    // panels start deactivated, this manager alone turns them on. Sync to the
    // state GameController set in Awake, then follow its changes
    private void Start() {
        if (GameController.I == null) {
            Debug.LogError(
                "ScreensManager:\tGameController instance not found",
                this
            );
            return;
        }

        ShowScreenFor(GameController.I.State);
        GameController.I.StateChanged += ShowScreenFor;
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }

        if (GameController.I != null) {
            GameController.I.StateChanged -= ShowScreenFor;
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
