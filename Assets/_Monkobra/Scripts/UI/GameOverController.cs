using UnityEngine;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;

    private bool isGameOver;

    private void Start()
    {
        Time.timeScale = 1f;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void ShowGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}