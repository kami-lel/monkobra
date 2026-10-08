using UnityEngine;

/// <summary>
/// Shared tuning for how Branch With Fruit prefabs are placed on a tree
/// segment.
/// </summary>
[CreateAssetMenu(
    fileName = "BranchFruitPlacementConfig",
    menuName = "Scriptable Objects/BranchFruitPlacementConfig"
)]
public class BranchFruitPlacementConfig: ScriptableObject {
    // Public Members  #########################################################
    /// <returns>climb progress, 0~1, at/above which a segment gets the
    /// max branch count of <see cref="GameBalanceConfig"/></returns>
    public float MaxCountProgress => maxCountProgress;

    /// <returns>branch distance from the trunk axis; u</returns>
    public float BranchRadius => branchRadius;

    /// <returns>min distance between branches; u</returns>
    public float MinBranchSeparation => minBranchSeparation;

    /// <returns>random position retries per branch before giving up on
    /// the separation rule</returns>
    public int MaxPlacementAttempts => maxPlacementAttempts;

    // Inspector Fields  #######################################################
    [Header("Branch Count")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("climb progress at/above which segment gets max branches")]
    private float maxCountProgress = 0.6f;

    [Header("Branch Position")]
    [SerializeField]
    [Tooltip("branch distance fr trunk axis; u")]
    private float branchRadius = 6.6f;

    [SerializeField]
    [Tooltip("min distance b/t branches; u")]
    private float minBranchSeparation = 3f;

    [SerializeField]
    [Tooltip("position retries per branch b4 accepting a close one")]
    private int maxPlacementAttempts = 30;

    // Editor Validation  ######################################################
    private void OnValidate() {
        branchRadius = Mathf.Max(0f, branchRadius);
        minBranchSeparation = Mathf.Max(0f, minBranchSeparation);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
    }
}
