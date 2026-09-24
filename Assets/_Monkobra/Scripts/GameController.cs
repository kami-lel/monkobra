using UnityEngine;

/// <summary>
/// Scene-scoped singleton entry point for game-wide control.
/// </summary>
public class GameController: MonoBehaviour {
    // Public Members  #########################################################
    /// <summary>the sole live instance; null before Awake or after
    /// destroy</summary>
    public static GameController Instance {
        get; private set;
    }

    // Public Methods  #########################################################
    /// <summary>ends the game as a win; ignored once the game is
    /// over</summary>
    public void WinGame() {
        // Todo show win screen
        EndGame("game won");
    }

    /// <summary>ends the game as a loss; ignored once the game is
    /// over</summary>
    public void LoseGame() {
        // Todo show lose screen
        EndGame("game lost");
    }

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Singleton Guard  ----------------------------------------------------
        if (Instance != null && Instance != this) {
            Debug.LogWarning(
                "GameController:\tduplicate instance destroyed", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    // private members  ########################################################
    // if the game already ended in a win or loss
    private bool isGameOver;

    // private methods  ########################################################
    private void EndGame(string result) {
        if (isGameOver) {
            return;
        }
        isGameOver = true;
        Time.timeScale = 0f;
        Debug.Log($"GameController:\t{result}", this);
    }
}

