using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Cobra that coils round the trunk and climbs after the monkey without ever
/// stopping or backing down, so once it reaches the monkey, whatever part
/// touches first ends the run. The body
/// wraps at least one full loop as a corkscrew with no gap round the trunk, so
/// a monkey dropping onto the cobra's height cannot slip past it by orbiting.
/// Segments are laid along the coil as a chain of rounded beads with small
/// gaps, slightly tapered, with the neck raised,
/// a slow travelling slither wave and eyes on the head. The body keeps its
/// material's own color.
/// Contact itself is handled by <see cref="CobraCollisions"/>.
/// </summary>
public class CobraClimb: MonoBehaviour {
    // Inspector Fields  #######################################################
    [Header("References")]
    [SerializeField]
    [Tooltip("trunk axis the cobra orbits, its y is the path's zero height")]
    private Transform pathCenter;

    [SerializeField]
    [Tooltip(
        "body segments, head first; missing ones are cloned from the last "
            + "to fill the coil"
    )]
    private Transform[] bodySegments;

    [Header("Tree Path")]
    [SerializeField]
    [Tooltip("distance from path center to the body; U")]
    private float orbitRadius = 5.8f;

    [SerializeField]
    [Tooltip("head height above path center at start; U")]
    private float startHeight = 0.5f;

    [SerializeField]
    [Tooltip("head climb speed; U/s")]
    private float climbSpeed = 0.25f;

    [SerializeField]
    [Tooltip("head orbit speed round the trunk; deg/s")]
    private float orbitSpeedDegrees = 20f;

    [SerializeField]
    [Tooltip("orbit direction seen from above")]
    private bool clockwise = true;

    [Header("Coil Shape")]
    [SerializeField]
    [Tooltip(
        "full loops the body wraps round the trunk, head to tail; "
            + "keep at least 1 so no gap is left"
    )]
    private float coilTurns = 1.25f;

    [SerializeField]
    [Tooltip("height the body rises per full loop, head on top; U")]
    private float coilPitchU = 2.5f;

    [SerializeField]
    [Tooltip(
        "largest gap allowed between neighbouring segments along the coil; "
            + "more segments are cloned until it holds; U"
    )]
    private float maxSegmentGapU = 0.6f;

    [Header("Body Look")]
    [SerializeField]
    [Tooltip("body thickness at its thickest point; U")]
    private float maxThicknessU = 0.5f;

    [SerializeField]
    [Tooltip(
        "thickness along the body as a share of max thickness, "
            + "0 = head, 1 = tail tip"
    )]
    private AnimationCurve thicknessProfile = new AnimationCurve(
        new Keyframe(0f, 0.95f),
        new Keyframe(0.1f, 0.9f),
        new Keyframe(0.4f, 1f),
        new Keyframe(1f, 0.75f)
    );

    [SerializeField]
    [Tooltip(
        "how much of the spacing between segments each one fills; below 1 "
            + "leaves a visible gap, the hit shape still has none"
    )]
    private float segmentFill = 0.8f;

    [SerializeField]
    [Tooltip("head length; U")]
    private float headLengthU = 0.75f;

    [SerializeField]
    [Tooltip("head width as a multiple of its thickness, cobra-hood flat")]
    private float headWidthRatio = 1.3f;

    [Header("Neck & Slither")]
    [SerializeField]
    [Tooltip("body length from the head that rises off the coil; U")]
    private float neckLengthU = 2f;

    [SerializeField]
    [Tooltip(
        "how far the head rises above the coil; the coil sits this much "
            + "lower, so the head still stops at the player's height; U"
    )]
    private float neckLiftU = 0.8f;

    [SerializeField]
    [Tooltip("up-down slither wave height, faded out at the neck; U")]
    private float waveAmplitudeU = 0.12f;

    [SerializeField]
    [Tooltip("body length of one slither wave; U")]
    private float waveLengthU = 2.5f;

    [SerializeField]
    [Tooltip("slither waves passing down the body; 1/s")]
    private float waveSpeedHz = 0.4f;

    [Header("Eyes")]
    [SerializeField]
    [Tooltip("eye diameter, eyes are skipped at 0; U")]
    private float eyeSizeU = 0.14f;

    [Header("Closing In")]
    [SerializeField]
    [FormerlySerializedAs("limitToPlayerHeight")]
    [Tooltip(
        "slow the climb as the coil nears the player, it still never stops"
    )]
    private bool slowNearPlayer = true;

    [SerializeField]
    [Tooltip("coil starts slowing within this distance below the player; U")]
    private float slowDownDistance = 2f;

    [SerializeField]
    [Tooltip("share of climb speed kept when the coil is right below")]
    private float minimumSpeedShare = 0.15f;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        // Inspector Assignment Guard  -----------------------------------------
        if (pathCenter == null) {
            Debug.LogError(
                "CobraClimb:\tmust assign Inspector Field: pathCenter",
                this
            );
        }

        if (bodySegments == null || bodySegments.Length == 0) {
            Debug.LogError(
                "CobraClimb:\tmust assign Inspector Field: bodySegments",
                this
            );
        }

        if (pathCenter == null || bodySegments == null
            || bodySegments.Length == 0) {
            enabled = false;
            return;
        }

        FillCoilSegments();
        segmentPositions = new Vector3[bodySegments.Length];
        StyleBody();
    }

    private void Start() {
        if (!enabled) {
            return;
        }

        headHeight = startHeight;
        UpdateBody();
    }

    private void Update() {
        if (player == null) {
            GameObject playerObject = GameObject.FindGameObjectWithTag(
                "Player"
            );

            if (playerObject != null) {
                player = playerObject.transform;
            }
        }

        headAngle += Direction * orbitSpeedDegrees * Time.deltaTime;

        float currentClimbSpeed = climbSpeed;

        if (slowNearPlayer && player != null) {
            float coilTopHeight = headHeight - neckLiftU;
            float distanceBelowPlayer =
                player.position.y - pathCenter.position.y - coilTopHeight;

            if (distanceBelowPlayer > 0f
                && distanceBelowPlayer < slowDownDistance) {
                // closing in: slow down for suspense, but never stop
                currentClimbSpeed *= Mathf.Lerp(
                    minimumSpeedShare,
                    1f,
                    distanceBelowPlayer / slowDownDistance
                );
            }
        }

        // always up, never held at the player's height and never down: the
        // coil climbs on into the monkey, and a monkey moving or dropping
        // down runs into it instead of pushing it
        headHeight += currentClimbSpeed * Time.deltaTime;

        UpdateBody();
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        orbitRadius = Mathf.Max(0.01f, orbitRadius);
        climbSpeed = Mathf.Max(0f, climbSpeed);
        orbitSpeedDegrees = Mathf.Max(0f, orbitSpeedDegrees);
        coilTurns = Mathf.Max(1f, coilTurns);
        coilPitchU = Mathf.Max(0f, coilPitchU);
        maxSegmentGapU = Mathf.Max(0.05f, maxSegmentGapU);
        maxThicknessU = Mathf.Max(0.01f, maxThicknessU);
        segmentFill = Mathf.Clamp(segmentFill, 0.2f, 2f);
        headLengthU = Mathf.Max(0.01f, headLengthU);
        headWidthRatio = Mathf.Max(0.1f, headWidthRatio);
        neckLengthU = Mathf.Max(0.01f, neckLengthU);
        neckLiftU = Mathf.Max(0f, neckLiftU);
        waveAmplitudeU = Mathf.Max(0f, waveAmplitudeU);
        waveLengthU = Mathf.Max(0.01f, waveLengthU);
        eyeSizeU = Mathf.Max(0f, eyeSizeU);
        slowDownDistance = Mathf.Max(0.01f, slowDownDistance);
        minimumSpeedShare = Mathf.Clamp01(minimumSpeedShare);
    }

    // private members  ########################################################
    private static readonly int BASE_COLOR_ID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int COLOR_ID = Shader.PropertyToID("_Color");

    private float headAngle;
    private float headHeight;
    private Transform player;
    private Vector3[] segmentPositions;

    private float Direction => clockwise ? -1f : 1f;

    private int LastIndex => Mathf.Max(1, bodySegments.Length - 1);

    /// <returns>angle between neighbouring segments so the whole body spans
    /// <see cref="coilTurns"/> loops; deg</returns>
    private float SegmentAngleStepDeg => coilTurns * 360f / LastIndex;

    /// <returns>height drop between neighbouring segments; U</returns>
    private float SegmentHeightStepU => coilTurns * coilPitchU / LastIndex;

    /// <returns>body length of the whole coil, head to tail; U</returns>
    private float CoilLengthU => Mathf.Sqrt(
        Mathf.Pow(coilTurns * 2f * Mathf.PI * orbitRadius, 2f)
            + Mathf.Pow(coilTurns * coilPitchU, 2f)
    );

    /// <returns>body length between neighbouring segments; U</returns>
    private float SegmentArcStepU => CoilLengthU / LastIndex;

    // private methods  ########################################################
    /// <summary>
    /// Clone the last body segment until neighbouring segments sit no more
    /// than <see cref="maxSegmentGapU"/> apart along the whole coil, so the
    /// loop round the trunk has no hole the monkey fits through.
    /// </summary>
    private void FillCoilSegments() {
        int neededCount = Mathf.CeilToInt(CoilLengthU / maxSegmentGapU) + 1;

        if (neededCount <= bodySegments.Length) {
            return;
        }

        Transform template = bodySegments[bodySegments.Length - 1];

        if (template == null) {
            Debug.LogError(
                "CobraClimb:\tlast body segment is empty, cannot fill coil",
                this
            );
            return;
        }

        int authoredCount = bodySegments.Length;
        Transform[] filledSegments = new Transform[neededCount];
        bodySegments.CopyTo(filledSegments, 0);

        for (int i = authoredCount; i < neededCount; i++) {
            Transform clone = Instantiate(template, template.parent);
            clone.name = $"Segment_{i:00}";
            filledSegments[i] = clone;
        }

        bodySegments = filledSegments;

        if (Debug.isDebugBuild) {
            Debug.Log(
                $"CobraClimb:\tcloned {neededCount - authoredCount} segments, "
                    + $"{neededCount} in total for {coilTurns:F2} loops",
                this
            );
        }
    }

    /// <summary>
    /// Size every segment into a rounded bead, slightly tapered, flatten the
    /// head into a hood and give the head eyes. Each bead's capsule collider
    /// is stretched to reach its neighbours, so the visible gaps never become
    /// holes in the hit shape. Runs once, the shape along the body never
    /// changes. Colors are left to the segments' material.
    /// </summary>
    private void StyleBody() {
        float arcStep = SegmentArcStepU;
        float segmentLength = arcStep * segmentFill;
        MaterialPropertyBlock colorBlock = new();

        for (int i = 0; i < bodySegments.Length; i++) {
            Transform segment = bodySegments[i];

            if (segment == null) {
                continue;
            }

            float bodyFraction = (float)i / LastIndex;
            float thickness =
                maxThicknessU * Mathf.Max(
                    0.05f,
                    thicknessProfile.Evaluate(bodyFraction)
                );

            // capsule mesh: local y runs along the body, 2 U long at scale 1,
            // local x is width, local z is depth off the trunk
            if (i == 0) {
                segment.localScale = new Vector3(
                    thickness * headWidthRatio,
                    headLengthU * 0.5f,
                    thickness * 0.75f
                );
            } else {
                segment.localScale = new Vector3(
                    thickness,
                    segmentLength * 0.5f,
                    thickness
                );
            }

            StretchHitShape(segment, arcStep);
        }

        if (eyeSizeU > 0f && bodySegments[0] != null) {
            AddEyes(bodySegments[0], colorBlock);
        }
    }

    /// <summary>
    /// Make a segment's capsule collider run along the body and span a bit
    /// more than the spacing to the next segment, whatever its visible size.
    /// </summary>
    private static void StretchHitShape(Transform segment, float arcStep) {
        if (!segment.TryGetComponent(out CapsuleCollider hitShape)) {
            return;
        }

        float lengthScale = Mathf.Max(0.0001f, segment.localScale.y);
        hitShape.direction = 1;
        hitShape.center = Vector3.zero;
        hitShape.height = Mathf.Max(
            2f * hitShape.radius,
            arcStep * 1.1f / lengthScale
        );
    }

    private void AddEyes(Transform head, MaterialPropertyBlock colorBlock) {
        Vector3 headScale = head.localScale;
        Renderer headRenderer = head.GetComponent<Renderer>();

        for (int side = -1; side <= 1; side += 2) {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = side < 0 ? "EyeLeft" : "EyeRight";

            // eyes are looks only, never part of the hit shape
            Destroy(eye.GetComponent<Collider>());

            eye.transform.SetParent(head, false);

            // head-local: toward the front (+y), off to each side (x), on the
            // face turned away from the trunk (-z)
            eye.transform.localPosition = new Vector3(
                side * 0.32f,
                0.55f,
                -0.3f
            );
            eye.transform.localScale = new Vector3(
                eyeSizeU / headScale.x,
                eyeSizeU / headScale.y,
                eyeSizeU / headScale.z
            );

            Renderer eyeRenderer = eye.GetComponent<Renderer>();

            if (headRenderer != null) {
                eyeRenderer.sharedMaterial = headRenderer.sharedMaterial;
            }

            SetColor(eyeRenderer, Color.black, colorBlock);
        }
    }

    private static void SetColor(
        Renderer target,
        Color color,
        MaterialPropertyBlock colorBlock
    ) {
        if (target == null || target.sharedMaterial == null) {
            return;
        }

        Material material = target.sharedMaterial;
        target.GetPropertyBlock(colorBlock);

        if (material.HasProperty(BASE_COLOR_ID)) {
            colorBlock.SetColor(BASE_COLOR_ID, color);
        } else if (material.HasProperty(COLOR_ID)) {
            colorBlock.SetColor(COLOR_ID, color);
        } else {
            return;
        }

        target.SetPropertyBlock(colorBlock);
        colorBlock.Clear();
    }

    private void UpdateBody() {
        float angleStep = SegmentAngleStepDeg;
        float heightStep = SegmentHeightStepU;
        float arcStep = SegmentArcStepU;
        float wavePhase = Time.time * waveSpeedHz * 2f * Mathf.PI;

        // the coil sits lower by the neck lift, so the raised head tip is
        // what reaches headHeight
        float coilTopHeight = headHeight - neckLiftU;

        for (int i = 0; i < bodySegments.Length; i++) {
            float bodyDistance = i * arcStep;
            float neckFraction = Mathf.Clamp01(bodyDistance / neckLengthU);

            float lift = neckLiftU * (1f - neckFraction) * (1f - neckFraction);
            float wave = waveAmplitudeU * neckFraction * Mathf.Sin(
                wavePhase - bodyDistance / waveLengthU * 2f * Mathf.PI
            );

            segmentPositions[i] = GetPathPosition(
                headAngle - Direction * angleStep * i,
                coilTopHeight - heightStep * i + lift + wave
            );
        }

        for (int i = 0; i < bodySegments.Length; i++) {
            Transform segment = bodySegments[i];

            if (segment == null) {
                continue;
            }

            segment.position = segmentPositions[i];

            if (bodySegments.Length < 2) {
                continue;
            }

            // lay the capsule along the body, pointing toward the head
            Vector3 alongBody = i == 0
                ? segmentPositions[0] - segmentPositions[1]
                : segmentPositions[i - 1] - segmentPositions[i];
            Vector3 awayFromTrunk = segmentPositions[i] - pathCenter.position;
            awayFromTrunk.y = 0f;

            if (alongBody.sqrMagnitude < 0.0001f
                || awayFromTrunk.sqrMagnitude < 0.0001f) {
                continue;
            }

            segment.rotation =
                Quaternion.LookRotation(alongBody, awayFromTrunk)
                * Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private Vector3 GetPathPosition(float angleDegrees, float height) {
        float radians = angleDegrees * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(radians) * orbitRadius,
            height,
            Mathf.Sin(radians) * orbitRadius
        );

        return pathCenter.position + offset;
    }
}
