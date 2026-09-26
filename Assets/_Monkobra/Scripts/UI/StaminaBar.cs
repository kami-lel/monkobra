using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class StaminaBar : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float decreaseAmount = 10f;
    [SerializeField] private float decreaseInterval = 30f;

    public float CurrentStamina => currentStamina;
    public bool HasStamina => currentStamina > 0f;

    private Slider staminaSlider;
    private float currentStamina;

    private void Awake()
    {
        staminaSlider = GetComponent<Slider>();

        currentStamina = maxStamina;
        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = maxStamina;
        staminaSlider.value = currentStamina;
    }

    private void Start()
    {
        StartCoroutine(DecreaseStaminaOverTime());
    }

    private IEnumerator DecreaseStaminaOverTime()
    {
        while (currentStamina > 0f)
        {
            yield return new WaitForSeconds(decreaseInterval);

            currentStamina = Mathf.Max(
                0f,
                currentStamina - decreaseAmount
            );

            staminaSlider.value = currentStamina;

            if (currentStamina <= 0f)
            {
                if (GameController.Instance != null)
                {
                    GameController.Instance.LoseGame();
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
            maxStamina,
            currentStamina + amount
        );

        staminaSlider.value = currentStamina;
    }
}