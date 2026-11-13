using UnityEngine;

/// <summary>
/// Builds the Spider King from Unity primitives & poses it flat on the
/// trunk, its back facing out & its head up the trunk: a dark body w/ a red
/// mark, a small gold crown, eight legs & eight eyes that glow red as an
/// attack winds up. Owned & moved by <see cref="SpiderFightController"/>.
/// </summary>
[DisallowMultipleComponent]
public class SpiderKingBody: MonoBehaviour {
    // Public Members  #########################################################
    /// <returns>if the body is built & shown</returns>
    public bool IsBuilt => root != null;

    /// <returns>angle round the trunk the body sits at; deg</returns>
    public float AngleDeg { get; private set; }

    /// <returns>world height the body's center sits at</returns>
    public float Y { get; private set; }

    /// <returns>world center of the body</returns>
    public Vector3 Position => root != null ? root.position : transform.position;

    /// <returns>world point between the big eyes, where webs & silk start
    /// </returns>
    public Vector3 HeadPosition => head != null
        ? head.position
        : Position;

    /// <returns>the body's trigger, the source of a leg swipe knock down
    /// </returns>
    public Collider Contact { get; private set; }

    /// <returns>lit material the controller's own primitives use too
    /// </returns>
    public Material PrimitiveMaterial => bodyMaterial;

    // Public Methods  #########################################################
    /// <summary>Builds the body at a spot on the trunk.</summary>
    /// <param name="center">trunk axis point, its y ignored</param>
    /// <param name="angleDeg">angle round the trunk; deg</param>
    /// <param name="y">world height</param>
    public void Build(Vector3 center, float angleDeg, float y) {
        Clear();
        trunkCenter = center;
        colorBlock ??= new MaterialPropertyBlock();

        root = new GameObject("SpiderKing").transform;
        root.SetParent(transform, false);
        SphereCollider sphere = root.gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 1.6f;
        Contact = sphere;

        // local space: x along the trunk's side, y out from the trunk,
        // z up the trunk, toward the head
        AddPart(PrimitiveType.Sphere, "Thorax", new Vector3(0f, 0f, 0.9f),
            new Vector3(1.6f, 1f, 1.6f), bodyColor);
        AddPart(PrimitiveType.Sphere, "Abdomen", new Vector3(0f, 0.15f, -1.2f),
            new Vector3(2.2f, 1.5f, 2.6f), bodyColor);
        AddPart(PrimitiveType.Sphere, "Mark", new Vector3(0f, 0.85f, -1.2f),
            new Vector3(0.8f, 0.3f, 1.1f), markColor);
        head = AddPart(PrimitiveType.Sphere, "Head",
            new Vector3(0f, 0.2f, 1.6f), new Vector3(1f, 0.7f, 0.8f),
            bodyColor).transform;

        // the eyes: two big, four small, all in one glow
        Vector3[] eyeSpots = {
            new Vector3(-0.25f, 0.5f, 1.9f), new Vector3(0.25f, 0.5f, 1.9f),
            new Vector3(-0.5f, 0.45f, 1.75f), new Vector3(0.5f, 0.45f, 1.75f),
            new Vector3(-0.15f, 0.62f, 1.7f), new Vector3(0.15f, 0.62f, 1.7f),
        };
        eyes = new Renderer[eyeSpots.Length];
        for (int i = 0; i < eyeSpots.Length; i++) {
            float size = i < 2 ? 0.3f : 0.17f;
            eyes[i] = AddPart(PrimitiveType.Sphere, "Eye", eyeSpots[i],
                Vector3.one * size, eyeDarkColor).GetComponent<Renderer>();
        }
        AddLimb("Fang", new Vector3(-0.2f, 0f, 1.85f),
            new Vector3(-0.15f, -0.25f, 2.25f), 0.12f, markColor, root);
        AddLimb("Fang", new Vector3(0.2f, 0f, 1.85f),
            new Vector3(0.15f, -0.25f, 2.25f), 0.12f, markColor, root);

        // the crown, small & gold on the thorax: a band & four points
        AddPart(PrimitiveType.Cylinder, "Crown", new Vector3(0f, 0.6f, 0.85f),
            new Vector3(0.8f, 0.08f, 0.8f), crownColor);
        for (int i = 0; i < 4; i++) {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            GameObject point = AddPart(PrimitiveType.Cube, "CrownPoint",
                new Vector3(Mathf.Cos(a) * 0.32f, 0.78f,
                    0.85f + Mathf.Sin(a) * 0.32f),
                new Vector3(0.14f, 0.3f, 0.14f), crownColor);
            point.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        }

        BuildLegs();
        Place(angleDeg, y, 0f);
        SetEyeGlow(0f);
    }

    /// <summary>
    /// Poses the body on the trunk & walks its legs; call every frame.
    /// </summary>
    /// <param name="angleDeg">angle round the trunk; deg</param>
    /// <param name="y">world height</param>
    /// <param name="walk">how hard the legs walk, 0 still, 1 a full run
    /// </param>
    public void Place(float angleDeg, float y, float walk) {
        if (root == null) {
            return;
        }
        AngleDeg = angleDeg;
        Y = y;
        float radians = angleDeg * Mathf.Deg2Rad;
        Vector3 outward = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        Vector3 spot = new Vector3(trunkCenter.x, y, trunkCenter.z)
            + outward * (trunkRadiusU + bodyOutU);
        // a little bob while it walks or eats
        float bob = Mathf.Sin(Time.time * (eating ? 14f : 9f))
            * (eating ? 0.08f : 0.05f * walk);
        root.SetPositionAndRotation(
            spot + outward * bob,
            Quaternion.LookRotation(Vector3.up, outward)
        );

        walkPhase += Time.deltaTime * Mathf.Lerp(0f, 12f, walk);
        for (int i = 0; i < hips.Length; i++) {
            // the legs go in two alternating sets of four
            float set = (i % 2 == (i / 4) % 2) ? 0f : Mathf.PI;
            float swing = Mathf.Sin(walkPhase + set) * 18f * walk;
            float lift = 0f;
            // the front pair rears up for a swipe
            if (i == 0 || i == 4) {
                lift = -70f * swipe;
            }
            hips[i].localRotation = hipRest[i]
                * Quaternion.AngleAxis(swing, Vector3.up)
                * Quaternion.AngleAxis(lift, Vector3.forward
                    * (i < 4 ? 1f : -1f));
        }
    }

    /// <summary>Sets how far an attack has wound up: 0 dark eyes, 1 lit.
    /// </summary>
    public void SetEyeGlow(float glow) {
        if (eyes == null) {
            return;
        }
        Color color = Color.Lerp(eyeDarkColor, eyeLitColor, Mathf.Clamp01(glow));
        foreach (Renderer eye in eyes) {
            SetColor(eye, color);
        }
    }

    /// <summary>Sets how far the front legs rear up for a swipe, 0~1.
    /// </summary>
    public void SetSwipe(float amount) {
        swipe = Mathf.Clamp01(amount);
    }

    /// <summary>Switches the quick eating bob on or off.</summary>
    public void SetEating(bool isEating) {
        eating = isEating;
    }

    /// <summary>Removes the built body.</summary>
    public void Clear() {
        if (root != null) {
            Destroy(root.gameObject);
        }
        root = null;
        head = null;
        eyes = null;
        Contact = null;
        hips = new Transform[0];
        hipRest = new Quaternion[0];
    }

    /// <summary>
    /// Detaches the body to fall off the trunk, curled & spinning; the
    /// caller destroys it via <see cref="Clear"/> once it is out of view.
    /// </summary>
    public void Fall(float timeS) {
        if (root == null) {
            return;
        }
        Vector3 outward = root.position - new Vector3(trunkCenter.x,
            root.position.y, trunkCenter.z);
        outward.y = 0f;
        root.position += (outward.normalized * 3f
            + Vector3.down * (2f + 9f * timeS)) * Time.deltaTime;
        root.Rotate(Vector3.forward, 400f * Time.deltaTime, Space.Self);
        swipe = 1f;
        for (int i = 0; i < hips.Length; i++) {
            hips[i].localRotation = Quaternion.Slerp(
                hips[i].localRotation,
                hipRest[i] * Quaternion.AngleAxis(60f, Vector3.right),
                Time.deltaTime * 4f
            );
        }
    }

    // Inspector Fields  #######################################################
    [Tooltip("lit material for every part, tinted per part")]
    [SerializeField]
    private Material bodyMaterial;

    [Tooltip("trunk radius the body sits on; u")]
    [SerializeField]
    [Min(0.5f)]
    private float trunkRadiusU = 5f;

    [Tooltip("how far the body's center sits out from the bark; u")]
    [SerializeField]
    [Min(0f)]
    private float bodyOutU = 0.8f;

    [Tooltip("body & leg color")]
    [SerializeField]
    private Color bodyColor = new Color(0.16f, 0.09f, 0.2f);

    [Tooltip("the mark on its back & the fangs")]
    [SerializeField]
    private Color markColor = new Color(0.8f, 0.1f, 0.12f);

    [Tooltip("the crown")]
    [SerializeField]
    private Color crownColor = new Color(1f, 0.8f, 0.15f);

    [Tooltip("eye color at rest")]
    [SerializeField]
    private Color eyeDarkColor = new Color(0.35f, 0.02f, 0.05f);

    [Tooltip("eye color when an attack is about to land")]
    [SerializeField]
    private Color eyeLitColor = new Color(1f, 0.2f, 0.1f);

    // private methods  ########################################################
    private void Awake() {
        colorBlock = new MaterialPropertyBlock();
    }

    private void BuildLegs() {
        // hips along each side, the legs fanned from forward to back
        float[] hipZ = { 1.2f, 0.75f, 0.3f, -0.15f };
        float[] fanDeg = { 50f, 18f, -18f, -50f };
        hips = new Transform[8];
        hipRest = new Quaternion[8];
        for (int side = 0; side < 2; side++) {
            float sign = side == 0 ? -1f : 1f;
            for (int i = 0; i < 4; i++) {
                int index = side * 4 + i;
                Transform hip = new GameObject("Hip").transform;
                hip.SetParent(root, false);
                hip.localPosition = new Vector3(sign * 0.6f, 0f, hipZ[i]);
                // fanned about the out axis, mirrored per side
                hip.localRotation = Quaternion.AngleAxis(
                    sign * -fanDeg[i], Vector3.up
                );
                hips[index] = hip;
                hipRest[index] = hip.localRotation;
                Vector3 knee = new Vector3(sign * 1.4f, 1f, 0f);
                Vector3 foot = new Vector3(sign * 2.6f, -0.45f, 0f);
                AddLimb("Thigh", Vector3.zero, knee, 0.2f, bodyColor, hip);
                AddLimb("Shin", knee, foot, 0.15f, bodyColor, hip);
            }
        }
    }

    private GameObject AddPart(
        PrimitiveType type,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color
    ) {
        GameObject part = CreatePart(type, partName, root, color);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        return part;
    }

    // a cylinder from one local point to another, under the given parent
    private void AddLimb(
        string partName,
        Vector3 from,
        Vector3 to,
        float thickness,
        Color color,
        Transform parent
    ) {
        GameObject part = CreatePart(PrimitiveType.Cylinder, partName, parent,
            color);
        Vector3 span = to - from;
        part.transform.localPosition = (from + to) * 0.5f;
        part.transform.localRotation =
            Quaternion.FromToRotation(Vector3.up, span.normalized);
        part.transform.localScale =
            new Vector3(thickness, span.magnitude * 0.5f, thickness);
    }

    private GameObject CreatePart(
        PrimitiveType type,
        string partName,
        Transform parent,
        Color color
    ) {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        Renderer partRenderer = part.GetComponent<Renderer>();
        if (bodyMaterial != null) {
            partRenderer.sharedMaterial = bodyMaterial;
        }
        SetColor(partRenderer, color);
        return part;
    }

    private void SetColor(Renderer target, Color color) {
        if (target == null) {
            return;
        }
        colorBlock ??= new MaterialPropertyBlock();
        target.GetPropertyBlock(colorBlock);
        colorBlock.SetColor(BASE_COLOR, color);
        colorBlock.SetColor(COLOR, color);
        target.SetPropertyBlock(colorBlock);
    }

    // private members  ########################################################
    private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");
    private static readonly int COLOR = Shader.PropertyToID("_Color");

    private Transform root;
    private Transform head;
    private Renderer[] eyes;
    private Transform[] hips = new Transform[0];
    private Quaternion[] hipRest = new Quaternion[0];
    private Vector3 trunkCenter;
    private MaterialPropertyBlock colorBlock;
    private float walkPhase;
    private float swipe;
    private bool eating;
}
