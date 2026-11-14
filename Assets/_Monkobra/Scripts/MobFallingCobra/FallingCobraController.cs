using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cobra hanging dormant on the tree that drops on the monkey. Spawned as a
/// child of a tree segment by <see cref="FallingCobraSpawner"/>, which hands
/// it the balance config and the tree axis through
/// <see cref="Initialize"/>.
/// <para>
/// Three states. Dormant: nothing moves until the monkey comes within
/// <c>triggerDistanceU</c> (3D), or within the farther
/// <see cref="CobraBalanceConfig.FallingCobraReachWakeDistanceU"/> while an
/// arm reaches for a banana. It can notice and watch a monkey passing above it.
/// Warning: it raises its head, watches the monkey and hisses in its normal green color.
/// After a fixed head-up warning it attacks, even if the monkey stays distant or retreats. Falling: a
/// short downward pursuit that follows the monkey within its tracking range.
/// The head rests against the bark and physical contacts follow the visible snake. Any part of
/// the snake touching any part of the monkey (body, tail, hand, or its
/// branch sensor) applies one branch-hit drop and immediately switches the
/// snake off. It cannot keep falling or strike the monkey again. After
/// <c>maximumFallDurationS</c> or <c>maxFallDistanceU</c> it switches itself off; the segment's restart
/// destroys it later.
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class FallingCobraController: MonoBehaviour {
    // Public Members  #########################################################
    /// <returns>pivot to the far end of the snake, hit radius included; u
    /// </returns>
    public float LengthU => animatedReferenceMesh!=null?smoothBodyLengthU:hitCollider.height;

    /// <returns>how far the snake's tail reaches below its pivot; u</returns>
    public float TailBelowPivotU => animatedReferenceMesh != null ? referenceHeadBelowPivotU : hitRadiusU;

    // Public Methods  #########################################################
    /// <summary>
    /// Arms the cobra. The ring it slides on is the horizontal distance from
    /// its current position to <paramref name="treeAxisPosition"/>, so call it
    /// once the cobra stands where it will hang.
    /// </summary>
    /// <param name="config">balance tuning; fall and slide speeds</param>
    /// <param name="treeAxisPosition">any world point on the trunk axis
    /// </param>
    public void Initialize(
        CobraBalanceConfig config,
        Vector3 treeAxisPosition
    ) {
        if (config == null) {
            Debug.LogWarning(
                "FallingCobraController:\tInitialize needs a CobraBalanceConfig, stay "
                    + "dormant",
                this
            );
            return;
        }

        fallSpeedU = config.FallingCobraFallSpeedU;
        horizontalSpeedU = config.FallingCobraHorizontalSpeedU;
        reachWakeDistanceU = config.FallingCobraReachWakeDistanceU;
        attackDistanceU = config.FallingCobraAttackDistanceU;
        axisXZ = new Vector2(treeAxisPosition.x, treeAxisPosition.z);
        Vector3 position = transform.position;
        ringRadiusU = Vector2.Distance(
            new Vector2(position.x, position.z),
            axisXZ
        );
        isInitialized = true;
        AlignReferenceToTree();
    }

    // Inspector Fields  #######################################################
    [Header("Trigger")]
    [SerializeField]
    [Tooltip("3D distance to the monkey below which the cobra wakes; u")]
    private float triggerDistanceU = 12f;

    [Header("Watch and Fall")]
    [SerializeField]
    [Tooltip("head-up hiss duration after spotting the monkey, before attacking; s")]
    private float warningDurationS = 2.5f;

    [SerializeField, Min(0f)]
    private float alertExitPaddingU = 3f;

    [Header("Watch Hiss")]
    [SerializeField]
    private AudioClip warningHiss;
    [SerializeField, Range(0f, 1f)]
    private float warningHissVolume = .3f;
    [SerializeField, Min(.1f)]
    private float warningHissIntervalS = 2.4f;
    [SerializeField, Min(.1f)]
    private float warningHissMinDistanceU = 3f;

    [Header("Watch Head Pose")]
    [SerializeField, Range(0f, 70f)]
    private float waitingLookDownDegrees = 35f;
    [SerializeField, Min(0f)]
    private float waitingHeadLiftU = .7f;
    [SerializeField, Min(.1f)]
    private float standingFaceHeightU = 2.2f;
    [SerializeField, Min(.1f)]
    private float standingNeckLengthU = 1.8f;
    [SerializeField, Min(0f)]
    private float standingChestLiftU = .4f;
    [SerializeField, Min(1f)]
    private float bodyTwistDegreesPerU = 480f;
    [SerializeField, Range(0f, 89f)]
    private float maximumSupportBellyAngle = 75f;
    [SerializeField, Min(.1f)]
    private float standingBaseHeightU = 1f;
    [SerializeField, Min(.1f)]
    private float standingCoilOuterRadiusU = .8f;
    [SerializeField, Min(.05f)]
    private float standingCoilInnerRadiusU = .35f;
    [SerializeField, Min(.5f)]
    private float standingCoilTurns = 1.1f;
    [Header("Branch Perch")]
    [SerializeField, Min(.1f)] private float branchCoilSpanU = 1.8f;
    [SerializeField, Min(.5f)] private float branchCoilTurns = 3f;
    [SerializeField, Min(.1f)] private float sittingCoilRadiusAlongU = 1.05f;
    [SerializeField, Min(.1f)] private float sittingCoilRadiusAcrossU = .6f;
    [SerializeField, Range(.1f, .9f)] private float sittingCoilInnerScale = .35f;
    [SerializeField, Min(.1f)] private float sittingCoilLayerRiseU = .36f;
    [SerializeField, Min(0f)] private float branchSkinClearanceU = .01f;
    [SerializeField, Min(0f)] private float branchHeadForwardU = .25f;
    [SerializeField, Min(0f)] private float hoodNeckSetbackU = .24f;
    [SerializeField, Min(.1f)] private float hoodNeckClearanceLengthU = .65f;
    private BoxCollider supportBranch;
    private bool perchBound;
    private Vector3 branchCenter, branchAxis, branchUp, branchSide;
    private float branchHalfLength, branchHalfHeight, branchHalfWidth, branchAnchorAlong;
    private Bounds originalHeadBounds;
    [SerializeField, Min(0f)] private float branchEscapeClearanceU = .12f;
    private float branchEscapeSide = 1f;
    [SerializeField, Min(0f)] private float branchHeadSideU = 1.1f;
    private float branchHeadSideSign = 1f;
    [SerializeField, Min(.1f)] private float perchedAimSharpness = 7f;
    [SerializeField, Range(.6f,1f)] private float minimumNeckThicknessShare = .85f;
    [SerializeField, Min(0f)] private float freeNeckSwayU = .12f;
    [SerializeField, Min(.1f)] private float bodyBarkSettleDropU = 1.2f;
    [SerializeField, Min(.001f)] private float bodyBarkContactFeatherU = .05f;
    private float perchedAimSide = 1f,perchedCoilPhaseDegrees;
    private Vector3[] roundedRingCenters,originalRingCenters;
    private Vector2[] neckTextureUV;
    private Vector3[] neckSourceNormals;
    private float headNeckRadiusU;
    [SerializeField, Min(.1f)]
    private float standReleaseDurationS = .7f;
    [SerializeField, Min(0f)]
    private float standReleaseReachU = .15f;
    [SerializeField, Min(.1f)]
    private float headPoseSharpness = 4f;

    private Quaternion originalHeadFaceBasis, standingHeadRotation;
    private Vector3 originalHeadFaceCenter, releaseTrailOffset;
    private float releaseStandingBlend;
    private float[] spineContact;
    private const float NeckOverlapU = .006f;
    [SerializeField, Min(.1f)] private float neckCollarLengthU = .75f;
    private const float HeadCollarBlendU = .4f;
    private AudioSource hissSource;
    private float nextHissTime, attackDistanceU, waitingHeadBlend, lastHeadPoseTime;

    [SerializeField]
    [Tooltip("attack travel distance b4 the cobra switches itself off; u")]
    private float maxFallDistanceU = 30f;
    [SerializeField, Min(0.1f)]
    private float maximumFallDurationS = 2.5f;
    [SerializeField, Min(0.01f)]
    private float attackSpeedRampS = 0.45f;

    [SerializeField, Min(0.1f)]
    private float attackSideBrakeDistanceU = 0.8f;

    [SerializeField, Min(0.1f)]
    private float treeSurfaceRadiusU = 5f;

    [SerializeField, Min(0f)]
    private float treeSurfaceClearanceU = 0.02f;

    [SerializeField, Range(0f, 0.12f)]
    private float bellyPressDepthU = 0.06f;

    private float attackStartedTime;
    [Header("Attack Animation")]
    [SerializeField, Min(.05f)] private float strikeWindupDurationS = .42f;
    [SerializeField, Min(0f)] private float strikePullbackU = .36f;
    [SerializeField, Min(0f)] private float strikeCompressionU = .08f;
    [SerializeField, Min(1f)] private float strikePoseSharpness = 22f;
    [SerializeField, Min(.02f)] private float strikeLaunchRampS = .09f;
    [SerializeField, Min(.05f)] private float strikeBurstDurationS = .38f;
    [SerializeField, Min(1f)] private float strikeSpeedMultiplier = 1.6f;
    [SerializeField, Range(.2f,1f)] private float strikeHeadFollowScale = .5f;
    private float strikeWindupBlend;
    private Vector3 strikeWindupOffset,launchWindupOffset;
    [SerializeField, Min(.01f)] private float headTrailSmoothTimeS = .16f;
    private Vector3 displayedTrailHead, displayedTrailVelocity;
    private float releaseHeadHeight, launchTrailDistance, launchNeckArc;
    [SerializeField, Min(.2f)] private float headTurnDropU = 1.25f;
    private float committedTargetAngle;
    private Vector3 committedTargetPosition;
    [Header("Live head tracking")]
    [SerializeField, Min(1f)] private float headTrackingRangeU = 10f;
    [SerializeField,Min(0f)] private float headTrackingExitPaddingU=.75f;
    [SerializeField,Min(30f)] private float headLookTurnSpeedDegrees=300f;
    private Quaternion shownHeadRotation;
    private bool headRotationReady;
    [SerializeField, Range(0f,35f)] private float flatHeadPitchDegrees = 16f;
    [SerializeField, Range(0f,70f)] private float upwardHeadPitchDegrees = 55f;
    [SerializeField, Range(0f,45f)] private float surfaceHeadTurnDegrees = 25f;
    [SerializeField, Min(0f)] private float headBarkGapU = .015f;
    [SerializeField, Min(.1f)] private float headContactRadiusU = .42f;
    [SerializeField, Range(8,40)] private int bodyContactSections = 24;
    [SerializeField, Min(.1f)] private float steadyAimDistanceU = 2f;
    [SerializeField,Range(0f,1f)] private float closeAimSwayScale=.1f;
    [SerializeField,Min(.1f)] private float headSteeringSharpness=18f;
    [SerializeField,Range(.2f,1f)] private float minimumApproachFallScale=.45f;
    [SerializeField,Min(1f)] private float approachDropRangeU=3f;
    private float approachFallScale=1f;
    [SerializeField,Min(.1f)] private float headAimGain=6f;
    [SerializeField,Min(0f)] private float headAimDamping=1f;
    private float headAimCommand,previousFeedbackAngle,headAngularSpeed;
    private bool headFeedbackReady;
    [SerializeField, Range(45f, 180f)] private float trackingSurfaceTurnDegrees = 160f;
    [SerializeField, Min(.01f)] private float targetHeightStopMarginU = .15f;
    [SerializeField, Min(.1f)] private float verticalArrivalGain = 3f;
    [SerializeField, Min(0f)] private float maximumStrikeReachU = .65f;
    [SerializeField, Min(.1f)] private float strikeReachDistanceU = 2f;
    private bool descentStopped;
    private float stoppedTrailHeight;
    private bool pursuitPaused;
    private float[] launchBodyOffsets;
    private float launchBodyArc;
    [SerializeField, Min(.05f)] private float uncoilSupportBlendU = .4f;
    private bool monkeyInHeadRange;
    private Transform cachedAimOwner;
    private Collider playerBodyCollider;
    private float headSteeringBias;
    private Vector3 supportDirection,previousHeadContact;
    private float supportMinimum;
    private bool supportCached,headContactReady;
    private CapsuleCollider[] visibleBodyContacts;
    private readonly Collider[] sweptHeadHits = new Collider[32];
    [Header("Eye gaze anchors")]
    [SerializeField] private bool eyeAnchorsReady;
    [SerializeField] private Vector3 leftEyeSurfaceLocal, rightEyeSurfaceLocal;
    [SerializeField] private Vector3 leftEyeNormalLocal=Vector3.forward, rightEyeNormalLocal=Vector3.forward;
    [SerializeField, Min(.01f)] private float eyeWhiteRadiusU=.13f,eyePupilRadiusU=.075f;
    private Transform[] eyePupils;
    private Material eyeWhiteMaterial,eyePupilMaterial;
    private Mesh eyePupilMesh;
    private bool hasCommittedTarget;
    private Renderer referenceRenderer;
    private MaterialPropertyBlock surfaceBlock;

    [Header("Body")]
    [SerializeField]
    [Tooltip("gap b/t body segments; u")]
    private float segmentSpacingU = 0.35f;

    [SerializeField]
    [Tooltip("hit radius of each segment; u")]
    private float hitRadiusU = 0.5f;

    [Header("Cartoon Appearance")]
    [SerializeField]
    private CartoonCobraVisual.Settings cartoonAppearance = new();

    [Header("Falling Snake Visual")]
    [SerializeField, Min(0.1f)]
    private float referenceSizeMultiplier = 1.5f;

    [SerializeField]
    private Vector3 referenceFaceDirection = new Vector3(-0.435f, -0.265f, -0.861f);

    [SerializeField, Min(0f)]
    private float bodyWaveSpeedHz = 1f;

    [SerializeField, Min(0.1f)]
    private float bodyWaveCycles = 0.8f;

    [SerializeField, Min(0.1f)]
    private float swimTurnSharpness = 8f;

    [SerializeField, Min(0.1f)]
    private float smoothBodyLengthU = 15.2f;

    [SerializeField, Min(0.01f)]
    private float smoothBodyRadiusU = 0.24f;

    [SerializeField, Range(0f, 55f)]
    private float bodyBendDegrees = 38f;

    [Header("Head-led Surface Swim")]
    [SerializeField, Min(0f)]
    private float headSwayAmplitudeU = 0.26f;

    [SerializeField, Min(0.5f)]
    private float swimWavelengthU = 3.6f;

    [SerializeField, Min(0.01f)]
    private float headSwayEntryDistanceU = 1.8f;
    [SerializeField, Range(0f, 1f)]
    private float headGazeStabilization = 0.55f;

    [SerializeField, Min(0.1f)]
    private float neckFollowLengthU = 1.25f;
    [SerializeField, Range(0f, 60f)] private float maximumHeadLeanDegrees = 50f;
    [SerializeField, Min(0f)] private float risingFaceClearanceU = .28f;
    [SerializeField, Range(.5f, 1f)] private float neckWaistScale = .74f;
    [SerializeField, Min(.2f)] private float releaseCurveDurationS = 1.15f;
    [SerializeField, Min(.1f)]
    private float headContactBlendLengthU = .45f;

    private struct SwimTrailPoint {
        public Vector3 position;
        public float distance;
        public SwimTrailPoint(Vector3 position, float distance) { this.position=position; this.distance=distance; }
    }
    private readonly List<SwimTrailPoint> swimTrail = new List<SwimTrailPoint>(256);
    private Vector3 previousTrailRoot, trailHeading;
    private float previousTrailTime, swimPhase, swimPhaseOffset, swimTravelDistance;
    private bool swimTrailReady, spinePoseReady;

    private bool swimRotationInitialized;
    private Mesh animatedReferenceMesh;
    private Transform referenceVisual;
    private Vector3[] referenceWaveVertices, referenceWaveNormals;
    [SerializeField, Min(.2f)] private float roundedNeckLengthU = 1.8f;
    [SerializeField, Range(1,32)] private int roundedNeckPasses = 20;
    [SerializeField, Min(.01f)] private float roundedNeckMaxOffsetU = .12f;
    private Vector3[] neckSurfaceBuffer,neckSurfaceOriginal;
    private Vector3[] paintedBellyDirections;
    private Vector3[] originalHeadVertices;
    private Transform referenceHeadVisual;
    private Renderer referenceHeadRenderer;
    private int headVertexCount;
    private Vector3[] neckRim;
    private Vector3[] spineCenters, spineTangents;
    private float[] spineRadii;
    private float originalHeadFront;
    private float referenceHeadBelowPivotU;
    private const int BodyRings = 257;
    private const int BodySides = 32;
    private const int BodyStride = BodySides + 1;

    private CartoonCobraVisual cartoonVisual;

    // MonoBehaviour Lifecycle  ################################################
    private void Awake() {
        body = GetComponent<Rigidbody>();
        hitCollider = GetComponent<CapsuleCollider>();
        if (body == null || hitCollider == null) {
            Debug.LogError(
                "FallingCobraController:\trequires Rigidbody and CapsuleCollider",
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
                segment.localPosition = Vector3.up * (i * segmentSpacingU);
                highestSegmentY = Mathf.Max(
                    highestSegmentY,
                    segment.localPosition.y
                );
            }
        }

        // One trigger covers the whole snake, so one collision creates one
        // branch-hit event and camera impulse instead of one per segment.
        foreach (
            Collider segmentCollider in GetComponentsInChildren<Collider>()
        ) {
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

        BuildCartoonVisual();
        CreateVisibleContacts();
        CreateTrackingEyes();

        if (warningHiss != null) {
            GameObject voice = new GameObject("SnakeHiss");
            voice.transform.SetParent(referenceHeadVisual != null ? referenceHeadVisual : transform, false);
            voice.transform.localPosition = originalHeadFaceCenter;
            hissSource = voice.AddComponent<AudioSource>();
            hissSource.clip = warningHiss;
            hissSource.playOnAwake = false;
            hissSource.loop = false;
            hissSource.volume = warningHissVolume;
            hissSource.spatialBlend = 1f;
            hissSource.dopplerLevel = 0f;
            hissSource.minDistance = warningHissMinDistanceU;
            hissSource.maxDistance = Mathf.Max(triggerDistanceU + alertExitPaddingU, warningHissMinDistanceU);
        }
        lastHeadPoseTime = Time.time;
    }

    private void OnDisable() {
        if (hissSource != null) hissSource.Stop();
    }

    private void OnDestroy() {
        cartoonVisual?.Dispose();
        if(eyeWhiteMaterial!=null)Destroy(eyeWhiteMaterial);
        if(eyePupilMaterial!=null)Destroy(eyePupilMaterial);
        if(eyePupilMesh!=null)Destroy(eyePupilMesh);
        if (animatedReferenceMesh != null) Destroy(animatedReferenceMesh);
    }

    private void BuildCartoonVisual() {
        if (!cartoonAppearance.enabled) {
            return;
        }
        var segments = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform) {
            if (child.name.StartsWith("Segment_")) {
                segments.Add(child);
            }
        }
        segments.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (segments.Count < 2 || !segments[0].TryGetComponent(out Renderer source)) {
            return;
        }
        if (cartoonAppearance.referenceBody != null) {
            foreach (Transform segment in segments) {
                if (segment.TryGetComponent(out Renderer renderer)) {
                    renderer.enabled = false;
                }
            }
            GameObject visual = Instantiate(cartoonAppearance.referenceBody, transform);
            visual.name = "CartoonSnakeBody";
            MeshFilter meshFilter = visual.GetComponent<MeshFilter>();
            BuildFallingReference(meshFilter);
            return;
        }
        cartoonVisual = new CartoonCobraVisual(transform, source.sharedMaterial, cartoonAppearance);
        Transform head = new GameObject("CartoonHeadAnchor").transform;
        head.SetParent(transform, false);
        head.localPosition = segments[0].localPosition;
        head.localRotation = Quaternion.Euler(0f, 0f, 180f);
        cartoonVisual.BuildHead(head, hitRadiusU * 0.78f);
        Vector3[] centers = new Vector3[segments.Count];
        Vector3[] outward = new Vector3[segments.Count];
        float[] radii = new float[segments.Count];
        for (int i = 0; i < segments.Count; i++) {
            float t = i / (float)(segments.Count - 1);
            Vector3 local = segments[i].localPosition;
            local.x += Mathf.Sin(t * Mathf.PI * 3f) * hitRadiusU * 0.38f;
            centers[i] = transform.TransformPoint(local);
            outward[i] = -transform.forward;
            radii[i] = hitRadiusU * 0.37f * (1f - t * t);
            if (segments[i].TryGetComponent(out Renderer renderer)) {
                renderer.enabled = false;
            }
        }
        cartoonVisual.UpdateBody(centers, radii, outward);
    }

    private struct HeadPoint {
        public Vector3 point, normal;
        public Vector2 uv;
        public static HeadPoint Lerp(HeadPoint a, HeadPoint b, float t) => new HeadPoint {
            point = Vector3.Lerp(a.point, b.point, t), normal = Vector3.Lerp(a.normal, b.normal, t).normalized,
            uv = Vector2.Lerp(a.uv, b.uv, t)
        };
    }

    private sealed class HeadTemplate {
        public Mesh mesh;
        public Vector3[] vertices, rim, rimNormals;
        public Vector2[] rimUV;
        public Bounds bounds;
        public float front;
        public Quaternion faceBasis;
        public Vector3 faceCenter;
    }

    private static readonly Dictionary<(Mesh, float, Vector3, float), HeadTemplate> headTemplates = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHeadTemplates() {
        ReleaseHeadTemplates();
        Application.quitting -= ReleaseHeadTemplates;
        Application.quitting += ReleaseHeadTemplates;
    }

    private static void ReleaseHeadTemplates() {
        foreach (HeadTemplate template in headTemplates.Values) {
            if (template.mesh != null) Destroy(template.mesh);
        }
        headTemplates.Clear();
    }

    // Original high-density head stays rigid and is prepared only once per source/size.
    private HeadTemplate CreateHeadTemplate(Mesh source, float size) {
        Vector3[] points = source.vertices, normals = source.normals;
        Vector2[] uv = source.uv;
        int[] sourceTriangles = source.triangles;
        Vector3 headAnchor=Vector3.zero,tailAnchor=Vector3.zero;
        int heads=0,tails=0;
        foreach(Vector3 point in points){
            if(point.y<source.bounds.min.y+source.bounds.size.y*.3f){headAnchor+=point;heads++;}
            if(point.y>source.bounds.max.y-source.bounds.size.y*.2f){tailAnchor+=point;tails++;}
        }
        headAnchor/=Mathf.Max(1,heads);tailAnchor/=Mathf.Max(1,tails);
        Vector3 sourceAxis=(tailAnchor-headAnchor).normalized;
        float cut=Vector3.Dot(headAnchor+sourceAxis*Vector3.Distance(headAnchor,tailAnchor)*.32f,sourceAxis);
        var head = new List<HeadPoint>();
        var rim = new List<HeadPoint>();
        for (int index = 0; index < sourceTriangles.Length; index += 3) {
            var triangle = new HeadPoint[3];
            for (int corner = 0; corner < 3; corner++) {
                int vertex = sourceTriangles[index + corner];
                triangle[corner] = new HeadPoint {point=points[vertex],normal=normals[vertex],uv=uv[vertex]};
            }
            var polygon = new List<HeadPoint>(4);
            for (int corner = 0; corner < 3; corner++) {
                HeadPoint a=triangle[corner], b=triangle[(corner+1)%3];
                float distanceA=Vector3.Dot(a.point,sourceAxis),distanceB=Vector3.Dot(b.point,sourceAxis);
                bool keepA=distanceA<=cut, keepB=distanceB<=cut;
                if (keepA) polygon.Add(a);
                if (keepA != keepB) {
                    HeadPoint crossing=HeadPoint.Lerp(a,b,(cut-distanceA)/(distanceB-distanceA));
                    polygon.Add(crossing);rim.Add(crossing);
                }
            }
            for(int corner=1;corner+1<polygon.Count;corner++){head.Add(polygon[0]);head.Add(polygon[corner]);head.Add(polygon[corner+1]);}
        }
        Bounds rimBounds=new Bounds(rim[0].point,Vector3.zero);
        foreach(var vertex in rim)rimBounds.Encapsulate(vertex.point);
        Vector3 pivot=rimBounds.center;
        pivot+=sourceAxis*(cut-Vector3.Dot(pivot,sourceAxis));
        Quaternion align=Quaternion.FromToRotation(sourceAxis,Vector3.up);
        Vector3 face=Vector3.ProjectOnPlane(align*referenceFaceDirection,Vector3.up).normalized;
        Quaternion yaw=Quaternion.FromToRotation(face,Vector3.forward)*align;
        Bounds localRimBounds=new Bounds(yaw*(rim[0].point-pivot)*size,Vector3.zero);
        foreach(var vertex in rim)localRimBounds.Encapsulate(yaw*(vertex.point-pivot)*size);
        Vector3 localCenter=new Vector3(localRimBounds.center.x,0f,localRimBounds.center.z);
        pivot+=Quaternion.Inverse(yaw)*localCenter/size;

        int count = head.Count;
        var template = new HeadTemplate {
            vertices = new Vector3[count], rim = new Vector3[BodyStride],
            rimUV = new Vector2[BodyStride], rimNormals = new Vector3[BodyStride],
            bounds = new Bounds(Vector3.zero, Vector3.zero)
        };
        var headNormals = new Vector3[count];
        var coordinates = new Vector2[count];
        var colors = new Color[count];
        var indices = new int[count];
        for (int i = 0; i < count; i++) {
            Vector3 vertex = yaw * (head[i].point - pivot) * size;
            Vector3 normal = yaw * head[i].normal;
            float collarT = Mathf.InverseLerp(-.4f, 0f, vertex.y);
            float collarScale = Mathf.Lerp(1f, neckWaistScale, Mathf.SmoothStep(0f, 1f, collarT));
            float derivative = (neckWaistScale-1f)*6f*collarT*(1f-collarT)/.4f;
            normal = new Vector3(normal.x/collarScale, normal.y-derivative*(vertex.x*normal.x+vertex.z*normal.z)/collarScale, normal.z/collarScale).normalized;
            float lengthT = Mathf.InverseLerp(-.6f,0f,vertex.y);
            float lengthDerivative = 1f-.2f*6f*lengthT*(1f-lengthT)/.6f;
            normal.y/=lengthDerivative;normal.Normalize();
            vertex.y+=.2f*(1f-Mathf.SmoothStep(0f,1f,lengthT));
            vertex.x*=collarScale;vertex.z*=collarScale;
            template.vertices[i] = vertex;
            Vector3 collarNormal = new Vector3(vertex.x, 0f, vertex.z).normalized;
            headNormals[i] = Vector3.Slerp(normal, collarNormal, Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-HeadCollarBlendU,0f,vertex.y))).normalized;
            coordinates[i] = head[i].uv;
            float neckBlend = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-HeadCollarBlendU,0f,vertex.y));
            colors[i] = new Color(0f, neckBlend, collarNormal.x*.5f+.5f, collarNormal.z*.5f+.5f);
            indices[i] = i;
            template.front = Mathf.Max(template.front, vertex.z);
            template.bounds.Encapsulate(vertex);
        }
        template.faceBasis = Quaternion.LookRotation(yaw * referenceFaceDirection, yaw * Vector3.up);
        template.faceCenter = template.bounds.center;
        // Intersect rays with the actual clipped boundary. The first body ring fits the original neck.
        for (int side = 0; side <= BodySides; side++) {
            float angle = (side % BodySides) * Mathf.PI * 2f / BodySides;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = 0f;
            Vector2 rimUV = rim[0].uv;
            Vector3 rimNormal = yaw * rim[0].normal;
            for (int edge = 0; edge + 1 < rim.Count; edge += 2) {
                Vector3 a3 = yaw * (rim[edge].point - pivot) * size;
                Vector3 b3 = yaw * (rim[edge + 1].point - pivot) * size;
                Vector2 a = new Vector2(a3.x, a3.z), delta = new Vector2(b3.x - a3.x, b3.z - a3.z);
                float cross = direction.x * delta.y - direction.y * delta.x;
                if (Mathf.Abs(cross) < .000001f) continue;
                float alongRay = (a.x * delta.y - a.y * delta.x) / cross;
                float alongEdge = (a.x * direction.y - a.y * direction.x) / cross;
                if (alongRay > radius && alongEdge >= -.001f && alongEdge <= 1.001f) {
                    radius = alongRay;
                    rimUV = Vector2.Lerp(rim[edge].uv, rim[edge + 1].uv, Mathf.Clamp01(alongEdge));
                    rimNormal = (yaw * Vector3.Lerp(rim[edge].normal, rim[edge + 1].normal, Mathf.Clamp01(alongEdge))).normalized;
                }
            }
            if (radius < .001f) radius = Mathf.Min(localRimBounds.extents.x, localRimBounds.extents.z);
            template.rim[side] = new Vector3(direction.x, 0f, direction.y) * radius * neckWaistScale;
            template.rimUV[side] = rimUV;
            template.rimNormals[side] = new Vector3(direction.x,0f,direction.y);
        }
        template.mesh = new Mesh {
            name = "Original rigid snake head", hideFlags = HideFlags.DontSave,
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
            vertices = template.vertices, normals = headNormals, uv = coordinates,
            colors = colors, triangles = indices, bounds = template.bounds
        };
        template.mesh.UploadMeshData(true);
        return template;
    }

    private void BuildFallingReference(MeshFilter filter) {
        swimPhaseOffset = (float)new System.Random(GetInstanceID()).NextDouble() * Mathf.PI * 2f;
        Mesh source = filter.sharedMesh;
        float size = hitRadiusU * 2f * referenceSizeMultiplier / Mathf.Max(source.bounds.size.x, source.bounds.size.z);
        var key = (source, size, referenceFaceDirection, neckWaistScale);
        if (!headTemplates.TryGetValue(key, out HeadTemplate template)) {
            template = CreateHeadTemplate(source, size);
            headTemplates.Add(key, template);
        }
        originalHeadVertices = template.vertices;
        headVertexCount = template.vertices.Length;
        originalHeadFront = template.front;
        originalHeadFaceBasis = template.faceBasis;
        originalHeadFaceCenter = template.faceCenter;
        neckRim = template.rim;
        neckTextureUV = template.rimUV;
        neckSourceNormals = template.rimNormals;
        originalHeadBounds = template.bounds;
        foreach (Vector3 p in neckRim) headNeckRadiusU = Mathf.Max(headNeckRadiusU, p.magnitude);
        Bounds headBounds = template.bounds;
        int total = BodyRings * BodyStride + 2;
        referenceWaveVertices = new Vector3[total];
        referenceWaveNormals = new Vector3[total];
        paintedBellyDirections = new Vector3[total];
        var coordinates = new Vector2[total];
        var sourceUV = new Vector2[total];
        var colors = new Color[total];
        var indices = new List<int>(BodyRings * BodySides * 6);
        for (int ring = 0; ring < BodyRings; ring++) {
            float along = ring / (float)(BodyRings - 1);
            for (int side = 0; side <= BodySides; side++) {
                int vertex = ring * BodyStride + side;
                coordinates[vertex] = new Vector2(side / (float)BodySides, along);
                sourceUV[vertex] = neckTextureUV[side];
                colors[vertex] = new Color(1f, 0f, 0f, 1f);
                if (ring + 1 < BodyRings && side < BodySides) {
                    int next = vertex + BodyStride;
                    indices.Add(vertex); indices.Add(next); indices.Add(vertex + 1);
                    indices.Add(vertex + 1); indices.Add(next); indices.Add(next + 1);
                }
            }
        }
        int tip = total - 2;
        int neckCap = total - 1;
        colors[neckCap] = new Color(1f, 0f, 0f, 1f);
        coordinates[neckCap] = new Vector2(.5f, 0f);
        sourceUV[neckCap] = neckTextureUV[0];
        for (int side = 0; side < BodySides; side++) { indices.Add(neckCap); indices.Add(side); indices.Add(side + 1); }
        colors[tip] = new Color(1f, 0f, 0f, 1f);
        coordinates[tip] = new Vector2(.5f, 1f);
        for (int side = 0; side < BodySides; side++) {
            indices.Add((BodyRings - 1) * BodyStride + side);
            indices.Add(tip);
            indices.Add((BodyRings - 1) * BodyStride + side + 1);
        }
        animatedReferenceMesh = new Mesh { name = "Smooth spine snake" };
        animatedReferenceMesh.MarkDynamic();
        animatedReferenceMesh.vertices = referenceWaveVertices;
        animatedReferenceMesh.normals = referenceWaveNormals;
        animatedReferenceMesh.uv = coordinates;
        animatedReferenceMesh.uv2 = sourceUV;
        animatedReferenceMesh.colors = colors;
        animatedReferenceMesh.triangles = indices.ToArray();
        filter.sharedMesh = animatedReferenceMesh;
        referenceVisual = filter.transform;
        referenceVisual.localScale = Vector3.one;
        referenceVisual.localPosition = Vector3.zero;
        referenceRenderer = filter.GetComponent<Renderer>();
        referenceHeadVisual = new GameObject("CartoonSnakeHead", typeof(MeshFilter), typeof(MeshRenderer)).transform;
        referenceHeadVisual.SetParent(transform, false);
        referenceHeadVisual.GetComponent<MeshFilter>().sharedMesh = template.mesh;
        referenceHeadRenderer = referenceHeadVisual.GetComponent<Renderer>();
        referenceHeadRenderer.sharedMaterial = referenceRenderer.sharedMaterial;
        referenceRenderer.enabled = false;
        referenceHeadRenderer.enabled = false;
        referenceHeadVisual.gameObject.layer = referenceVisual.gameObject.layer;
        surfaceBlock = new MaterialPropertyBlock();
        spineCenters = new Vector3[BodyRings];
        spineTangents = new Vector3[BodyRings];
        spineRadii = new float[BodyRings];
        spineContact = new float[BodyRings];
        referenceHeadBelowPivotU = Mathf.Max(hitRadiusU, -headBounds.min.y + .05f);
        hitCollider.radius = Mathf.Max(headBounds.extents.x, smoothBodyRadiusU + Mathf.Max(headSwayAmplitudeU, standingCoilOuterRadiusU));
        hitCollider.height = Mathf.Max(hitCollider.radius * 2f, smoothBodyLengthU - headBounds.min.y);
        hitCollider.center = Vector3.up * (smoothBodyLengthU + headBounds.min.y) * .5f;
    }


    private Vector3 GetTreeSurfacePoint(Vector3 position) {
        Vector3 outward=new Vector3(position.x-axisXZ.x,0f,position.z-axisXZ.y);
        if(outward.sqrMagnitude<0.000001f)outward=-transform.forward;
        outward.Normalize();
        float radius=treeSurfaceRadiusU+treeSurfaceClearanceU;
        return new Vector3(axisXZ.x+outward.x*radius,position.y,axisXZ.y+outward.z*radius);
    }

    private Vector3 GetAttackHeading(Vector3 position) {
        Vector3 outward=new Vector3(position.x-axisXZ.x,0f,position.z-axisXZ.y).normalized;
        Vector2 velocity=CommittedAttackVelocity(position);
        Vector3 heading=Vector3.Cross(Vector3.up,outward)*velocity.x+Vector3.up*velocity.y;
        return heading.sqrMagnitude>0.000001f?heading.normalized:-transform.up;
    }

    private Vector3 GetPredictedAttackPosition() {
        Vector3 position=body.position;
        float lag=Mathf.Clamp(Time.time-Time.fixedTime,0f,Time.fixedDeltaTime);
        Vector2 velocity=CommittedAttackVelocity(position);
        position.y+=velocity.y*lag;
        if(ringRadiusU>MIN_RING_RADIUS_U){
            float angle=Mathf.Atan2(position.x-axisXZ.x,position.z-axisXZ.y)+velocity.x*lag/ringRadiusU;
            position.x=axisXZ.x+Mathf.Sin(angle)*ringRadiusU;
            position.z=axisXZ.y+Mathf.Cos(angle)*ringRadiusU;
        }
        return position;
    }

    private void AppendSwimTrail(Vector3 position) {
        if(swimTrail.Count==0){swimTrail.Add(new SwimTrailPoint(position,0f));return;}
        SwimTrailPoint previous=swimTrail[swimTrail.Count-1];
        float distance=Vector3.Distance(previous.position,position);
        if(distance>0.00001f)swimTrail.Add(new SwimTrailPoint(position,previous.distance+distance));
    }

    private Vector3 OutsideTree(Vector3 point) {
        Vector3 outward = new Vector3(point.x - axisXZ.x, 0f, point.z - axisXZ.y);
        float radius = outward.magnitude, minimum = treeSurfaceRadiusU + treeSurfaceClearanceU;
        if (radius < minimum) {
            outward = radius > .00001f ? outward / radius : -transform.forward;
            point.x = axisXZ.x + outward.x * minimum;
            point.z = axisXZ.y + outward.z * minimum;
        }
        return point;
    }

    private float GroundSpineOffset(int ring) {
        float behind = ring * smoothBodyLengthU / (BodyRings - 1);
        return Mathf.Lerp(Mathf.Max(smoothBodyRadiusU * .9f, originalHeadFront),
            spineRadii[ring] * .9f - bellyPressDepthU, Mathf.SmoothStep(0f, 1f, behind / headContactBlendLengthU));
    }

    private void BeginSwimTrail() {
        swimTrail.Clear();
        launchBodyOffsets = new float[BodyRings];
        for (int ring = BodyRings - 1; ring >= 0; ring--) {
            Vector3 position = spinePoseReady ? spineCenters[ring] : body.position + transform.up * (smoothBodyLengthU * ring / (BodyRings - 1));
            Vector3 outward = new Vector3(position.x - axisXZ.x, 0f, position.z - axisXZ.y).normalized;
            AppendSwimTrail(OutsideTree(position - outward * GroundSpineOffset(ring)));
            launchBodyOffsets[ring]=swimTrail[swimTrail.Count-1].distance;
        }
        launchTrailDistance=swimTrail[swimTrail.Count-1].distance;
        launchBodyArc = launchTrailDistance;
        for (int ring=0; ring<BodyRings; ring++) launchBodyOffsets[ring]=launchTrailDistance-launchBodyOffsets[ring];
        launchNeckArc=0f;
        int raisedEnd=Mathf.Clamp(Mathf.RoundToInt(standingNeckLengthU/smoothBodyLengthU*(BodyRings-1)),1,BodyRings-1);
        for(int ring=1;ring<=raisedEnd;ring++)launchNeckArc+=Vector3.Distance(spineCenters[ring-1],spineCenters[ring]);
        previousTrailRoot = swimTrail[swimTrail.Count - 1].position;
        displayedTrailHead = previousTrailRoot; displayedTrailVelocity = Vector3.zero;
        releaseHeadHeight = spineCenters[0].y;
        releaseTrailOffset = previousTrailRoot - GetTreeSurfacePoint(body.position);
        releaseStandingBlend = waitingHeadBlend;
        previousTrailTime = Time.time;
        trailHeading = GetAttackHeading(body.position);
        swimPhase = swimPhaseOffset; swimTravelDistance = 0f;
        swimTrailReady = true;
    }

    private Vector3 GetAttackTrailRoot() {
        if (pursuitPaused && swimTrailReady) return previousTrailRoot;
        float age = Mathf.Max(0f, Time.time - attackStartedTime);
        float t = Mathf.Clamp01(age / releaseCurveDurationS);
        float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
        Vector3 point = GetTreeSurfacePoint(GetPredictedAttackPosition()) + releaseTrailOffset * (1f - ease);
        point-=launchWindupOffset*Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/strikeLaunchRampS))*(1f-ease);
        if (descentStopped) point.y = Mathf.Max(point.y, stoppedTrailHeight);
        return OutsideTree(point);
    }

    private void AdvanceSwimTrail() {
        if(!swimTrailReady)BeginSwimTrail();
        if(pursuitPaused) { previousTrailTime=Time.time;displayedTrailVelocity=Vector3.zero;return; }
        float elapsed=Time.time-previousTrailTime;
        if(elapsed<=0.000001f)return;
        Vector3 currentRoot=GetAttackTrailRoot(),lastRoot=previousTrailRoot;
        int samples=Mathf.Clamp(Mathf.CeilToInt(elapsed*120f),1,64);
        float sampleTime=elapsed/samples;
        for(int sample=1;sample<=samples;sample++){
            float fraction=sample/(float)samples;
            Vector3 nominal=Vector3.Lerp(previousTrailRoot,currentRoot,fraction);
            float advance=Vector3.Distance(GetTreeSurfacePoint(lastRoot),GetTreeSurfacePoint(nominal));
            swimTravelDistance+=advance;
            swimPhase+=advance/Mathf.Max(.5f,swimWavelengthU)*Mathf.PI*2f;
            Vector3 outward=new Vector3(nominal.x-axisXZ.x,0f,nominal.z-axisXZ.y).normalized;
            Vector3 desired=GetAttackHeading(nominal);
            Vector3 previous=Vector3.ProjectOnPlane(trailHeading,outward).normalized;
            if(previous.sqrMagnitude<0.000001f)previous=desired;
            float turn=Vector3.SignedAngle(previous,desired,outward);
            trailHeading=Quaternion.AngleAxis(turn*(1f-Mathf.Exp(-swimTurnSharpness*sampleTime)),outward)*previous;
            Vector3 lateral=Vector3.Cross(trailHeading,outward).normalized;
            float enterDistance=Mathf.Max(smoothBodyRadiusU*4f,headSwayEntryDistanceU);
            float enter=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(swimTravelDistance/enterDistance));
            Vector3 head=nominal+lateral*(headSwayAmplitudeU*Mathf.Sin(swimPhase)*enter*SteadyHeadAim(nominal));
            head = Vector3.SmoothDamp(displayedTrailHead, head, ref displayedTrailVelocity, Mathf.Lerp(headTrailSmoothTimeS*strikeHeadFollowScale,headTrailSmoothTimeS,Mathf.SmoothStep(0f,1f,Mathf.Clamp01((Time.time-attackStartedTime)/strikeBurstDurationS))), Mathf.Infinity, sampleTime);
            // Store the head's branch-clearing route so the entire body follows that route.
            for (int clearancePass=0; clearancePass<4; clearancePass++) {
                Vector3 headOutward = new Vector3(head.x-axisXZ.x,0f,head.z-axisXZ.y).normalized;
                Vector3 headCenter = head + headOutward * GroundSpineOffset(0);
                Quaternion headRotation = Quaternion.Slerp(standingHeadRotation, DirectedAttackRotation(headCenter), HeadDeparture(headCenter));
                headCenter=FitHeadToBark(headCenter,headRotation);
                head = OutsideTree(headCenter-headOutward*GroundSpineOffset(0));
                headCenter=head+headOutward*GroundSpineOffset(0);
                head = OutsideTree(head + KeepHeadClearOfBranch(headCenter,headRotation) - headCenter);
            }
            displayedTrailHead = head;
            // Follow the displayed head poses; substep corner projections must not add invisible detours.
            if (sample == samples) AppendSwimTrail(head);
            lastRoot=nominal;
        }
        previousTrailRoot=currentRoot;previousTrailTime=Time.time;
        float newest=swimTrail[swimTrail.Count-1].distance;
        while(swimTrail.Count>4&&newest-swimTrail[1].distance>Mathf.Max(smoothBodyLengthU,launchBodyArc)+1f)swimTrail.RemoveAt(0);
    }

    private float BodyTrailDistance(int ring) {
        float nominal = ring*smoothBodyLengthU/(BodyRings-1);
        if (launchBodyOffsets==null || !swimTrailReady) return nominal;
        float travel = swimTrail[swimTrail.Count-1].distance-launchTrailDistance;
        float released = Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(launchBodyArc,launchBodyArc+neckFollowLengthU,travel));
        return Mathf.Lerp(launchBodyOffsets[ring],nominal,released);
    }

    private float RemainingCoilWeight(int ring) {
        if (state!=State.Falling || supportBranch==null || !swimTrailReady) return 0f;
        float distance=swimTrail[swimTrail.Count-1].distance-BodyTrailDistance(ring);
        return 1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(launchTrailDistance-launchNeckArc-uncoilSupportBlendU,
            launchTrailDistance-launchNeckArc+uncoilSupportBlendU,distance));
    }

    private void SettleRemainingCoil() {
        if (supportBranch==null) return;
        float gap=float.PositiveInfinity;
        for(int ring=1;ring<BodyRings;ring++) {
            if(RemainingCoilWeight(ring)<.99f)continue;
            gap=Mathf.Min(gap,Vector3.Dot(spineCenters[ring]-branchCenter,branchUp)-branchHalfHeight-spineRadii[ring]-branchSkinClearanceU);
        }
        if(float.IsInfinity(gap)||gap<=0f)return;
        for(int ring=1;ring<BodyRings;ring++)spineCenters[ring]-=branchUp*(gap*RemainingCoilWeight(ring));
    }

    private Vector3 SampleSwimTrail(float behind,ref int cursor) {
        float distance=swimTrail[swimTrail.Count-1].distance-behind;
        if(distance<=swimTrail[0].distance){
            Vector3 tail=(swimTrail[0].position-swimTrail[1].position).normalized;
            return OutsideTree(swimTrail[0].position+tail*(swimTrail[0].distance-distance));
        }
        while(cursor>1&&swimTrail[cursor-1].distance>distance)cursor--;
        int lower=cursor-1,upper=cursor;
        SwimTrailPoint a=swimTrail[lower],b=swimTrail[upper];
        SwimTrailPoint before=swimTrail[Mathf.Max(0,lower-1)],after=swimTrail[Mathf.Min(swimTrail.Count-1,upper+1)];
        float span=Mathf.Max(.00001f,b.distance-a.distance);
        float t=Mathf.Clamp01((distance-a.distance)/span),t2=t*t,t3=t2*t;
        Vector3 firstTangent=(b.position-before.position)*(span/Mathf.Max(.00001f,b.distance-before.distance));
        Vector3 lastTangent=(after.position-a.position)*(span/Mathf.Max(.00001f,after.distance-a.distance));
        Vector3 point=(2f*t3-3f*t2+1f)*a.position+(t3-2f*t2+t)*firstTangent
            +(-2f*t3+3f*t2)*b.position+(t3-t2)*lastTangent;
        return OutsideTree(point);
    }


    private Vector2 SurfaceDeltaToPlayer(Vector3 position) {
        if (player == null || ringRadiusU <= MIN_RING_RADIUS_U) return new Vector2(0f, -1f);
        Vector2 fromAxis = new Vector2(position.x, position.z) - axisXZ;
        Vector2 toPlayer = new Vector2(player.position.x, player.position.z) - axisXZ;
        if (toPlayer.sqrMagnitude < MIN_RING_RADIUS_U * MIN_RING_RADIUS_U) return new Vector2(0f, -1f);
        float currentAngle = Mathf.Atan2(fromAxis.x, fromAxis.y) * Mathf.Rad2Deg;
        float targetAngle = Mathf.Atan2(toPlayer.x, toPlayer.y) * Mathf.Rad2Deg;
        return new Vector2(Mathf.DeltaAngle(currentAngle, targetAngle) * Mathf.Deg2Rad * ringRadiusU,
            player.position.y - position.y);
    }

    private void AlignReferenceToTree() {
        AlignReferenceToTree(transform.position);
    }

    private void AlignReferenceToTree(Vector3 position) {
        if (!isInitialized || referenceVisual == null) return;
        Vector3 inward = new Vector3(axisXZ.x - position.x, 0f,
            axisXZ.y - position.z);
        if (inward.sqrMagnitude <= 0.001f) return;
        inward.Normalize();
        Vector3 tangent = Vector3.Cross(Vector3.up, -inward);
        Vector2 surfaceDelta = state == State.Falling ? CommittedAttackVelocity(position) : SurfaceDeltaToPlayer(position);
        if(state!=State.Falling && surfaceDelta.y>=0f)surfaceDelta.y=-fallSpeedU;
        if (surfaceDelta.sqrMagnitude <= 0.001f) return;
        Vector3 heading = (tangent * surfaceDelta.x + Vector3.up * surfaceDelta.y).normalized;
        Vector3 tailDirection = -heading;
        if (swimRotationInitialized) {
            Vector3 previous = Vector3.ProjectOnPlane(transform.up, inward).normalized;
            if (previous.sqrMagnitude > 0.001f) {
                float turn = Vector3.SignedAngle(previous, tailDirection, inward);
                tailDirection = Quaternion.AngleAxis(turn * (1f - Mathf.Exp(-swimTurnSharpness * Time.deltaTime)),
                    inward) * previous;
            }
        }
        referenceVisual.localRotation = Quaternion.identity;
        body.rotation = Quaternion.LookRotation(inward, tailDirection);
        swimRotationInitialized = true;
    }

    private static float SmoothVisual01(float value) {
        float t=Mathf.Clamp01(value);
        return t*t*t*(t*(t*6f-15f)+10f);
    }

    private static Vector3 SpineCurveDirection(Vector3 start,Vector3 end,Vector3 first,Vector3 last,float t) {
        return ((6f*t*t-6f*t)*start+(3f*t*t-4f*t+1f)*first
            +(-6f*t*t+6f*t)*end+(3f*t*t-2f*t)*last).normalized;
    }

    private static Vector3 SpineCurve(Vector3 start, Vector3 end, Vector3 startTangent, Vector3 endTangent, float t) {
        float t2 = t * t, t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * start + (t3 - 2f * t2 + t) * startTangent
            + (-2f * t3 + 3f * t2) * end + (t3 - t2) * endTangent;
    }

    /// <summary>Bind this snake to a real, enabled branch before it can be shown or wake.</summary>
    public bool AttachToBranch(BoxCollider branch) {
        if (!isInitialized || branch == null || !branch.enabled || !branch.gameObject.activeInHierarchy) return false;
        supportBranch = branch;
        headRotationReady=false;
        RefreshBranchFrame();
        if (branchHalfLength < sittingCoilRadiusAlongU + smoothBodyRadiusU + .1f) { supportBranch = null; return false; }
        Vector3 outwards = new Vector3(branchCenter.x - axisXZ.x, 0f, branchCenter.z - axisXZ.y).normalized;
        Vector3 nearTrunk = new Vector3(axisXZ.x, branchCenter.y, axisXZ.y) + outwards * (treeSurfaceRadiusU + sittingCoilRadiusAlongU + .9f);
        branchAnchorAlong = Mathf.Clamp(Vector3.Dot(nearTrunk - branchCenter, branchAxis),
            -branchHalfLength + sittingCoilRadiusAlongU + .1f, branchHalfLength - sittingCoilRadiusAlongU - .1f);
        Vector3 top = BranchCoilPoint(0f, smoothBodyRadiusU);
        Vector3 root = new Vector3(axisXZ.x, top.y - standingBaseHeightU, axisXZ.y) + outwards * ringRadiusU;
        body.position = root;
        transform.position = root;
        if (player == null) {
            GameObject monkey = GameObject.FindGameObjectWithTag(PLAYER_TAG);
            if (monkey != null) player = monkey.transform;
        }
        branchHeadSideSign = player != null && Vector3.Dot(player.position - root, branchSide) < -.01f ? -1f : 1f;
        perchBound = true;
        if (referenceRenderer != null) referenceRenderer.enabled = true;
        if (referenceHeadRenderer != null) referenceHeadRenderer.enabled = true;
        Physics.SyncTransforms();
        LateUpdate();
        return true;
    }

    private void RefreshBranchFrame() {
        if (supportBranch == null) return;
        Transform branch = supportBranch.transform;
        branchCenter = branch.TransformPoint(supportBranch.center);
        Vector3 outwards = new Vector3(branchCenter.x - axisXZ.x, 0f, branchCenter.z - axisXZ.y).normalized;
        branchAxis = branch.right;
        if (Vector3.Dot(branchAxis, outwards) < 0f) branchAxis = -branchAxis;
        branchUp = branch.up;
        branchSide = Vector3.Cross(branchUp, branchAxis).normalized;
        Vector3 scale = branch.lossyScale, size = supportBranch.size;
        branchHalfLength = Mathf.Abs(scale.x * size.x) * .5f;
        branchHalfHeight = Mathf.Abs(scale.y * size.y) * .5f;
        branchHalfWidth = Mathf.Abs(scale.z * size.z) * .5f;
    }

    // The snake coils around itself above the wood; the branch never passes through a loop.
    private Vector3 BranchCoilPoint(float behind, float tubeRadius) {
        float along = Mathf.Clamp01(behind / Mathf.Max(.1f, smoothBodyLengthU - standingNeckLengthU));
        float phase = along * branchCoilTurns * Mathf.PI * 2f + perchedCoilPhaseDegrees*Mathf.Deg2Rad;
        float baseStart = Mathf.Max(.01f, (branchCoilTurns - 1f) / Mathf.Max(1f, branchCoilTurns));
        float radiusScale = along <= baseStart
            ? Mathf.Lerp(.55f, 1f, Mathf.Sin(Mathf.Clamp01(along / baseStart) * Mathf.PI * .5f))
            : Mathf.Lerp(1f, sittingCoilInnerScale, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(baseStart, 1f, along)));
        float baseProgress = (standingNeckLengthU + (smoothBodyLengthU - standingNeckLengthU) * baseStart) / smoothBodyLengthU;
        float baseRadius = smoothBodyRadiusU * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, baseProgress)));
        // The last turn lies flat on the branch; its taper curls inward underneath the upper body.
        float stackHeight = along < baseStart ? baseRadius + sittingCoilLayerRiseU * branchCoilTurns * (baseStart - along) : tubeRadius;
        float height = branchHalfHeight + Mathf.Max(tubeRadius, stackHeight) + branchSkinClearanceU;
        return branchCenter + branchAxis * (branchAnchorAlong + sittingCoilRadiusAlongU * radiusScale * Mathf.Cos(phase))
            + branchSide * (sittingCoilRadiusAcrossU * radiusScale * Mathf.Sin(phase)) + branchUp * height;
    }

    private Vector3 BranchSurfacePoint(Vector3 point) {
        Vector3 delta = point - branchCenter;
        float a = Vector3.Dot(delta, branchAxis), u = Vector3.Dot(delta, branchUp), s = Vector3.Dot(delta, branchSide);
        Vector3 closest = branchCenter + branchAxis * Mathf.Clamp(a, -branchHalfLength, branchHalfLength)
            + branchUp * Mathf.Clamp(u, -branchHalfHeight, branchHalfHeight)
            + branchSide * Mathf.Clamp(s, -branchHalfWidth, branchHalfWidth);
        if ((closest - point).sqrMagnitude > .000001f) return closest;
        float da = branchHalfLength - Mathf.Abs(a), du = branchHalfHeight - Mathf.Abs(u), ds = branchHalfWidth - Mathf.Abs(s);
        if (da < du && da < ds) return closest + branchAxis * ((a >= 0f ? 1f : -1f) * da);
        if (du < ds) return closest + branchUp * ((u >= 0f ? 1f : -1f) * du);
        return closest + branchSide * ((s >= 0f ? 1f : -1f) * ds);
    }

    private Vector3 KeepClearOfBranch(Vector3 point, float radius) {
        if (supportBranch == null) return point;
        Vector3 closest = BranchSurfacePoint(point);
        Vector3 delta = point - branchCenter;
        bool inside = Mathf.Abs(Vector3.Dot(delta, branchAxis)) < branchHalfLength
            && Mathf.Abs(Vector3.Dot(delta, branchUp)) < branchHalfHeight && Mathf.Abs(Vector3.Dot(delta, branchSide)) < branchHalfWidth;
        Vector3 away = inside ? closest - point : point - closest;
        float distance = away.magnitude, clearance = radius + branchSkinClearanceU;
        if (!inside && distance >= clearance) return point;
        return closest + (distance > .00001f ? away / distance : branchUp) * clearance;
    }

    private Vector3 VentralSupportDirection(Vector3 point, Vector3 treeInward, float tubeRadius) {
        if (supportBranch == null) return treeInward;
        Vector3 delta = point - branchCenter - branchAxis * branchAnchorAlong;
        float height = Vector3.Dot(delta, branchUp) - branchHalfHeight;
        float alongEdge = Mathf.Abs(Vector3.Dot(delta, branchAxis)) - sittingCoilRadiusAlongU - tubeRadius;
        float sideEdge = Mathf.Abs(Vector3.Dot(delta, branchSide)) - sittingCoilRadiusAcrossU - tubeRadius;
        float pileSupport = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-tubeRadius, .05f, height))
            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .25f, Mathf.Max(alongEdge, sideEdge))));
        Vector3 supportedBelly = Vector3.Slerp(treeInward, -branchUp, pileSupport).normalized;
        Vector3 towardBranch = BranchSurfacePoint(point) - point;
        float distance = towardBranch.magnitude;
        if (distance < .00001f) return supportedBelly;
        float contact = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(tubeRadius + .03f, tubeRadius + .25f, distance));
        return Vector3.Slerp(supportedBelly, towardBranch / distance, contact).normalized;
    }

    private float HeadDeparture(Vector3 position) {
        return Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.2f,headTurnDropU,releaseHeadHeight-position.y));
    }

    private void RoundNeckSurface(int neckEnd) {
        if(neckSurfaceBuffer==null || neckSurfaceBuffer.Length!=referenceWaveVertices.Length) {
            neckSurfaceBuffer=new Vector3[referenceWaveVertices.Length];
            neckSurfaceOriginal=new Vector3[referenceWaveVertices.Length];
        }
        System.Array.Copy(referenceWaveVertices,neckSurfaceOriginal,referenceWaveVertices.Length);
        float step=smoothBodyLengthU/(BodyRings-1);
        int end=Mathf.Clamp(Mathf.CeilToInt(roundedNeckLengthU/step),3,Mathf.Min(BodyRings-2,neckEnd+4));
        float roundLength=end*step;
        for(int pass=0;pass<roundedNeckPasses;pass++) {
            System.Array.Copy(referenceWaveVertices,neckSurfaceBuffer,referenceWaveVertices.Length);
            for(int ring=1;ring<end;ring++) {
                float behind=ring*step;
                float weight=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(behind/.16f))
                    *(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(roundLength*.65f,roundLength,behind)));
                for(int side=0;side<=BodySides;side++) {
                    int i=ring*BodyStride+side;
                    Vector3 average=(referenceWaveVertices[i-BodyStride]+referenceWaveVertices[i+BodyStride])*.5f;
                    Vector3 rounded=Vector3.Lerp(referenceWaveVertices[i],average,.5f*weight);
                    neckSurfaceBuffer[i]=neckSurfaceOriginal[i]+Vector3.ClampMagnitude(rounded-neckSurfaceOriginal[i],roundedNeckMaxOffsetU);
                }
            }
            var swap=referenceWaveVertices;referenceWaveVertices=neckSurfaceBuffer;neckSurfaceBuffer=swap;
        }
        if(roundedRingCenters==null) {roundedRingCenters=new Vector3[BodyRings];originalRingCenters=new Vector3[BodyRings];}
        for(int ring=0;ring<=end;ring++) {
            Vector3 center=Vector3.zero,original=Vector3.zero;
            for(int side=0;side<BodySides;side++){center+=referenceWaveVertices[ring*BodyStride+side];original+=neckSurfaceOriginal[ring*BodyStride+side];}
            roundedRingCenters[ring]=center/BodySides;originalRingCenters[ring]=original/BodySides;
        }
        Vector3 headAxis=referenceVisual.InverseTransformDirection(spineTangents[0]).normalized;
        Vector3 previousRight=referenceVisual.InverseTransformDirection(referenceHeadVisual.right).normalized;
        for(int ring=1;ring<end;ring++) {
            float behind=ring*step;
            Vector3 axis=(roundedRingCenters[ring+1]-roundedRingCenters[ring-1]).normalized;
            axis=Vector3.Slerp(headAxis,axis,Mathf.SmoothStep(0f,1f,Mathf.Clamp01(behind/neckCollarLengthU))).normalized;
            float thicknessFloor=smoothBodyRadiusU*minimumNeckThicknessShare*Mathf.SmoothStep(0f,1f,Mathf.Clamp01(behind/.35f));
            Vector3 transported=Vector3.ProjectOnPlane(previousRight,axis).normalized;
            if(transported.sqrMagnitude<.000001f)transported=Vector3.ProjectOnPlane(Vector3.right,axis).normalized;
            Vector3 targetRight=Vector3.ProjectOnPlane(referenceWaveVertices[ring*BodyStride]-roundedRingCenters[ring],axis).normalized;
            if(targetRight.sqrMagnitude<.000001f)targetRight=transported;
            float roll=Vector3.SignedAngle(transported,targetRight,axis);
            Vector3 right=Quaternion.AngleAxis(Mathf.Clamp(roll,-bodyTwistDegreesPerU*step,bodyTwistDegreesPerU*step),axis)*transported;
            Vector3 forward=Vector3.Cross(right,axis).normalized;
            previousRight=right;
            for(int side=0;side<=BodySides;side++) {
                int i=ring*BodyStride+side;
                float angle=(side%BodySides)*Mathf.PI*2f/BodySides;
                Vector3 radial=right*Mathf.Cos(angle)+forward*Mathf.Sin(angle);
                float radius=Mathf.Max(thicknessFloor,Vector3.Distance(neckSurfaceOriginal[i],originalRingCenters[ring]));
                referenceWaveVertices[i]=roundedRingCenters[ring]+radial.normalized*radius;
            }
        }

    }

    private void SmoothTubeNormals() {
        if(roundedRingCenters==null)roundedRingCenters=new Vector3[BodyRings];
        for(int ring=0;ring<BodyRings;ring++) {
            Vector3 center=Vector3.zero;
            for(int side=0;side<BodySides;side++)center+=referenceWaveVertices[ring*BodyStride+side];
            roundedRingCenters[ring]=center/BodySides;
        }
        float step=smoothBodyLengthU/(BodyRings-1);
        for(int ring=1;ring<BodyRings;ring++)for(int side=0;side<=BodySides;side++) {
            int i=ring*BodyStride+side,s=side%BodySides;
            Vector3 along=referenceWaveVertices[Mathf.Min(BodyRings-1,ring+1)*BodyStride+side]
                -referenceWaveVertices[(ring-1)*BodyStride+side];
            Vector3 across=referenceWaveVertices[ring*BodyStride+(s+1)%BodySides]
                -referenceWaveVertices[ring*BodyStride+(s+BodySides-1)%BodySides];
            Vector3 normal=Vector3.Cross(along,across);
            if(normal.sqrMagnitude<.00000001f)continue;
            normal.Normalize();
            if(Vector3.Dot(normal,referenceWaveVertices[i]-roundedRingCenters[ring])<0f)normal=-normal;
            referenceWaveNormals[i]=Vector3.Slerp(referenceWaveNormals[i],normal,SmoothVisual01(ring*step/.3f)).normalized;
        }
    }

    private void UpdatePerchedAim(float deltaTime) {
        if(player==null || supportBranch==null)return;
        Vector3 toward=MonkeyAimPosition()-branchCenter-branchAxis*branchAnchorAlong;
        float side=Vector3.Dot(toward,branchSide),along=Vector3.Dot(toward,branchAxis);
        float gain=1f-Mathf.Exp(-perchedAimSharpness*deltaTime);
        perchedAimSide=Mathf.Lerp(perchedAimSide,Mathf.Clamp(side/Mathf.Max(.5f,branchHeadSideU),-1f,1f),gain);
        float phase=Mathf.Atan2(side/Mathf.Max(.1f,sittingCoilRadiusAcrossU),along/Mathf.Max(.1f,sittingCoilRadiusAlongU))*Mathf.Rad2Deg;
        perchedCoilPhaseDegrees=Mathf.LerpAngle(perchedCoilPhaseDegrees,phase,gain);
        float headClearance=referenceHeadVisual!=null?Vector3.Dot(referenceHeadVisual.position+referenceHeadVisual.rotation*originalHeadBounds.center-branchCenter,branchUp):0f;
        if(Mathf.Abs(perchedAimSide)>.2f && headClearance>branchHalfHeight+originalHeadBounds.extents.magnitude+.05f)
            branchHeadSideSign=Mathf.Sign(perchedAimSide);
    }

    private Vector3 SupportedPaintBelly(Vector3 outward,float branchWeight) {
        // The raised neck faces the trunk; a seated coil rests its belly downward on the wood.
        // Do not project onto the spine tangent: steep bends can invert that projection.
        return Vector3.Slerp(-outward,-branchUp,Mathf.Clamp01(branchWeight)).normalized;
    }

    private Vector3 SourceEyeForward()=>eyeAnchorsReady?(leftEyeNormalLocal+rightEyeNormalLocal).normalized:originalHeadFaceBasis*Vector3.forward;
    private Vector3 SourceEyeUp(){
        if(!eyeAnchorsReady)return originalHeadFaceBasis*Vector3.up;
        Vector3 up=Vector3.Cross(SourceEyeForward(),rightEyeSurfaceLocal-leftEyeSurfaceLocal).normalized;
        return Vector3.Dot(up,Vector3.up)<0f?-up:up;
    }
    private Quaternion SourceEyeBasis()=>Quaternion.LookRotation(SourceEyeForward(),SourceEyeUp());

    private Quaternion DirectedAttackRotation(Vector3 position) {
        Vector3 outward = new Vector3(position.x-axisXZ.x, 0f, position.z-axisXZ.y).normalized;
        Vector3 heading = Vector3.ProjectOnPlane(displayedTrailVelocity, outward).normalized;
        if (heading.sqrMagnitude < .00001f) heading = GetAttackHeading(position);
        Vector3 toMonkey = committedTargetPosition - position;
        if (monkeyInHeadRange) {
            Vector3 tangent = Vector3.ProjectOnPlane(toMonkey, outward).normalized;
            if (tangent.sqrMagnitude > .00001f) {
                float turn = Mathf.Clamp(Vector3.SignedAngle(heading, tangent, outward), -trackingSurfaceTurnDegrees, trackingSurfaceTurnDegrees);
                heading = Quaternion.AngleAxis(turn, outward) * heading;
            }
        }
        Quaternion dorsal = Quaternion.LookRotation(Vector3.ProjectOnPlane(SourceEyeForward(), Vector3.up).normalized, Vector3.up);
        Quaternion flat = Quaternion.LookRotation(outward, -heading) * Quaternion.Inverse(dorsal);
        if (!monkeyInHeadRange || toMonkey.sqrMagnitude < .00001f) return flat;
        // The face, rather than just its pupils, follows the live target. Keep roll supported by the bark.
        Vector3 gaze = toMonkey.normalized;
        Vector3 faceUp = Vector3.ProjectOnPlane(outward, gaze).normalized;
        if (faceUp.sqrMagnitude < .00001f) faceUp = Vector3.ProjectOnPlane(Vector3.up, gaze).normalized;
        Quaternion aimed = Quaternion.LookRotation(gaze, faceUp) * Quaternion.Inverse(SourceEyeBasis());
        return Quaternion.RotateTowards(flat, aimed, upwardHeadPitchDegrees);
    }

    private Vector3 FitHeadToBark(Vector3 position,Quaternion rotation){
        if(state!=State.Falling)return position;
        float blend=HeadDeparture(position);
        Vector3 outward=new Vector3(position.x-axisXZ.x,0f,position.z-axisXZ.y).normalized;
        Vector3 direction=Quaternion.Inverse(rotation)*outward;
        if(!supportCached||Vector3.Dot(direction,supportDirection)<.99995f){
            supportMinimum=float.PositiveInfinity;
            foreach(Vector3 vertex in originalHeadVertices)supportMinimum=Mathf.Min(supportMinimum,Vector3.Dot(vertex,direction));
            supportDirection=direction;supportCached=true;
        }
        float desired=treeSurfaceRadiusU+headBarkGapU-supportMinimum;
        if (monkeyInHeadRange) {
            Vector3 faceOffset = rotation * (eyeAnchorsReady ? (leftEyeSurfaceLocal+rightEyeSurfaceLocal)*.5f-SourceEyeUp()*.06f : originalHeadFaceCenter);
            Vector3 face = position + faceOffset;
            Vector3 delta = committedTargetPosition - face;
            float surfaceGap = Vector3.ProjectOnPlane(delta, outward).magnitude;
            float reachBlend = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(surfaceGap / strikeReachDistanceU));
            float targetRadius = Vector3.Dot(committedTargetPosition-new Vector3(axisXZ.x,position.y,axisXZ.y)-faceOffset, outward);
            desired += Mathf.Clamp(targetRadius-desired, 0f, maximumStrikeReachU)*reachBlend;
        }
        float radius=new Vector2(position.x-axisXZ.x,position.z-axisXZ.y).magnitude;
        return position+outward*((desired-radius)*blend);
    }

    private Vector3 MonkeyAimPosition(){
        if(player==null)return committedTargetPosition;
        if(cachedAimOwner!=player){
            cachedAimOwner=player;playerBodyCollider=null;float best=0f;
            foreach(Collider candidate in player.GetComponentsInChildren<Collider>()){
                if(candidate.isTrigger||candidate.GetComponentInParent<ArmController>()!=null)continue;
                Vector3 size=candidate.bounds.size;float volume=size.x*size.y*size.z;
                if(volume>best){best=volume;playerBodyCollider=candidate;}
            }
        }
        return playerBodyCollider!=null&&playerBodyCollider.enabled?playerBodyCollider.bounds.center:player.position;
    }

    private void UpdateTrackedMonkey(){
        if(player==null){monkeyInHeadRange=false;pursuitPaused=state==State.Falling;return;}
        Vector3 aim=MonkeyAimPosition();
        float range = Mathf.Max(headTrackingRangeU, triggerDistanceU);
        if (IsMonkeyReaching()) range=Mathf.Max(range,reachWakeDistanceU);
        monkeyInHeadRange=Vector3.Distance(ThreatPosition,aim)<=range+(monkeyInHeadRange?headTrackingExitPaddingU:0f);
        pursuitPaused=state==State.Falling && !monkeyInHeadRange;
        if(monkeyInHeadRange){
            committedTargetPosition=aim;hasCommittedTarget=true;
            Vector2 radial=new Vector2(aim.x,aim.z)-axisXZ;
            committedTargetAngle=Mathf.Atan2(radial.x,radial.y)*Mathf.Rad2Deg;
        }
        if(state==State.Falling){
            Vector3 head=ThreatPosition;
            float rootAngle=Mathf.Atan2(body.position.x-axisXZ.x,body.position.z-axisXZ.y)*Mathf.Rad2Deg;
            float headAngle=Mathf.Atan2(head.x-axisXZ.x,head.z-axisXZ.y)*Mathf.Rad2Deg;
            if(!headFeedbackReady){previousFeedbackAngle=headAngle;headAngularSpeed=0f;headFeedbackReady=true;}
            float measured=Mathf.DeltaAngle(previousFeedbackAngle,headAngle)*Mathf.Deg2Rad*ringRadiusU/Time.fixedDeltaTime;previousFeedbackAngle=headAngle;
            measured=Mathf.Clamp(measured,-horizontalSpeedU*2f,horizontalSpeedU*2f);
            headAngularSpeed=Mathf.Lerp(headAngularSpeed,measured,1f-Mathf.Exp(-12f*Time.fixedDeltaTime));
            float aimError=Mathf.DeltaAngle(headAngle,committedTargetAngle)*Mathf.Deg2Rad*ringRadiusU;
            headAimCommand=Mathf.Clamp(headAimGain*aimError-headAimDamping*headAngularSpeed,-horizontalSpeedU,horizontalSpeedU);
            float bias=Mathf.DeltaAngle(rootAngle,headAngle);
            headSteeringBias=Mathf.LerpAngle(headSteeringBias,bias,1f-Mathf.Exp(-headSteeringSharpness*Time.fixedDeltaTime));
            float drop=head.y-committedTargetPosition.y;float desiredFall=1f;
            if (monkeyInHeadRange) {
                if (!descentStopped && drop <= targetHeightStopMarginU) {
                    descentStopped = true;
                    stoppedTrailHeight = displayedTrailHead.y;
                    displayedTrailVelocity.y = Mathf.Max(0f, displayedTrailVelocity.y);
                }
                // Brake using the visible face height; the root pivot can be well below the raised head.
                desiredFall = descentStopped ? 0f : Mathf.Clamp01((drop-targetHeightStopMarginU)*verticalArrivalGain/Mathf.Max(.01f, fallSpeedU));
            }
            if(monkeyInHeadRange&&drop>-.25f&&drop<approachDropRangeU){
                float angularError=Mathf.Abs(Mathf.DeltaAngle(headAngle,committedTargetAngle))*Mathf.Deg2Rad*ringRadiusU;
                float offAim=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(headContactRadiusU*.6f,headContactRadiusU*1.6f,angularError));
                float near=1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(1f,approachDropRangeU,Mathf.Max(0f,drop)));
                desiredFall*=Mathf.Lerp(1f,minimumApproachFallScale,offAim*near);
            }
            approachFallScale = descentStopped ? 0f : Mathf.Min(desiredFall, Mathf.Lerp(approachFallScale,desiredFall,1f-Mathf.Exp(-10f*Time.fixedDeltaTime)));
        }
    }

    private float SteadyHeadAim(Vector3 position){
        if(!monkeyInHeadRange)return 1f;
        return Mathf.Lerp(closeAimSwayScale,1f,Mathf.SmoothStep(0f,1f,Mathf.Clamp01(Vector3.Distance(position,committedTargetPosition)/steadyAimDistanceU)));
    }

    private Vector3 KeepHeadClearOfBranch(Vector3 position, Quaternion rotation) {
        if (supportBranch == null) return position;
        Vector3 center = position + rotation * originalHeadBounds.center;
        float radius = originalHeadBounds.extents.magnitude + .035f;
        Vector3 delta = center - branchCenter;
        float alongGap = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(delta, branchAxis)) - branchHalfLength);
        float protectedTop=branchHalfHeight,protectedSide=branchHalfWidth;
        if(state==State.Falling && swimTrailReady && RemainingCoilWeight(BodyRings-1)>.01f) {
            // Exit outside the supported pile, rather than diving through the remaining body.
            protectedTop+=smoothBodyRadiusU*2f+sittingCoilLayerRiseU*branchCoilTurns;
            protectedSide=Mathf.Max(protectedSide,sittingCoilRadiusAcrossU+smoothBodyRadiusU);
        }
        float protectedCenter=(protectedTop-branchHalfHeight)*.5f;
        float protectedHalfHeight=(protectedTop+branchHalfHeight)*.5f;
        float upGap=Mathf.Max(0f,Mathf.Abs(Vector3.Dot(delta,branchUp)-protectedCenter)-protectedHalfHeight);
        float remaining = radius * radius - alongGap * alongGap - upGap * upGap;
        if (remaining <= 0f) return position;
        float requiredSide = protectedSide + Mathf.Sqrt(remaining);
        float penetration = requiredSide - Vector3.Dot(delta, branchSide) * branchHeadSideSign;
        const float feather = .1f;
        float shift = penetration >= feather ? penetration : penetration <= -feather ? 0f
            : (penetration + feather) * (penetration + feather) / (4f * feather);
        return position + branchSide * (branchHeadSideSign * shift);
    }

    private Vector3 RestingBranchFace(Quaternion rotation) {
        Vector3 woodTop = branchCenter + branchAxis * branchAnchorAlong + branchUp * branchHalfHeight;
        float bottom = 0f;
        for (int corner = 0; corner < 8; corner++) {
            Vector3 p = originalHeadBounds.center + Vector3.Scale(originalHeadBounds.extents,
                new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            bottom = Mathf.Min(bottom, Vector3.Dot(rotation * (p - originalHeadFaceCenter), branchUp));
        }
        Vector3 outwards = new Vector3(branchCenter.x - axisXZ.x, 0f, branchCenter.z - axisXZ.y).normalized;
        return woodTop + branchUp * (-bottom + .025f) + outwards * branchHeadForwardU + branchSide * (branchHeadSideU * perchedAimSide);
    }

    private Vector3 StandingTailPoint(float behind, Vector3 outward, Vector3 side) {
        float length = Mathf.Max(.1f, smoothBodyLengthU - standingNeckLengthU);
        float along = Mathf.Clamp01(behind / length);
        float radius = Mathf.Lerp(standingCoilOuterRadiusU, standingCoilInnerRadiusU, along);
        float theta = along * standingCoilTurns * Mathf.PI * 2f;
        float horizontal = radius * Mathf.Sin(theta);
        float height = standingBaseHeightU + radius * Mathf.Cos(theta) - standingCoilOuterRadiusU;
        float skinRadius = treeSurfaceRadiusU + treeSurfaceClearanceU;
        float angle = horizontal / skinRadius;
        Vector3 radial = outward * Mathf.Cos(angle) + side * Mathf.Sin(angle);
        return new Vector3(axisXZ.x + radial.x * skinRadius, body.position.y + height, axisXZ.y + radial.z * skinRadius);
    }

    private Vector3[] fairSpine, relaxedNeck;
    private void RelaxOriginalRaisedNeck(){
        if(relaxedNeck==null||relaxedNeck.Length!=BodyRings)relaxedNeck=new Vector3[BodyRings];
        System.Array.Copy(spineCenters,relaxedNeck,BodyRings);
        float step=smoothBodyLengthU/(BodyRings-1),newest=swimTrail[swimTrail.Count-1].distance;
        int reach=Mathf.Max(1,Mathf.RoundToInt(.4f/step));
        float phase=Mathf.SmoothStep(0f,1f,Mathf.Clamp01((Time.time-attackStartedTime)/.25f));
        for(int ring=3;ring<BodyRings-2;ring++){
            float behind=ring*step,pathDistance=newest-behind;
            float start=launchTrailDistance-launchNeckArc;
            float window=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(start-.25f,start+.25f,pathDistance))
                *(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(launchTrailDistance+.25f,launchTrailDistance+.75f,pathDistance)))
                *Mathf.SmoothStep(0f,1f,Mathf.Clamp01(behind/.5f));
            if(window<=0f || RemainingCoilWeight(ring)>.01f)continue;
            Vector3 point=(spineCenters[Mathf.Max(0,ring-2*reach)]+spineCenters[Mathf.Max(0,ring-reach)]*4f+spineCenters[ring]*6f
                +spineCenters[Mathf.Min(BodyRings-1,ring+reach)]*4f+spineCenters[Mathf.Min(BodyRings-1,ring+2*reach)])/16f;
            relaxedNeck[ring]=KeepClearOfBranch(Vector3.Lerp(spineCenters[ring],point,phase*window),spineRadii[ring]+.012f);
        }
        var swap=spineCenters;spineCenters=relaxedNeck;relaxedNeck=swap;
    }

    private void FairSpineCorners(bool following, int neckEnd) {
        if(fairSpine==null||fairSpine.Length!=BodyRings)fairSpine=new Vector3[BodyRings];
        int end=following?BodyRings-2:Mathf.Min(BodyRings-2,neckEnd+2);
        for(int pass=0;pass<64;pass++){
            System.Array.Copy(spineCenters,fairSpine,BodyRings);
            for(int ring=3;ring<end;ring++){
                float freeWeight=following?1f-RemainingCoilWeight(ring):1f;
                if(freeWeight<.00001f)continue;
                Vector3 before=spineCenters[ring]-spineCenters[ring-1],after=spineCenters[ring+1]-spineCenters[ring];
                float bend=Vector3.Angle(before,after);
                float strength=SmoothVisual01(Mathf.InverseLerp(2f,8f,bend))*freeWeight;
                if(strength<.00001f)continue;
                Vector3 point=(spineCenters[ring-1]+spineCenters[ring]*2f+spineCenters[ring+1])*.25f;
                fairSpine[ring]=KeepClearOfBranch(Vector3.Lerp(spineCenters[ring],point,strength),spineRadii[ring]+.012f);
            }
            var swap=spineCenters;spineCenters=fairSpine;fairSpine=swap;
        }
    }

    private void LateUpdate() {
        if (animatedReferenceMesh == null || state == State.Done || !perchBound) return;
        RefreshBranchFrame();
        AlignReferenceToTree();
        bool following = state == State.Falling;
        float poseDelta = Mathf.Max(0f, Time.time - lastHeadPoseTime);
        lastHeadPoseTime = Time.time;
        float gain = 1f - Mathf.Exp(-headPoseSharpness * poseDelta);
        waitingHeadBlend = Mathf.Lerp(waitingHeadBlend, state == State.Warning ? 1f : 0f, gain);
        float windup=state==State.Warning && monkeyInHeadRange
            ?Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(fallStartTime-strikeWindupDurationS,fallStartTime,Time.time)):0f;
        strikeWindupBlend=Mathf.Lerp(strikeWindupBlend,windup,1f-Mathf.Exp(-strikePoseSharpness*poseDelta));
        if(!following && monkeyInHeadRange)UpdatePerchedAim(poseDelta);
        if (following) AdvanceSwimTrail();
        float phase = Time.time * bodyWaveSpeedHz * .3f * Mathf.PI * 2f + swimPhaseOffset;
        Matrix4x4 toWorld = referenceVisual.localToWorldMatrix, toLocal = referenceVisual.worldToLocalMatrix;
        Vector3 rootOut = new Vector3(body.position.x - axisXZ.x, 0f, body.position.z - axisXZ.y).normalized;
        Vector3 rootSide = Vector3.Cross(Vector3.up, rootOut);

        int cursor = following ? swimTrail.Count - 1 : 0;
        Vector3 idle = Vector3.zero;
        for (int ring = 0; ring < BodyRings; ring++) {
            float along = ring / (float)(BodyRings - 1);
            float taper = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, along));
            float neckAlong = along * smoothBodyLengthU;
            float neckWaist = Mathf.Lerp(neckWaistScale, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1.25f, neckAlong)));
            spineRadii[ring] = Mathf.Max(.001f, smoothBodyRadiusU * taper * neckWaist);
            Vector3 contact;
            if (following) contact = SampleSwimTrail(BodyTrailDistance(ring), ref cursor);
            else {
                float angle = Mathf.Sin(phase - along * bodyWaveCycles * Mathf.PI * 2f) * bodyBendDegrees * .3f * Mathf.Deg2Rad
                    * Mathf.SmoothStep(0f, 1f, along / .3f);
                if (ring > 0) idle += new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * (smoothBodyLengthU / (BodyRings - 1));
                contact = GetTreeSurfacePoint(toWorld.MultiplyPoint3x4(idle));
            }
            Vector3 outward = new Vector3(contact.x - axisXZ.x, 0f, contact.z - axisXZ.y).normalized;
            spineCenters[ring] = contact + outward * GroundSpineOffset(ring);
        }

        Vector3 crawlHead = spineCenters[0];
        Vector3 headOut = new Vector3(crawlHead.x - axisXZ.x, 0f, crawlHead.z - axisXZ.y).normalized;
        Vector3 crawlTail = transform.up;
        if (following) {
            int sample = swimTrail.Count - 1;
            Vector3 contact = SampleSwimTrail(0f, ref sample);
            sample = swimTrail.Count - 1;
            Vector3 actual = Vector3.ProjectOnPlane(SampleSwimTrail(.06f, ref sample) - contact, headOut).normalized;
            Vector3 nominal = Vector3.ProjectOnPlane(-trailHeading, headOut).normalized;
            crawlTail = Vector3.Slerp(actual, nominal, headGazeStabilization).normalized;
        }
        if (crawlTail.sqrMagnitude < .00001f) crawlTail = transform.up;
        Quaternion crawlRotation = Quaternion.LookRotation(-headOut, crawlTail);
        if (following) crawlRotation = DirectedAttackRotation(crawlHead);
        if (state != State.Falling || monkeyInHeadRange) {
            Vector3 watchPoint=following?ThreatPosition:supportBranch!=null?BranchCoilPoint(0f,smoothBodyRadiusU)+Vector3.up*(standingFaceHeightU-standingBaseHeightU)
                +rootOut*branchHeadForwardU+branchSide*(branchHeadSideU * perchedAimSide):ThreatPosition;
            if(!following)watchPoint+=strikeWindupOffset;
            Vector3 watchDelta = MonkeyAimPosition() - watchPoint;
            Vector3 direction = monkeyInHeadRange && watchDelta.sqrMagnitude > .00001f
                ? watchDelta.normalized : (rootOut + Vector3.down * Mathf.Tan(waitingLookDownDegrees*Mathf.Deg2Rad)).normalized;
            Vector3 faceUp = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
            if (faceUp.sqrMagnitude < .00001f) faceUp = rootOut;
            Quaternion face = Quaternion.LookRotation(direction, faceUp);
            standingHeadRotation = face * Quaternion.Inverse(SourceEyeBasis());
        }
        Quaternion restRotation = Quaternion.LookRotation(rootOut * .5f + Vector3.down * .8660254f, Vector3.up) * Quaternion.Inverse(SourceEyeBasis());
        float liftBlend=1f-(1f-waitingHeadBlend)*(1f-waitingHeadBlend);
        float turnBlend=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.15f,.85f,waitingHeadBlend));
        float risingClearance=4f*waitingHeadBlend*(1f-waitingHeadBlend)*risingFaceClearanceU;
        float departure=following?HeadDeparture(crawlHead):0f;
        Quaternion headRotation = following ? Quaternion.Slerp(standingHeadRotation,crawlRotation,departure)
            : Quaternion.Slerp(restRotation,standingHeadRotation,turnBlend);
        Vector3 desiredFace = new Vector3(axisXZ.x, body.position.y + standingFaceHeightU, axisXZ.y)
            + rootOut * (treeSurfaceRadiusU + treeSurfaceClearanceU + waitingHeadLiftU);
        if (supportBranch != null) desiredFace = BranchCoilPoint(0f, smoothBodyRadiusU)
            + Vector3.up * (standingFaceHeightU - standingBaseHeightU) + rootOut * branchHeadForwardU + branchSide * (branchHeadSideU * perchedAimSide);
        if(!following) {
            Vector3 toward=MonkeyAimPosition()-desiredFace;
            if(toward.sqrMagnitude<.00001f)toward=rootOut;
            strikeWindupOffset=(-toward.normalized*strikePullbackU-Vector3.up*strikeCompressionU)*strikeWindupBlend;
            desiredFace+=strikeWindupOffset;
        }
        Vector3 restFace = supportBranch != null ? RestingBranchFace(restRotation) : crawlHead + restRotation * originalHeadFaceCenter;
        Vector3 restHead = restFace - restRotation * originalHeadFaceCenter;
        Vector3 standHead = desiredFace - standingHeadRotation * originalHeadFaceCenter;
        Vector3 raisedFace = Vector3.Lerp(restFace, desiredFace, liftBlend) + rootOut*risingClearance;
        Vector3 headCenter = following ? crawlHead : raisedFace-headRotation*originalHeadFaceCenter;

        if(headRotationReady)headRotation=Quaternion.RotateTowards(shownHeadRotation,headRotation,headLookTurnSpeedDegrees*poseDelta);
        shownHeadRotation=headRotation;headRotationReady=true;
        headCenter = FitHeadToBark(headCenter,headRotation);
        headCenter = KeepHeadClearOfBranch(headCenter, headRotation);

        float raisedLength = Mathf.Min(standingNeckLengthU, smoothBodyLengthU * .7f);
        float curveLength = following ? Mathf.Lerp(raisedLength,neckFollowLengthU,departure)+.8f*Mathf.Sin(Mathf.PI*departure) : raisedLength;
        int endRing = Mathf.Clamp(Mathf.RoundToInt(curveLength / smoothBodyLengthU * (BodyRings - 1)), 1, BodyRings - 2);
        bool continuousFreeNeck=false;
        float curveRingSpan=endRing,freeSpanDistance=0f;
        Vector3 continuousAnchor=Vector3.zero,continuousAnchorDirection=Vector3.zero;
        if(following) {
            SettleRemainingCoil();
            int anchor=1;
            while(anchor<BodyRings-2 && RemainingCoilWeight(anchor)<.98f)anchor++;
            if(anchor<BodyRings-2 && anchor>=4) {
                int lower=anchor-1;
                float transition=.5f-Mathf.Sin(Mathf.Asin(.96f)/3f);
                float boundary=launchTrailDistance-launchNeckArc+Mathf.Lerp(-uncoilSupportBlendU,uncoilSupportBlendU,transition);
                freeSpanDistance=swimTrail[swimTrail.Count-1].distance-boundary;
                float lowDistance=BodyTrailDistance(lower),highDistance=BodyTrailDistance(anchor);
                float fraction=Mathf.InverseLerp(lowDistance,highDistance,freeSpanDistance);
                float span=Mathf.Max(.0001f,highDistance-lowDistance);
                curveRingSpan=lower+fraction;endRing=anchor;continuousFreeNeck=true;
                Vector3 first=(spineCenters[anchor]-spineCenters[lower-1])*(span/Mathf.Max(.0001f,highDistance-BodyTrailDistance(lower-1)));
                Vector3 last=(spineCenters[anchor+1]-spineCenters[lower])*(span/Mathf.Max(.0001f,BodyTrailDistance(anchor+1)-lowDistance));
                continuousAnchor=SpineCurve(spineCenters[lower],spineCenters[anchor],first,last,fraction);
                continuousAnchorDirection=SpineCurveDirection(spineCenters[lower],spineCenters[anchor],first,last,fraction);
            }
        }
        curveLength = continuousFreeNeck?freeSpanDistance:curveRingSpan * smoothBodyLengthU / (BodyRings - 1);
        Vector3 curveEnd = continuousFreeNeck?continuousAnchor:spineCenters[endRing];
        Vector3 curveEndDirection = continuousFreeNeck
            ?continuousAnchorDirection
            :(spineCenters[endRing + 1] - spineCenters[endRing - 1]).normalized;
        if (!following) {
            Vector3 standBaseContact = StandingTailPoint(0f, rootOut, rootSide);
            Vector3 standBase = supportBranch != null ? BranchCoilPoint(0f, spineRadii[endRing]) : standBaseContact + rootOut * GroundSpineOffset(endRing);
            curveEnd = standBase;
            curveEndDirection = supportBranch != null ? (BranchCoilPoint(.02f, spineRadii[endRing]) - standBase).normalized
                : (StandingTailPoint(.02f, rootOut, rootSide) - standBaseContact).normalized;
            for (int ring = endRing; ring < BodyRings; ring++) {
                float behind = (ring - endRing) * smoothBodyLengthU / (BodyRings - 1);
                Vector3 contact = StandingTailPoint(behind, rootOut, rootSide);
                Vector3 outward = new Vector3(contact.x - axisXZ.x, 0f, contact.z - axisXZ.y).normalized;
                Vector3 stand = supportBranch != null ? BranchCoilPoint(behind, spineRadii[ring]) : contact + outward * GroundSpineOffset(ring);
                spineCenters[ring] = stand;
            }
        }
        float handle = continuousFreeNeck?Vector3.Distance(headCenter,curveEnd)*.9f
            :Mathf.Max(Vector3.Distance(headCenter, curveEnd) * .75f, curveLength * .65f);
        Vector3 startTangent = headRotation * Vector3.up * handle;
        Vector3 endTangent = curveEndDirection * handle;
        for (int ring = 0; ring < endRing; ring++) {
            float t = continuousFreeNeck?Mathf.Clamp01(BodyTrailDistance(ring)/Mathf.Max(.001f,freeSpanDistance)):Mathf.Clamp01(ring/curveRingSpan);
            spineCenters[ring] = SpineCurve(headCenter, curveEnd, startTangent, endTangent, t);
            if(continuousFreeNeck && ring>0) {
                Vector3 point=spineCenters[ring],radial=new Vector3(point.x-axisXZ.x,0f,point.z-axisXZ.y);
                float rawRadius=radial.magnitude;Vector3 outwards=rawRadius>.0001f?radial/rawRadius:rootOut;
                float barkRadius=treeSurfaceRadiusU+treeSurfaceClearanceU+spineRadii[ring]*.9f-bellyPressDepthU;
                float belowWood=Vector3.Dot(branchCenter+branchUp*branchHalfHeight-headCenter,branchUp);
                float contact=SmoothVisual01(t/.2f)*(1f-SmoothVisual01(Mathf.InverseLerp(.6f,1f,t)))*SmoothVisual01(belowWood/bodyBarkSettleDropU);
                float radius=Mathf.Lerp(rawRadius,barkRadius,contact);
                float gap=radius-barkRadius;
                float softRadius=barkRadius+(gap+Mathf.Sqrt(gap*gap+bodyBarkContactFeatherU*bodyBarkContactFeatherU))*.5f;
                radius=Mathf.Lerp(radius,softRadius,SmoothVisual01(t/.05f));
                Vector3 side=Vector3.Cross(Vector3.up,outwards);
                point=new Vector3(axisXZ.x,point.y,axisXZ.y)+outwards*radius;
                point+=side*(freeNeckSwayU*Mathf.Sin(phase+t*Mathf.PI*2f)*Mathf.Pow(Mathf.Sin(t*Mathf.PI),4f));
                spineCenters[ring]=point;
            }
            if (!following) {
                float arch = Mathf.Sin(Mathf.PI * t);
                spineCenters[ring] += rootOut * (standingChestLiftU * waitingHeadBlend * arch * arch);
                float nearHood = Mathf.Sin(Mathf.PI * Mathf.Clamp01(ring * smoothBodyLengthU / (BodyRings - 1) / hoodNeckClearanceLengthU));
                spineCenters[ring] -= rootOut * (hoodNeckSetbackU * waitingHeadBlend * nearHood * nearHood);
                Vector3 faceForward=headRotation*originalHeadFaceBasis*Vector3.forward;
                spineCenters[ring] -= faceForward*(risingClearance*nearHood*nearHood);
            }
        }
        for (int ring = 1; ring < BodyRings; ring++) {
            float behind = ring * smoothBodyLengthU / (BodyRings - 1);
            float radius = Mathf.Lerp(headNeckRadiusU, spineRadii[ring], Mathf.SmoothStep(0f, 1f, behind / .4f));
            spineCenters[ring] = KeepClearOfBranch(spineCenters[ring], radius);
        }
        if(following) { SettleRemainingCoil(); if(!continuousFreeNeck)RelaxOriginalRaisedNeck(); }
        FairSpineCorners(following, endRing);
        spineCenters[0] = headCenter;
        referenceHeadVisual.SetPositionAndRotation(headCenter, headRotation);

        float collarLoftU=neckCollarLengthU;
        Vector3 headOutward=new Vector3(headCenter.x-axisXZ.x,0f,headCenter.z-axisXZ.y).normalized;
        Vector3 headBelly=SupportedPaintBelly(headOutward,0f);
        Quaternion frame = headRotation;
        float surface = treeSurfaceRadiusU + treeSurfaceClearanceU - bellyPressDepthU;
        for (int ring = 0; ring < BodyRings; ring++) {
            float behind = ring * smoothBodyLengthU / (BodyRings - 1);
            Vector3 tangent = ring == 0 ? headRotation * Vector3.up
                : (spineCenters[Mathf.Min(BodyRings - 1, ring + 1)] - spineCenters[ring - 1]).normalized;
            if (tangent.sqrMagnitude < .00001f) tangent = frame * Vector3.up;
            spineTangents[ring] = tangent;
            Vector3 outward = new Vector3(spineCenters[ring].x - axisXZ.x, 0f, spineCenters[ring].z - axisXZ.y).normalized;
            float centreRadius = new Vector2(spineCenters[ring].x - axisXZ.x, spineCenters[ring].z - axisXZ.y).magnitude;
            float lift = centreRadius - (surface + spineRadii[ring] * .9f);
            spineContact[ring] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.02f, .15f, lift));
            if (ring > 0) {
                Vector3 transported = Vector3.ProjectOnPlane(frame * Vector3.forward, tangent).normalized;
                if (transported.sqrMagnitude < .00001f) transported = Vector3.ProjectOnPlane(-outward, tangent).normalized;
                frame = Quaternion.LookRotation(transported, tangent);

            }
            float paintBranchWeight=following?RemainingCoilWeight(ring):Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(raisedLength-.25f,raisedLength+.25f,behind));
            Vector3 paintBelly=ring==0?headBelly:SupportedPaintBelly(outward,paintBranchWeight);
            Vector3 right = frame * Vector3.right, belly = frame * Vector3.forward;
            float loft = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(behind / collarLoftU));
            for (int side = 0; side <= BodySides; side++) {
                float angle = (side % BodySides) * Mathf.PI * 2f / BodySides, cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                Vector3 localCircle = new Vector3(cosine * spineRadii[ring], 0f, sine * spineRadii[ring]);
                Vector3 section = Vector3.Lerp(neckRim[side], localCircle, loft);
                Vector3 point = spineCenters[ring] + frame * section - tangent * (NeckOverlapU * (1f - loft));
                Vector3 normal = right * cosine + belly * sine;
                normal = Vector3.Slerp(frame * neckSourceNormals[side], normal, loft).normalized;
                Vector3 radial = new Vector3(point.x - axisXZ.x, 0f, point.z - axisXZ.y);
                if (ring > 0 && radial.magnitude < surface) {
                    radial.Normalize(); point.x = axisXZ.x + radial.x * surface; point.z = axisXZ.y + radial.z * surface;
                    normal = -radial;
                }
                int index = ring * BodyStride + side;
                referenceWaveVertices[index] = toLocal.MultiplyPoint3x4(point);
                referenceWaveNormals[index] = toLocal.MultiplyVector(normal).normalized;
                paintedBellyDirections[index] = toLocal.MultiplyVector(paintBelly).normalized;
            }
        }
        RoundNeckSurface(endRing);
        int tip = BodyRings * BodyStride, neckCap = tip + 1;
        referenceWaveVertices[tip] = toLocal.MultiplyPoint3x4(spineCenters[BodyRings - 1] + spineTangents[BodyRings - 1] * .005f);
        referenceWaveNormals[tip] = toLocal.MultiplyVector(spineTangents[BodyRings - 1]);
        referenceWaveVertices[neckCap] = toLocal.MultiplyPoint3x4(headCenter - headRotation * Vector3.up * NeckOverlapU);
        referenceWaveNormals[neckCap] = toLocal.MultiplyVector(-(headRotation * Vector3.up));
        paintedBellyDirections[neckCap]=toLocal.MultiplyVector(headBelly).normalized;
        paintedBellyDirections[tip]=paintedBellyDirections[(BodyRings-1)*BodyStride];
        SmoothTubeNormals();
        animatedReferenceMesh.vertices = referenceWaveVertices;
        animatedReferenceMesh.normals = referenceWaveNormals;
        animatedReferenceMesh.SetUVs(2,paintedBellyDirections);
        animatedReferenceMesh.RecalculateBounds();
        Vector3 sourceBelly = -Vector3.ProjectOnPlane(SourceEyeForward(),Vector3.up).normalized;
        float palettePhase = .25f-Mathf.Atan2(sourceBelly.z,sourceBelly.x)/(Mathf.PI*2f);
        referenceRenderer.GetPropertyBlock(surfaceBlock);
        surfaceBlock.SetVector("_TreeAxis", new Vector4(axisXZ.x, 0f, axisXZ.y, 0f));
        surfaceBlock.SetFloat("_HeadBellyPhase",palettePhase);
        surfaceBlock.SetVector("_HeadBellyDirection",headBelly);
        surfaceBlock.SetFloat("_CollarLength",neckCollarLengthU);
        surfaceBlock.SetFloat("_HeadWatch", following ? 1f-departure : 1f);
        surfaceBlock.SetFloat("_BodyLength", smoothBodyLengthU);
        referenceRenderer.SetPropertyBlock(surfaceBlock);
        referenceHeadRenderer.GetPropertyBlock(surfaceBlock);
        surfaceBlock.SetVector("_TreeAxis", new Vector4(axisXZ.x, 0f, axisXZ.y, 0f));
        surfaceBlock.SetFloat("_HeadBellyPhase",palettePhase);
        surfaceBlock.SetVector("_HeadBellyDirection",headBelly);
        surfaceBlock.SetFloat("_CollarLength",neckCollarLengthU);
        surfaceBlock.SetFloat("_HeadWatch", following ? waitingHeadBlend : 1f);
        surfaceBlock.SetFloat("_HeadWatch",1f);
        referenceHeadRenderer.SetPropertyBlock(surfaceBlock);
        spinePoseReady = true;
        UpdateTrackingEyes();
        UpdateVisibleContacts();
    }


    private void CreateTrackingEyes(){
        if(!eyeAnchorsReady||referenceHeadVisual==null)return;
        Shader shader=Shader.Find("Universal Render Pipeline/Lit");
        eyeWhiteMaterial=new Material(shader){name="Snake eye white",hideFlags=HideFlags.DontSave};eyeWhiteMaterial.SetColor("_BaseColor",new Color(1f,.98f,.9f));eyeWhiteMaterial.SetFloat("_Smoothness",.35f);
        eyePupilMaterial=new Material(shader){name="Snake pupil",hideFlags=HideFlags.DontSave};eyePupilMaterial.SetColor("_BaseColor",new Color(.004f,.008f,.003f));eyePupilMaterial.SetFloat("_Smoothness",.5f);
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
        int sides=24,rings=5;float aperture=Mathf.Asin(Mathf.Clamp(eyePupilRadiusU/eyeWhiteRadiusU,.1f,.85f));
        for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++){
            float theta=aperture*ring/rings,phi=side*Mathf.PI*2f/sides;
            Vector3 direction=new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Sin(theta)*Mathf.Sin(phi),Mathf.Cos(theta));vertices.Add(direction*(eyeWhiteRadiusU+.004f));normals.Add(direction);
            if(ring<rings&&side<sides){int a=ring*(sides+1)+side,b=a+sides+1;triangles.Add(a);triangles.Add(b);triangles.Add(b+1);triangles.Add(a);triangles.Add(b+1);triangles.Add(a+1);}
        }
        eyePupilMesh=new Mesh{name="Tracking pupil cap",hideFlags=HideFlags.DontSave};eyePupilMesh.SetVertices(vertices);eyePupilMesh.SetNormals(normals);eyePupilMesh.SetTriangles(triangles,0);eyePupilMesh.RecalculateBounds();
        eyePupils=new Transform[2];Vector3[] points={leftEyeSurfaceLocal,rightEyeSurfaceLocal},directions={leftEyeNormalLocal,rightEyeNormalLocal};
        for(int i=0;i<2;i++){
            Vector3 center=points[i]-directions[i].normalized*(eyeWhiteRadiusU-.006f);
            var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.name=i==0?"TrackingEyeLeft":"TrackingEyeRight";eye.transform.SetParent(referenceHeadVisual,false);eye.transform.localPosition=center;eye.transform.localScale=Vector3.one*(eyeWhiteRadiusU*2f);eye.GetComponent<Collider>().enabled=false;Destroy(eye.GetComponent<Collider>());eye.GetComponent<Renderer>().sharedMaterial=eyeWhiteMaterial;
            var pupil=new GameObject("TrackingPupil",typeof(MeshFilter),typeof(MeshRenderer));pupil.transform.SetParent(referenceHeadVisual,false);pupil.transform.localPosition=center;pupil.GetComponent<MeshFilter>().sharedMesh=eyePupilMesh;pupil.GetComponent<Renderer>().sharedMaterial=eyePupilMaterial;eyePupils[i]=pupil.transform;
        }
    }

    private void UpdateTrackingEyes(){
        if(eyePupils==null)return;
        for(int i=0;i<eyePupils.Length;i++){
            Vector3 normal=(i==0?leftEyeNormalLocal:rightEyeNormalLocal).normalized;
            Vector3 direction=monkeyInHeadRange?referenceHeadVisual.InverseTransformDirection(committedTargetPosition-eyePupils[i].position).normalized:normal;
            Vector3 tangent=Vector3.ProjectOnPlane(direction,normal);
            Vector3 gaze=(normal*Mathf.Max(.28f,Vector3.Dot(direction,normal))+tangent).normalized;
            eyePupils[i].localRotation=Quaternion.LookRotation(gaze,originalHeadFaceBasis*Vector3.up);
        }
    }

    private void CreateVisibleContacts(){
        if(referenceHeadVisual==null)return;
        hitCollider.radius=headContactRadiusU;hitCollider.height=headContactRadiusU*2f;
        visibleBodyContacts=new CapsuleCollider[bodyContactSections];
        for(int i=0;i<visibleBodyContacts.Length;i++){
            var part=new GameObject("VisibleSnakeContact_"+i);part.transform.SetParent(transform,false);part.layer=gameObject.layer;part.tag="Branch";
            var shape=part.AddComponent<CapsuleCollider>();shape.direction=1;shape.isTrigger=true;shape.enabled=false;visibleBodyContacts[i]=shape;
        }
    }

    private void UpdateVisibleContacts(){
        if(referenceHeadVisual==null||visibleBodyContacts==null)return;
        bool active=state==State.Falling;
        if(!active){hitCollider.enabled=false;foreach(var part in visibleBodyContacts)part.enabled=false;return;}
        hitCollider.center=transform.InverseTransformPoint(ThreatPosition);hitCollider.radius=headContactRadiusU;hitCollider.height=headContactRadiusU*2f;hitCollider.enabled=active;
        for(int i=0;i<visibleBodyContacts.Length;i++){
            int first=Mathf.RoundToInt(Mathf.Lerp(0f,1f,i/(float)visibleBodyContacts.Length)*(BodyRings-1));
            int last=Mathf.RoundToInt(Mathf.Lerp(0f,1f,(i+1f)/visibleBodyContacts.Length)*(BodyRings-1));
            Vector3 a=spineCenters[first],b=spineCenters[last],delta=b-a;var shape=visibleBodyContacts[i];
            shape.transform.SetPositionAndRotation((a+b)*.5f,delta.sqrMagnitude>.000001f?Quaternion.FromToRotation(Vector3.up,delta):Quaternion.identity);
            float startRadius=Mathf.Lerp(headNeckRadiusU,spineRadii[first],Mathf.SmoothStep(0f,1f,first*smoothBodyLengthU/(BodyRings-1)/.4f));
            float endRadius=Mathf.Lerp(headNeckRadiusU,spineRadii[last],Mathf.SmoothStep(0f,1f,last*smoothBodyLengthU/(BodyRings-1)/.4f));
            shape.radius=Mathf.Max(.015f,Mathf.Max(startRadius,endRadius));shape.height=delta.magnitude+shape.radius*2f;shape.enabled=active;
        }
    }

    private void SweepVisibleHead(){
        Vector3 point=ThreatPosition;if(!headContactReady){previousHeadContact=point;headContactReady=true;}
        int count=Physics.OverlapCapsuleNonAlloc(previousHeadContact,point,headContactRadiusU,sweptHeadHits,~0,QueryTriggerInteraction.Collide);
        previousHeadContact=point;
        for(int i=0;i<count&&state==State.Falling;i++)TryHit(sweptHeadHits[i]);
    }

    private void Start() {
        GameObject playerObject = GameObject.FindGameObjectWithTag(PLAYER_TAG);
        if (playerObject != null) {
            player = playerObject.transform;
            playerArms = playerObject.GetComponentsInChildren<ArmController>(true);
            playerSensor =
                playerObject.GetComponentInChildren<HitBranchSensor>(true);
        }
    }

    private void Update() {
        switch (state) {
            case State.Dormant:
                TryWake();
                break;
            case State.Warning:
                WatchMonkey();
                break;
        }
    }

    private void FixedUpdate() {
        UpdateTrackedMonkey();
        if (state == State.Warning && CanStartFall()) {
            StartFalling();
        }
        if (state == State.Falling) {
            StepFall();
            if(gameObject.activeInHierarchy)SweepVisibleHead();
        }
    }

    private void OnTriggerEnter(Collider other) {
        TryHit(other);
    }

    // Enter/stay and the monkey sensor all share the same one-hit guard.
    private void OnTriggerStay(Collider other) {
        TryHit(other);
    }

    // Editor Validation  ######################################################
    private void OnValidate() {
        triggerDistanceU = Mathf.Max(0f, triggerDistanceU);
        warningDurationS = Mathf.Max(0f, warningDurationS);
        maxFallDistanceU = Mathf.Max(0f, maxFallDistanceU);
        segmentSpacingU = Mathf.Max(0f, segmentSpacingU);
        hitRadiusU = Mathf.Max(0.01f, hitRadiusU);
        referenceSizeMultiplier = Mathf.Max(0.1f, referenceSizeMultiplier);
        bodyWaveSpeedHz = Mathf.Max(0f, bodyWaveSpeedHz);
        bodyWaveCycles = Mathf.Max(0.1f, bodyWaveCycles);
    }

    // constants  ##############################################################
    private const string PLAYER_TAG = "Player";

    // private types  ############################################################
    private enum State {
        Dormant,
        Warning,
        Falling,
        Done,
    }

    // private members  ########################################################
    private State state = State.Dormant;
    private bool isInitialized; // if the spawner armed this cobra
    private float fallSpeedU; // from config; u/s
    private float horizontalSpeedU; // from config; u/s along the ring
    private float reachWakeDistanceU; // from config; wake range mid-grab; u
    private Vector2 axisXZ; // tree axis, world xz
    private float ringRadiusU; // horizontal distance to the axis; u
    private float warningStartTime;
    private float fallStartTime;
    private float fallenU;

    // cached references  ------------------------------------------------------
    private Rigidbody body;
    private CapsuleCollider hitCollider;
    private Transform player;
    private ArmController[] playerArms; // the monkey's arms, read for a reach
    private HitBranchSensor playerSensor; // applies the drop

    // private methods  ########################################################
    // Notice the monkey from above or below; a falling attack still never climbs.
    private Vector3 ThreatPosition => spinePoseReady && referenceHeadVisual != null
        ? referenceHeadVisual.TransformPoint(eyeAnchorsReady?(leftEyeSurfaceLocal+rightEyeSurfaceLocal)*.5f-SourceEyeUp()*.06f:originalHeadFaceCenter) : transform.position;

    private void TryWake() {
        if (!isInitialized || player == null || supportBranch == null) {
            return;
        }
        Vector3 position = ThreatPosition;
        float wakeDistanceU = IsMonkeyReaching()
            ? Mathf.Max(triggerDistanceU, reachWakeDistanceU)
            : triggerDistanceU;
        if (Vector3.Distance(position, MonkeyAimPosition()) >= wakeDistanceU) {
            return;
        }

        state = State.Warning;
        warningStartTime = Time.time;
        fallStartTime = warningStartTime + warningDurationS;
        nextHissTime = Time.time;
    }

    // Called from either side of the trigger pair. Claim the hit before
    // notifying listeners so callback order cannot create duplicate damage.
    public void TryHit(Collider other) {
        if (other == null || state != State.Falling || !hitCollider.enabled
            || GameController.I == null || !GameController.I.IsRunLive
            || GameController.I.IsLeaping || Time.timeScale <= 0f) {
            return;
        }
        HitBranchSensor sensor = other.GetComponent<HitBranchSensor>();
        if (sensor != null || !IsMonkeyPart(other)) {
            return;
        }
        state = State.Done;
        hitCollider.enabled = false;
        HitBranchSensor receiver = sensor != null ? sensor : playerSensor;
        if (receiver != null) {
            receiver.ReportHit(
                hitCollider,
                HitBranchSensor.HitKind.FallingCobraController
            );
        }
        // Keep the existing segment-owned cleanup lifecycle, but remove
        // visuals, physics and movement immediately on the first contact.
        gameObject.SetActive(false);
    }

    /// <returns>true for the monkey's physical parts: body, tail and hands;
    /// false for its other trigger volumes, which reach far past the body
    /// </returns>
    private static bool IsMonkeyPart(Collider other) {
        if(other.GetComponent<DetectionZoneRelay>()!=null)return false;
        if(other.GetComponentInParent<ArmController>(true)!=null)return true;
        if (other.isTrigger) return false;
        for (Transform t = other.transform; t != null; t = t.parent) {
            if (t.CompareTag(PLAYER_TAG)) {
                return true;
            }
        }
        return false;
    }

    private bool IsMonkeyReaching() {
        if (playerArms == null) {
            return false;
        }
        foreach (ArmController arm in playerArms) {
            if (arm != null && arm.IsReaching) {
                return true;
            }
        }
        return false;
    }

    private void WatchMonkey() {
        if (supportBranch == null) { FinishFall(); return; }
        if (player == null || !monkeyInHeadRange) {
            EndWatching();
            return;
        }
        if (hissSource != null && Time.time >= nextHissTime && !hissSource.isPlaying) {
            hissSource.Play();
            nextHissTime = Time.time + Mathf.Max(warningHissIntervalS, warningHiss.length);
        }
    }

    private void EndWatching() {
        state = State.Dormant;
        if (hissSource != null) hissSource.Stop();
    }

    private bool CanStartFall() {
        return player != null && monkeyInHeadRange && Time.time >= fallStartTime
            && (GameController.I == null || !GameController.I.IsLeaping);
    }

    private void StartFalling() {
        if (!monkeyInHeadRange) return;
        float targetSide=player!=null?Vector3.Dot(MonkeyAimPosition()-branchCenter,branchSide):perchedAimSide;
        if(Mathf.Abs(targetSide)>.05f)branchHeadSideSign=Mathf.Sign(targetSide);
        pursuitPaused=false;
        state = State.Falling;
        attackStartedTime = Time.time;
        launchWindupOffset=strikeWindupOffset;
        approachFallScale=1f;headFeedbackReady=false;headAimCommand=0f;descentStopped=false;
        hasCommittedTarget = player != null;
        if (hasCommittedTarget) {
            committedTargetPosition = MonkeyAimPosition();
            Vector2 target = new Vector2(committedTargetPosition.x, committedTargetPosition.z) - axisXZ;
            committedTargetAngle = Mathf.Atan2(target.x, target.y) * Mathf.Rad2Deg;
        }
        if (hissSource != null) hissSource.Stop();
        branchEscapeSide = branchHeadSideSign;
        BeginSwimTrail();
        hitCollider.enabled = true;
        previousHeadContact=ThreatPosition;headContactReady=true;
        UpdateVisibleContacts();
    }

    private Vector2 CommittedAttackVelocity(Vector3 position) {
        if (pursuitPaused) return Vector2.zero;
        float sideways = 0f;
        if (hasCommittedTarget && ringRadiusU > MIN_RING_RADIUS_U) {
            Vector2 radial = new Vector2(position.x, position.z) - axisXZ;
            float angle = Mathf.Atan2(radial.x, radial.y) * Mathf.Rad2Deg;
            float arc = Mathf.DeltaAngle(angle, committedTargetAngle-headSteeringBias) * Mathf.Deg2Rad * ringRadiusU;
            float brake=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(Mathf.Abs(arc)/attackSideBrakeDistanceU));
            float desired=Mathf.Sign(arc)*horizontalSpeedU*brake;
            sideways=Mathf.Clamp(desired,-Mathf.Abs(arc)/Time.fixedDeltaTime,Mathf.Abs(arc)/Time.fixedDeltaTime);
        }
        if(monkeyInHeadRange)sideways=headAimCommand;
        float age=Mathf.Max(0f,Time.time-attackStartedTime);
        float release=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/attackSpeedRampS));
        float burst=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/strikeLaunchRampS))
            *Mathf.Lerp(strikeSpeedMultiplier,1f,Mathf.SmoothStep(0f,1f,Mathf.Clamp01(age/strikeBurstDurationS)));
        release=Mathf.Max(release,burst);
        return new Vector2(sideways, -fallSpeedU*approachFallScale)*release;
    }

    private void FinishFall() {
        state = State.Done;
        hitCollider.enabled = false;
        gameObject.SetActive(false);
    }

    private void StepFall() {
        if (Time.time - attackStartedTime >= maximumFallDurationS) {
            FinishFall();
            return;
        }
        Vector3 position = body.position;
        Vector2 velocity = CommittedAttackVelocity(position);
        float drop = -velocity.y * Time.fixedDeltaTime;
        position.y -= drop;
        if (ringRadiusU > MIN_RING_RADIUS_U) {
            Vector2 radial = new Vector2(position.x, position.z) - axisXZ;
            float angle = Mathf.Atan2(radial.x, radial.y) + velocity.x * Time.fixedDeltaTime / ringRadiusU;
            position.x = axisXZ.x + Mathf.Sin(angle) * ringRadiusU;
            position.z = axisXZ.y + Mathf.Cos(angle) * ringRadiusU;
        }
        body.MovePosition(position);
        AlignReferenceToTree(position);
        body.MoveRotation(body.rotation);
        fallenU += drop;
        if (fallenU >= maxFallDistanceU) FinishFall();
    }

    private const float MIN_RING_RADIUS_U = 0.001f;
}
