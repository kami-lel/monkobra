using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private StaminaBar staminaBar;
    [SerializeField] private Transform finishPoint;

    private bool isGameOver;
    private Transform player;

    public void WinGame()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        if (ScreensManager.I != null)
        {
            ScreensManager.I.ShowWinScreen();
        }
        else
        {
            Debug.LogError(
                "GameController:\tScreensManager instance not found",
                this
            );
        }

        Time.timeScale = 0f;
        Debug.Log("GameController:\tgame won", this);
    }

    public void LoseGame()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        if (ScreensManager.I != null)
        {
            ScreensManager.I.ShowLoseScreen();
        }
        else
        {
            Debug.LogError(
                "GameController:\tScreensManager instance not found",
                this
            );
        }

        Time.timeScale = 0f;
        Debug.Log("GameController:\tgame lost", this);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
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

    private void Update()
    {
        if (isGameOver)
        {
            return;
        }

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject == null)
            {
                return;
            }

            player = playerObject.transform;
        }

        if (staminaBar == null || finishPoint == null)
        {
            return;
        }

        bool reachedTop =
            player.position.y >= finishPoint.position.y;

        if (reachedTop && staminaBar.HasStamina)
        {
            WinGame();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Time.timeScale = 1f;
        }
    }
}