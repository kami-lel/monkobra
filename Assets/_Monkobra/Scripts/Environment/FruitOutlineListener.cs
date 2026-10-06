using UnityEngine;

/// <summary>
/// Sits on the "FruitCollider" object of a banana. Listens to
/// <see cref="UpwardFruitDetection.GrabbableFruitChanged"/> and, while a
/// hand overlaps this fruit (release grabs it), adds the outline material as
/// an extra slot on every child mesh renderer.
/// </summary>
public class FruitOutlineListener: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    [Tooltip("outline material; extra slot on fruit meshes while grabbable")]
    private Material outlineMat;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (outlineMat == null) {
            Debug.LogWarning(
                "FruitOutlineListener:\t"
                    + "must assign Inspector Field: outlineMat",
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
        outlinedMats = new Material[meshes.Length][];
        for (int i = 0; i < meshes.Length; i++) {
            // keep slot 0 only, so a leftover outline slot never sticks
            Material baseMat = meshes[i].sharedMaterials[0];
            plainMats[i] = new Material[] { baseMat };
            outlinedMats[i] = new Material[] { baseMat, outlineMat };
        }
        SetOutlined(false);
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

        detection.GrabbableFruitChanged += OnGrabbableFruitChanged;
        SetOutlined(detection.GrabbableFruit == fruitCollider);
    }

    private void OnDestroy() {
        if (UpwardFruitDetection.I != null) {
            UpwardFruitDetection.I.GrabbableFruitChanged -=
                OnGrabbableFruitChanged;
        }
    }

    // Event Handlers  #########################################################
    private void OnGrabbableFruitChanged(Collider previous, Collider current) {
        if (current == fruitCollider) {
            SetOutlined(true);
        } else if (previous == fruitCollider) {
            SetOutlined(false);
        }
    }

    // private members  ########################################################
    private Collider fruitCollider;
    private MeshRenderer[] meshes;
    private Material[][] plainMats;
    private Material[][] outlinedMats;

    // private methods  ########################################################
    private void SetOutlined(bool isOutlined) {
        // w/o outlineMat still strip the slot, never show a null material
        bool showOutline = isOutlined && outlineMat != null;
        for (int i = 0; i < meshes.Length; i++) {
            meshes[i].sharedMaterials =
                showOutline ? outlinedMats[i] : plainMats[i];
        }

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"FruitOutlineListener:\t{name} outlined: {isOutlined}",
                this
            );
        }
    }
}
