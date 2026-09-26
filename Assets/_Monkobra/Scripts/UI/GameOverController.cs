using TMPro;
using UnityEngine;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;

    private void Start()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void ShowLose()
    {
        ShowResult("Game Over\nYou Lose");
    }

    public void ShowWin()
    {
        ShowResult("Congratulations!\nYou Win");
    }

    private void ShowResult(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }
    }
}