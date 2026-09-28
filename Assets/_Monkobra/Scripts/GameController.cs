using UnityEngine;

public class GameController: MonoBehaviour {
    // Public Members  #########################################################
    public static GameController Instance {
        get; private set;
    }

    // Public Methods  #########################################################
    public void WinGame() {
        if (isGameOver) {
            return;
        }

        isGameOver = true;

        if (ScreensManager.I != null) {
            ScreensManager.I.ShowWinScreen();
        } else {
            Debug.LogError(
                "GameController:\tScreensManager instance not found",
                this
            );
        }

        Time.timeScale = 0f;
        Debug.Log("GameController:\tgame won", this);
    }

    public void LoseGame() {
        if (isGameOver) {
            return;
        }

        isGameOver = true;

        if (ScreensManager.I != null) {
            ScreensManager.I.ShowLoseScreen();
        } else {
            Debug.LogError(
                "GameController:\tScreensManager instance not found",
                this
            );
        }

        Time.timeScale = 0f;
        Debug.Log("GameController:\tgame lost", this);
    }

    // Inspector Fields  #######################################################
    [Header("References")]
    [SerializeField]
    [Tooltip("trigger collider; Player entering it wins the game")]
    private Collider winZone;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (winZone == null) {
            Debug.LogError(
                "GameController:\tmust assign Inspector Field: Win Zone",
                this
            );
        }

        if (Instance != null && Instance != this) {
            Debug.LogWarning(
                "GameController:\tduplicate instance destroyed",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
        Time.timeScale = 1f;
    }

    private void OnTriggerEnter(Collider other) {
        if (isGameOver) {
            return;
        }

        if (!other.CompareTag("Player")) {
            return;
        }

        WinGame();
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
            Time.timeScale = 1f;
        }
    }

    // private members  ########################################################
    private bool isGameOver;
}
