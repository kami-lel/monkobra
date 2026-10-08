using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;

public class GameController: MonoBehaviour {
    // Public Members  #########################################################
    public static GameController I {
        get; private set;
    }

    public GameState State {
        get; private set;
    }

    public event Action<GameState> StateChanged;

    // Public Methods  #########################################################
    public void LoseGame() {
        if (State != GameState.Playing) {
            return;
        }

        // settle score first
        if (ScoreManager.I != null) {
            ScoreManager.I.EndRun();
        }

        SetState(GameState.Lost);
        Debug.Log("GameController:\tgame lost", this);
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("interact action, restarts the scene on the lose screen")]
    private InputActionReference interactAction;

    [SerializeField]
    [Min(0f)]
    [Tooltip("unscaled seconds the lose screen ignores restart input")]
    private float restartInputDelay = 0.5f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (interactAction == null) {
            Debug.LogError(
                "GameController:\tmust assign Inspector Field: Interact Action",
                this
            );
        }

        if (I != null && I != this) {
            Debug.LogWarning(
                "GameController:\tduplicate instance destroyed",
                this
            );

            Destroy(gameObject);
            return;
        }

        I = this;
        SetState(GameState.Tutorial);
        anyPressSub = InputSystem.onAnyButtonPress.CallOnce(OnAnyPressed);
    }

    private void Update() {
        if (State != GameState.Lost || interactAction == null) {
            return;
        }

        if (Time.unscaledTime - lostTime < restartInputDelay) {
            return;
        }

        if (interactAction.action.WasPressedThisFrame()) {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnDestroy() {
        anyPressSub?.Dispose();

        if (I == this) {
            I = null;
            Time.timeScale = 1f;
        }
    }

    // Event Handlers  #########################################################
    private void OnAnyPressed(InputControl _) {
        anyPressSub = null;
        if (State == GameState.Tutorial) {
            SetState(GameState.Playing);
        }
    }

    // private methods  ########################################################
    private void SetState(GameState next) {
        State = next;
        Time.timeScale = next == GameState.Playing ? 1f : 0f;

        if (next == GameState.Lost) {
            lostTime = Time.unscaledTime;
        }

        StateChanged?.Invoke(next);
    }

    // private members  ########################################################
    private IDisposable anyPressSub;
    private float lostTime;
}
