using UnityEngine;

public class CobraCollision : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (GameController.I != null)
        {
            if (Debug.isDebugBuild)
            {
                Debug.Log("CobraCollision:\tplayer caught, losing game", this);
            }

            GameController.I.LoseGame();
        }
        else
        {
            Debug.LogWarning(
                "CobraCollision:\tGameController instance not found",
                this
            );
        }
    }
}