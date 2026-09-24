using UnityEngine;

/// <summary>
/// Prefab root for one branch. Decides on initialization whether this
/// branch bears a banana, disabling the Banana + Banana Stem children when
/// it does not, or scaling/repositioning them into place when it does.
/// </summary>
public class BranchWithScriptRoot: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("banana child; disabled w/o banana")]
    private GameObject banana;

    [SerializeField]
    [Tooltip("banana stem child; scaled w/ banana")]
    private GameObject bananaStem;

    [Header("Banana Spawn")]
    [SerializeField]
    [Tooltip("chance branch bears banana; 0~1")]
    [Range(0f, 1f)]
    private float bananaSpawnProbability = 0.5f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (banana == null) {
            Debug.LogWarning(
                "BranchWithScriptRoot:\tmust assign Inspector Field: banana",
                this
            );
        }
        if (bananaStem == null) {
            Debug.LogWarning(
                "BranchWithScriptRoot:\t"
                    + "must assign Inspector Field: bananaStem",
                this
            );
        }

        if (banana == null || bananaStem == null) {
            return;
        }

        if (Random.value <= bananaSpawnProbability) {
            AttachBanana();
        } else {
            banana.SetActive(false);
            bananaStem.SetActive(false);
        }
    }

    // constants  ##############################################################
    private const float STEM_Y_SCALE_MIN = 0.5f;
    private const float STEM_Y_SCALE_MAX = 2.0f;
    private const float BANANA_Y_OFFSET = -.2f;

    // private methods  ########################################################
    private void AttachBanana() {
        Vector3 stemBasePosition = bananaStem.transform.localPosition;
        Vector3 bananaBaseOffset =
            banana.transform.localPosition - stemBasePosition;

        float stemYScale = Random.Range(STEM_Y_SCALE_MIN, STEM_Y_SCALE_MAX);
        Vector3 stemScale = bananaStem.transform.localScale;
        bananaStem.transform.localScale = new Vector3(
            stemScale.x,
            stemScale.y * stemYScale,
            stemScale.z
        );

        banana.transform.localPosition =
            stemBasePosition
            + bananaBaseOffset * stemYScale
            + Vector3.up * BANANA_Y_OFFSET;
        banana.transform.localRotation = Quaternion.Euler(
            0f,
            Random.Range(0f, 360f),
            0f
        );
    }
}
