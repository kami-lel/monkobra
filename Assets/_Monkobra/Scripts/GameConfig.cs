using UnityEngine;

/// <summary>
/// Game-wide tuning shared by every level: the stamina budget and how fast it
/// drains. Consumed by <see cref="StaminaBar"/>.
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

    // Editor Validation  ######################################################
    private void OnValidate() {
        maxStamina = Mathf.Max(1f, maxStamina);
        staminaDecreaseAmount = Mathf.Max(0f, staminaDecreaseAmount);
        staminaDecreaseIntervalS = Mathf.Max(0.01f, staminaDecreaseIntervalS);
    }
}
