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
        if (Debug.isDebugBuild) {
            Debug.Log(
                $"StaminaBar:\tfruit grabbed, stamina {currentStamina}",
                this
            );
        }
    }

    public void AddStamina(float amount) {
        // a drained bar means the run is lost, the drain loop is gone too
        if (amount <= 0f || !HasStamina) {
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

        if (currentStamina <= 0f) {
            LoseByDepletion();
        }
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
    private bool isDepletionHandled; // timer and movement both can empty bar

    // cached references  ------------------------------------------------------
    private Slider staminaSlider;

    // private methods  ########################################################
    private IEnumerator DecreaseStaminaOverTime() {
        while (currentStamina > 0f) {
            yield return new WaitForSeconds(config.StaminaDecreaseIntervalS);

            currentStamina = Mathf.Max(
                0f,
                currentStamina - config.StaminaDecreaseAmount
            );

            staminaSlider.value = currentStamina;

            if (currentStamina <= 0f) {
                LoseByDepletion();
                yield break;
            }
        }
    }

    // run is lost once, whichever of timer or movement empties the bar
    private void LoseByDepletion() {
        if (isDepletionHandled) {
            return;
        }
        isDepletionHandled = true;

        if (GameController.I != null) {
            if (Debug.isDebugBuild) {
                Debug.Log("StaminaBar:\tstamina depleted, losing game", this);
            }

            GameController.I.LoseGame();
        } else {
            Debug.LogWarning(
                "StaminaBar:\tGameController instance not found",
                this
            );
        }
    }
}
