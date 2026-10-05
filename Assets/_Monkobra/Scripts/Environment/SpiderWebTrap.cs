using UnityEngine;

/// <summary>
/// Prefab root of one spider web. The strands carry no colliders: a single
/// trigger <see cref="BoxCollider"/> on this root covers the whole web and
/// reaches outward along local +X, the web's face normal, far enough to
/// touch the climbing monkey's body.
/// <para>
/// The monkey's solid body touching the trigger hands this web to its
/// <see cref="SpiderWebStruggle"/>, which decides whether it traps. The
/// struggle pulses the web on each escape press, and releases it, removing
/// it, once the monkey breaks free.
/// </para>
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class SpiderWebTrap: MonoBehaviour {
    // Public Methods  #########################################################
    /// <summary>
    /// Plays a short squash and twist, visual feedback for 1 escape press.
    /// The rest pose is captured only between pulses, so rapid presses
    /// restart the pulse without ever drifting it.
    /// </summary>
    public void Pulse() {
        if (pulseTimeLeftS <= 0f) {
            restScale = transform.localScale;
            restRotation = transform.localRotation;
        }
        pulseTimeLeftS = pulseDurationS;
    }

    /// <summary>removes the web once the monkey breaks free of it</summary>
    public void Release() {
        // stop catching now, Destroy only lands at the end of the frame
        if (trigger != null) {
            trigger.enabled = false;
        }
        Destroy(gameObject);
    }

    // Inspector Fields  #######################################################
    [Header("Escape Press Pulse")]
    [SerializeField]
    [Range(0f, 0.5f)]
    [Tooltip("how far the web face squashes on each escape press; 0~1")]
    private float pulseScale = 0.1f;

    [SerializeField]
    [Range(0f, 30f)]
    [Tooltip("how far the web twists about its face normal per press; deg")]
    private float pulseTwistDeg = 6f;

    [SerializeField]
    [Tooltip("length of 1 escape press pulse; s")]
    private float pulseDurationS = 0.15f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        trigger = GetComponent<BoxCollider>();
        if (trigger != null && !trigger.isTrigger) {
            Debug.LogWarning(
                "SpiderWebTrap:\tBoxCollider must be Is Trigger, switching "
                    + "it on",
                this
            );
            trigger.isTrigger = true;
        }
    }

    private void Update() {
        if (pulseTimeLeftS <= 0f) {
            return;
        }

        pulseTimeLeftS = Mathf.Max(0f, pulseTimeLeftS - Time.deltaTime);
        // t runs 0 to 1 over the pulse, both curves end back at rest
        float t = 1f - pulseTimeLeftS / pulseDurationS;

        // squash the face only, local Y and Z, X is the normal into the bark
        float faceScale = 1f - pulseScale * Mathf.Sin(t * Mathf.PI);
        transform.localScale = new Vector3(
            restScale.x,
            restScale.y * faceScale,
            restScale.z * faceScale
        );

        // twist about the face normal, a full sine swings it both ways
        float twistDeg = pulseTwistDeg * Mathf.Sin(t * 2f * Mathf.PI);
        transform.localRotation =
            restRotation * Quaternion.Euler(twistDeg, 0f, 0f);
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        pulseDurationS = Mathf.Max(0.01f, pulseDurationS);
    }

    // Event Handlers  #########################################################
    private void OnTriggerEnter(Collider other) {
        TryCatch(other);
    }

    // a monkey still inside when its post-escape protection ends is caught
    // then, Enter alone would have let it through
    private void OnTriggerStay(Collider other) {
        TryCatch(other);
    }

    // private members  ########################################################
    private float pulseTimeLeftS;
    private Vector3 restScale = Vector3.one;
    private Quaternion restRotation = Quaternion.identity;

    // cached references  ------------------------------------------------------
    private BoxCollider trigger;

    // private methods  ########################################################
    // only the monkey's solid body counts: its trigger volumes (fruit zones,
    // branch sensor) reach metres past it, and an arm has its own Rigidbody
    // without a struggle, so a hand brushing the web is ignored as well
    private void TryCatch(Collider other) {
        if (other.isTrigger) {
            return;
        }

        Rigidbody otherBody = other.attachedRigidbody;
        if (
            otherBody == null
            || !otherBody.TryGetComponent(out SpiderWebStruggle struggle)
        ) {
            return;
        }

        struggle.TryTrap(this);
    }
}
