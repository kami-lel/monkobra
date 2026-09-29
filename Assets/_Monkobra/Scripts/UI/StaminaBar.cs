using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class StaminaBar : MonoBehaviour
{
    [SerializeField]
    [Tooltip("game-wide tuning; stamina budget and drain rate")]
    private GameConfig config;

    public float CurrentStamina => currentStamina;
    public bool HasStamina => currentStamina > 0f;

    private Slider staminaSlider;
    private float currentStamina;

    private void Awake()
    {
        // Inspector Assignment Guard  -----------------------------------------
        if (config == null)
        {
            Debug.LogWarning(
                "StaminaBar:\tmust assign Inspector Field: config",
                this
            );
            enabled = false;
            return;
        }

        staminaSlider = GetComponent<Slider>();

        currentStamina = config.MaxStamina;
        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = config.MaxStamina;
        staminaSlider.value = currentStamina;

        if (Debug.isDebugBuild)
        {
            Debug.Log("StaminaBar:\tready", this);
        }
    }

    private void Start()
    {
        StartCoroutine(DecreaseStaminaOverTime());
    }

    private IEnumerator DecreaseStaminaOverTime()
    {
        while (currentStamina > 0f)
        {
            yield return new WaitForSeconds(config.StaminaDecreaseIntervalS);

            currentStamina = Mathf.Max(
                0f,
                currentStamina - config.StaminaDecreaseAmount
            );

            staminaSlider.value = currentStamina;

            if (currentStamina <= 0f)
            {
                if (GameController.I != null)
                {
                    if (Debug.isDebugBuild)
                    {
                        Debug.Log(
                            "StaminaBar:\tstamina depleted, losing game",
                            this
                        );
                    }

                    GameController.I.LoseGame();
                }
                else
                {
                    Debug.LogWarning(
                        "StaminaBar:\tGameController instance not found",
                        this
                    );
                }

                yield break;
            }
        }
    }

    public void AddStamina(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentStamina = Mathf.Min(
            config.MaxStamina,
            currentStamina + amount
        );

        staminaSlider.value = currentStamina;
    }
}