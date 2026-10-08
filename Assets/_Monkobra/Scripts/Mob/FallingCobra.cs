using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class FallingCobra: MonoBehaviour {
    [Header("Warning and Fall")]
    [SerializeField] private float warningDurationS = 1.25f;
    [SerializeField] private Color warningColor =
        new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] private float warningPulseHz = 4f;
    [SerializeField] private float fallSpeedU = 6f;
    [SerializeField] private float segmentSpacingU = 0.35f;
    [SerializeField] private float hitRadiusU = 0.5f;
    [SerializeField] private float despawnBelowPlayerU = 5f;

    private static readonly int BASE_COLOR_ID =
        Shader.PropertyToID("_BaseColor");

    private Rigidbody body;
    private CapsuleCollider hitCollider;
    private Renderer[] snakeRenderers;
    private Color[] baseColors;
    private MaterialPropertyBlock warningBlock;
    private Transform player;
    private float fallStartTime;
    private bool isFalling;
    private bool hitMonkey;

    private void Awake() {
        body = GetComponent<Rigidbody>();
        hitCollider = GetComponent<CapsuleCollider>();
        if (body == null || hitCollider == null) {
            Debug.LogError(
                "FallingCobra:\trequires Rigidbody and CapsuleCollider",
                this
            );
            enabled = false;
            return;
        }

        body.isKinematic = true;
        body.useGravity = false;

        float highestSegmentY = 0f;
        for (int i = 0; i < transform.childCount; i++) {
            Transform segment = transform.GetChild(i);
            if (segment.name.StartsWith("Segment_")) {
                segment.localPosition =
                    Vector3.up * (i * segmentSpacingU);
                highestSegmentY = Mathf.Max(
                    highestSegmentY,
                    segment.localPosition.y
                );
            }
        }

        // One trigger covers the whole snake, so one collision creates one
        // branch-hit event and camera impulse instead of one per segment.
        foreach (Collider segmentCollider in GetComponentsInChildren<Collider>()) {
            if (segmentCollider != hitCollider) {
                segmentCollider.enabled = false;
            }
        }
        hitCollider.direction = 1;
        hitCollider.radius = hitRadiusU;
        hitCollider.height = highestSegmentY + hitRadiusU * 2f;
        hitCollider.center = Vector3.up * highestSegmentY * 0.5f;
        hitCollider.isTrigger = true;
        hitCollider.enabled = false;

        snakeRenderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[snakeRenderers.Length];
        warningBlock = new MaterialPropertyBlock();

        for (int i = 0; i < snakeRenderers.Length; i++) {
            Material material = snakeRenderers[i].sharedMaterial;
            baseColors[i] =
                material != null && material.HasProperty(BASE_COLOR_ID)
                    ? material.GetColor(BASE_COLOR_ID)
                    : Color.white;
        }
    }

    private void Start() {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) {
            player = playerObject.transform;
        }

        fallStartTime = Time.time + warningDurationS;
    }

    private void Update() {
        if (hitMonkey && hitCollider.enabled) {
            hitCollider.enabled = false;
        }

        if (isFalling) {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(
            Time.time * warningPulseHz * 2f * Mathf.PI
        );

        for (int i = 0; i < snakeRenderers.Length; i++) {
            Renderer snakeRenderer = snakeRenderers[i];
            snakeRenderer.GetPropertyBlock(warningBlock);
            warningBlock.SetColor(
                BASE_COLOR_ID,
                Color.Lerp(baseColors[i], warningColor, pulse)
            );
            snakeRenderer.SetPropertyBlock(warningBlock);
        }
    }

    private void FixedUpdate() {
        if (Time.time < fallStartTime) {
            return;
        }

        if (!isFalling) {
            isFalling = true;

            foreach (Renderer snakeRenderer in snakeRenderers) {
                snakeRenderer.SetPropertyBlock(null);
            }

            hitCollider.enabled = true;
        }

        body.MovePosition(
            body.position
                + Vector3.down * fallSpeedU * Time.fixedDeltaTime
        );

        if (
            player != null
            && body.position.y
                < player.position.y - despawnBelowPlayerU
        ) {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (
            other.CompareTag("Player")
            || other.GetComponent<HitBranchDetection>() != null
        ) {
            // The monkey's detector handles the drop. Stop later contacts
            // after this physics step, once its own trigger callback has run.
            hitMonkey = true;
        }
    }

    private void OnValidate() {
        warningDurationS = Mathf.Max(0f, warningDurationS);
        warningPulseHz = Mathf.Max(0f, warningPulseHz);
        fallSpeedU = Mathf.Max(0f, fallSpeedU);
        segmentSpacingU = Mathf.Max(0f, segmentSpacingU);
        hitRadiusU = Mathf.Max(0.01f, hitRadiusU);
        despawnBelowPlayerU = Mathf.Max(0f, despawnBelowPlayerU);
    }
}
