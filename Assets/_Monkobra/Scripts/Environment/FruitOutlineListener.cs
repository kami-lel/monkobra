using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Sits on the "FruitCollider" object of a banana. Listens to
/// <see cref="UpwardFruitDetection"/> and outlines every child mesh renderer
/// in 1 of 2 tiers, grabbable winning over reachable. Only the single
/// targeted fruit is ever outlined:
/// <list type="bullet">
/// <item>reachable: the fruit is the one a reach would aim at</item>
/// <item>grabbable: a hand overlaps the fruit, so a release grabs it</item>
/// </list>
/// The outline is an extra material slot, added only while a tier applies.
/// </summary>
public class FruitOutlineListener: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("Outline Materials")]
    [SerializeField]
    [Tooltip("outline while fruit sits in a detection zone")]
    private Material reachableOutlineMat;

    [FormerlySerializedAs("outlineMat")]
    [SerializeField]
    [Tooltip("outline while a hand overlaps fruit; release grabs it")]
    private Material grabbableOutlineMat;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (reachableOutlineMat == null) {
            Debug.LogWarning(
                "FruitOutlineListener:\t"
                    + "must assign Inspector Field: reachableOutlineMat",
                this
            );
        }
        if (grabbableOutlineMat == null) {
            Debug.LogWarning(
                "FruitOutlineListener:\t"
                    + "must assign Inspector Field: grabbableOutlineMat",
                this
            );
        }

        fruitCollider = GetComponent<Collider>();
        if (fruitCollider == null) {
            Debug.LogError(
                "FruitOutlineListener:\tfail to get Component: Collider",
                this
            );
        }

        meshes = GetComponentsInChildren<MeshRenderer>(true);
        plainMats = new Material[meshes.Length][];
        reachableMats = new Material[meshes.Length][];
        grabbableMats = new Material[meshes.Length][];
        for (int i = 0; i < meshes.Length; i++) {
            // keep slot 0 only, so a leftover outline slot never sticks
            Material baseMat = meshes[i].sharedMaterials[0];
            plainMats[i] = new Material[] { baseMat };
            reachableMats[i] = BuildMats(baseMat, reachableOutlineMat);
            grabbableMats[i] = BuildMats(baseMat, grabbableOutlineMat);
        }
        ApplyOutline();
    }

    // read in Start, so Awake order never matters
    private void Start() {
        UpwardFruitDetection detection = UpwardFruitDetection.I;
        if (detection == null) {
            Debug.LogError(
                "FruitOutlineListener:\t"
                    + "fail to get singleton: UpwardFruitDetection",
                this
            );
            return;
        }

        detection.ReachTargetChanged += OnReachTargetChanged;
        detection.GrabbableFruitChanged += OnGrabbableFruitChanged;
        isReachable = detection.ReachTarget == fruitCollider;
        isGrabbable = detection.GrabbableFruit == fruitCollider;
        ApplyOutline();
    }

    private void OnDestroy() {
        UpwardFruitDetection detection = UpwardFruitDetection.I;
        if (detection != null) {
            detection.ReachTargetChanged -= OnReachTargetChanged;
            detection.GrabbableFruitChanged -= OnGrabbableFruitChanged;
        }
    }

    // Event Handlers  #########################################################
    private void OnReachTargetChanged(Collider previous, Collider current) {
        if (current == fruitCollider) {
            isReachable = true;
        } else if (previous == fruitCollider) {
            isReachable = false;
        } else {
            return;
        }
        ApplyOutline();
    }

    private void OnGrabbableFruitChanged(Collider previous, Collider current) {
        if (current == fruitCollider) {
            isGrabbable = true;
        } else if (previous == fruitCollider) {
            isGrabbable = false;
        } else {
            return;
        }
        ApplyOutline();
    }

    // private members  ########################################################
    private bool isReachable;
    private bool isGrabbable;

    // cached references  ------------------------------------------------------
    private Collider fruitCollider;
    private MeshRenderer[] meshes;

    // per mesh: slot 0 alone, or slot 0 plus a tier's outline
    private Material[][] plainMats;
    private Material[][] reachableMats;
    private Material[][] grabbableMats;

    // private methods  ########################################################
    // w/o an outline material the tier falls back to plain, never a null slot
    private static Material[] BuildMats(Material baseMat, Material outlineMat) {
        return outlineMat != null
            ? new Material[] { baseMat, outlineMat }
            : new Material[] { baseMat };
    }

    // grabbable beats reachable beats none
    private void ApplyOutline() {
        Material[][] tierMats =
            isGrabbable ? grabbableMats
            : isReachable ? reachableMats
            : plainMats;
        for (int i = 0; i < meshes.Length; i++) {
            meshes[i].sharedMaterials = tierMats[i];
        }

    }
}
