using UnityEngine;

public class CobraCollision : MonoBehaviour
{
    [SerializeField] private GameOverController gameOverController;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        gameOverController.ShowGameOver();
    }
}
