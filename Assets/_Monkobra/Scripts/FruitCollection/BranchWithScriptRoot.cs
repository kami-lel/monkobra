using UnityEngine;

/// <summary>
/// Prefab root for one branch. Decides on initialization whether this
/// branch bears a banana, disabling the Banana + Banana Stem children when
/// it does not, or scaling/repositioning them into place when it does.
/// </summary>
public class BranchWithScriptRoot: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    private GameObject banana;

    [SerializeField]
    private GameObject bananaStem;

    [SerializeField]
    private float bananaSpawnProbability = 0.5f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (banana == null || bananaStem == null) {
            Debug.LogWarning(
                "must assign Inspector Fields: Banana, Banana Stem", this);
            return;
        }

        if (Random.value <= bananaSpawnProbability) {
            AttachBanana();
        } else {
            banana.SetActive(false);
            bananaStem.SetActive(false);
        }
    }

    // Constants  ###############################################################
    private const float STEM_Y_SCALE_MIN = 0.5f;
    private const float STEM_Y_SCALE_MAX = 2.0f;
    private const float BANANA_Y_OFFSET = -.2f;

    // Private Methods  #########################################################
    private void AttachBanana() {
        Vector3 stemBasePosition = bananaStem.transform.localPosition;
        Vector3 bananaBaseOffset =
            banana.transform.localPosition - stemBasePosition;

        float stemYScale = Random.Range(STEM_Y_SCALE_MIN, STEM_Y_SCALE_MAX);
        Vector3 stemScale = bananaStem.transform.localScale;
        bananaStem.transform.localScale =
            new Vector3(stemScale.x, stemScale.y * stemYScale, stemScale.z);

        banana.transform.localPosition =
            stemBasePosition + bananaBaseOffset * stemYScale +
            Vector3.up * BANANA_Y_OFFSET;
        banana.transform.localRotation =
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
}
