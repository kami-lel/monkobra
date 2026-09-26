using UnityEngine;

public class CobraCollision : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (GameController.Instance != null)
        {
            GameController.Instance.LoseGame();
        }
    }
}