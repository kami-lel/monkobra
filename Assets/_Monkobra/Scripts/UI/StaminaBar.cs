using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class StaminaBar: MonoBehaviour {
    // Public Members  #########################################################
    public static StaminaBar I {
        get; private set;
    }

    public float CurrentStamina => currentStamina;
    public bool HasStamina => currentStamina > 0f;

    // Public Methods  #########################################################
    /// <summary>
    /// restores the per-fruit amount from <see cref="GameConfig"/>, called
    /// when an arm grabs a fruit
    /// </summary>
    public void AddStaminaByFruit() {
        AddStamina(config.FruitStaminaRestore);
    }

    public void AddStamina(float amount) {
        if (amount <= 0f) {
            return;
        }

        currentStamina = Mathf.Min(
            config.MaxStamina,
            currentStamina + amount
        );

        staminaSlider.value = currentStamina;
    }

    /// <summary>
    /// drains stamina for one step of movement: climbing costs the most,
    /// orbiting the trunk costs less, descending is free. Rates come from
    /// <see cref="GameConfig"/>
    /// </summary>
    /// <param name="climbedU">upward distance covered this step; u</param>
    /// <param name="orbitedU">sideways arc length covered this step; u</param>
    public void DrainByMovement(float climbedU, float orbitedU) {
        if (!HasStamina) {
            return;
        }

        float amount =
            Mathf.Max(0f, climbedU) * config.StaminaUpwardDrainPerU
            + Mathf.Max(0f, orbitedU) * config.StaminaSidewayDrainPerU;
        if (amount <= 0f) {
            return;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        staminaSlider.value = currentStamina;
    }

    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("game-wide tuning; stamina budget and drain rate")]
    private GameConfig config;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (config == null) {
            Debug.LogWarning(
                "StaminaBar:\tmust assign Inspector Field: config",
                this
            );
            enabled = false;
            return;
        }

        // drop this duplicate component only, the GameObject may hold more
        if (I != null && I != this) {
            Debug.LogWarning("StaminaBar:\tduplicate instance, removing", this);
            Destroy(this);
            return;
        }

        I = this;
        staminaSlider = GetComponent<Slider>();

        currentStamina = config.MaxStamina;
        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = config.MaxStamina;
        staminaSlider.value = currentStamina;

        if (Debug.isDebugBuild) {
            Debug.Log("StaminaBar:\tready", this);
        }
    }

    private void Start() {
        StartCoroutine(DecreaseStaminaOverTime());
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }

    // private members  ########################################################
    private float currentStamina;

    // cached references  ------------------------------------------------------
    private Slider staminaSlider;

    // private methods  ########################################################
    private IEnumerator DecreaseStaminaOverTime() {
        // an empty bar only slows the monkey, so the tick keeps running
        while (true) {
            yield return new WaitForSeconds(config.StaminaDecreaseIntervalS);

            currentStamina = Mathf.Max(
                0f,
                currentStamina - config.StaminaDecreaseAmount
            );

            staminaSlider.value = currentStamina;
        }
    }
}
