using UnityEngine;

/// <summary>
/// Singleton holding the climbable tree's vertical extent, for systems that
/// map a height to progress.
/// </summary>
public class TreeManager: MonoBehaviour {
    // Public Members  #########################################################
    public static TreeManager I {
        get; private set;
    }

    /// <returns>tree base world y; u</returns>
    public float MinY => minY;

    /// <returns>tree top world y; u</returns>
    public float MaxY => maxY;

    // Inspector Fields  #######################################################
    [Header("Tree Extent")]
    [SerializeField]
    [Tooltip("tree base world y; u")]
    private float minY = 0f;

    [SerializeField]
    [Tooltip("tree top world y; u")]
    private float maxY = 100f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // drop this duplicate component only, its GameObject may hold more
        if (I != null && I != this) {
            Debug.LogWarning(
                "TreeManager:\tduplicate instance, removing",
                this
            );
            Destroy(this);
            return;
        }
        I = this;

        if (Debug.isDebugBuild) {
            Debug.Log($"TreeManager:\tready, extent y {minY}~{maxY}");
        }
    }

    private void OnDestroy() {
        if (I == this) {
            I = null;
        }
    }
}
