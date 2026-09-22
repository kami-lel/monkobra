using UnityEngine;

public class MonkeyPrefabRoot: MonoBehaviour {
    // Inspector Fields  #######################################################
    [SerializeField]
    private Transform treeParent;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (treeParent == null) {
            Debug.LogWarning("must assign Inspector Field: treeParent", this);
        }
    }

    private void Start() {

    }

    private void Update() {

    }
}
