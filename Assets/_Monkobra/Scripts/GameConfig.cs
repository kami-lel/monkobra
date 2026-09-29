using UnityEngine;

/// <summary>
/// Game-wide tuning shared by every level: the stamina budget, how fast it
/// drains, what a fruit restores and what climbing or orbiting costs.
/// Consumed by <see cref="StaminaBar"/>.
/// </summary>
[CreateAssetMenu(
    fileName = "GameConfig",
    menuName = "Scriptable Objects/GameConfig"
)]
public class GameConfig: ScriptableObject {
    // Public Members  #########################################################
    /// <returns>stamina at level start, also the ceiling for restores</returns>
    public float MaxStamina => maxStamina;

    /// <returns>stamina lost per drain tick</returns>
    public float StaminaDecreaseAmount => staminaDecreaseAmount;

    /// <returns>time between drain ticks; s</returns>
    public float StaminaDecreaseIntervalS => staminaDecreaseIntervalS;

    /// <returns>stamina restored per grabbed fruit</returns>
    public float FruitStaminaRestore => fruitStaminaRestore;

    /// <returns>stamina lost per unit climbed upward</returns>
    public float StaminaUpwardDrainPerU => staminaUpwardDrainPerU;

    /// <returns>stamina lost per unit orbited round the trunk</returns>
    public float StaminaSidewayDrainPerU => staminaSidewayDrainPerU;

    // Inspector Fields  #######################################################
    [Header("Stamina")]
    [SerializeField]
    [Tooltip("stamina at level start, also the ceiling for restores")]
    private float maxStamina = 100f;

    [SerializeField]
    [Tooltip("stamina lost per drain tick")]
    private float staminaDecreaseAmount = 10f;

    [SerializeField]
    [Tooltip("time between drain ticks; s")]
    private float staminaDecreaseIntervalS = 30f;

    [SerializeField]
    [Tooltip("stamina restored per grabbed fruit, capped at max stamina")]
    private float fruitStaminaRestore = 30f;

    [SerializeField]
    [Tooltip(
        "stamina lost per unit climbed upward, the costliest move; "
            + "descending is free"
    )]
    private float staminaUpwardDrainPerU = 0.5f;

    [SerializeField]
    [Tooltip(
        "stamina lost per unit travelled sideways round the trunk, "
            + "keep below the upward rate"
    )]
    private float staminaSidewayDrainPerU = 0.3f;

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaDecreaseAmount = Mathf.Max(0f, staminaDecreaseAmount);
        staminaDecreaseIntervalS = Mathf.Max(0.01f, staminaDecreaseIntervalS);
        fruitStaminaRestore = Mathf.Max(0f, fruitStaminaRestore);
        staminaUpwardDrainPerU = Mathf.Max(0f, staminaUpwardDrainPerU);
        staminaSidewayDrainPerU = Mathf.Max(0f, staminaSidewayDrainPerU);
    }
}
