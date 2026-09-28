using UnityEngine;

public class ScreensManager: MonoBehaviour {
    // Public Members  #########################################################
    public static ScreensManager I {
        get; private set;
    }

    // Public Methods  #########################################################
    /// <summary>Shows the win screen and hides the lose screen.</summary>
    public void ShowWinScreen() {
        SetScreens(winActive: true, loseActive: false);
    }

    /// <summary>Shows the lose screen and hides the win screen.</summary>
    public void ShowLoseScreen() {
        SetScreens(winActive: false, loseActive: true);
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("win screen panel")]
    private GameObject winScreen;

    [SerializeField]
    [Tooltip("lose screen panel")]
    private GameObject loseScreen;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (winScreen == null) {
            Debug.LogError(
                "ScreensManager:\tmust assign Inspector Field: Win Screen",
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

    private void Start() {
        SetScreens(winActive: false, loseActive: false);
    }

    // private methods  ########################################################
    private void SetScreens(bool winActive, bool loseActive) {
        if (winScreen != null) {
            winScreen.SetActive(winActive);
        }
        if (loseScreen != null) {
            loseScreen.SetActive(loseActive);
        }

        if (Debug.isDebugBuild) {
            Debug.Log($"ScreensManager:\twin={winActive}, lose={loseActive}");
        }
    }
}
