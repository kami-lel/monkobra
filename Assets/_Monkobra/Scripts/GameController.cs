using UnityEngine;

public class GameController: MonoBehaviour {
    // Public Members  #########################################################
    public static GameController I {
        get; private set;
    }

    public bool IsGameOver => isGameOver;
    //公开，让其他脚本查看游戏是否结束
    // Public Methods  #########################################################
    public void WinGame() {
        if (isGameOver) {
            return;
        }

        isGameOver = true;

        if (ScoreManager.I != null) {
            ScoreManager.I.EndRun();
        }//先结算分数

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

        if (ScoreManager.I != null) {
            ScoreManager.I.EndRun();
        }//先结算分数

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

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (I != null && I != this) {
            Debug.LogWarning(
                "GameController:\tduplicate instance destroyed",
                this
            );

            Destroy(gameObject);
            return;
        }

        I = this;
        Time.timeScale = 1f;
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
            Time.timeScale = 1f;
        }
    }

    // private members  ########################################################
    private bool isGameOver;
}
