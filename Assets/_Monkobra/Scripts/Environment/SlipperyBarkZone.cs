using UnityEngine;

/// <summary>A permanent wet patch on the trunk. The visible wavy outline
/// also defines contact. Each new traversal can lose grip again</summary>
public class SlipperyBarkZone: MonoBehaviour {
    // Public Members  #########################################################
    public float HeightU => heightU;

    // Public Methods  #########################################################
    public void Initialize(float radius, float height, float angle, float arc,
        Material material, bool buildMesh, int seed = 42) {
        radiusU = radius;
        heightU = height;
        centerAngleDeg = angle;
        arcDeg = arc;
        shapeSeed = seed;
        if (buildMesh) BuildMesh(material);
    }

    /// <summary>if the point lies on the puddle-shaped surface, not on its
    /// rectangular dry corners</summary>
    public bool Contains(Vector3 position) {
        return ContainsWithMargin(position, 0f, 0f);
    }

    // Inspector Fields  #######################################################
    [Header("Shape")]
    [SerializeField, Min(0.1f)]
    [Tooltip("patch height; u")]
    private float heightU = 2.6f;

    [SerializeField, Min(0.1f)]
    [Tooltip("trunk surface radius; u")]
    private float radiusU = 5.05f;

    [SerializeField, Range(1f, 360f)]
    [Tooltip("angular width; 360 = full ring; deg")]
    private float arcDeg = 360f;

    [SerializeField]
    [Tooltip("arc middle around trunk; deg")]
    private float centerAngleDeg = 0f;

    [SerializeField]
    [Tooltip("seed of wavy edge outline")]
    private int shapeSeed = 42;

    [Header("Contact")]
    [SerializeField, Min(0f)]
    [Tooltip("radial slack b/t body & trunk surface; u")]
    private float radialToleranceU = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("extra reach below entry edge b4 re-arming slip; u")]
    private float passageMarginU = 0.25f;

    // MonoBehaviour Lifecycle  ################################################
    private void Start() {
        monkey = FindFirstObjectByType<MonkeyController>();
    }

    private void FixedUpdate() {
        if (monkey == null || GameController.I == null
            || !GameController.I.IsRunLive) return;
        Vector3 point = monkey.transform.position;
        if (slippedThisPass) {
            // slide can carry body below entry edge, so latch until real
            // exit: holding up then crosses a full ring. Re-arm once body
            // clears visible top/sides; only lower edge needs hysteresis
            if (!monkey.IsSlipping && !ContainsWithMargin(point,
                0f, reentryBelowU + passageMarginU)) slippedThisPass = false;
            else return;
        }
        if (Contains(point) && monkey.TrySlip()) {
            slippedThisPass = true;
            reentryBelowU = monkey.CurrentSlipDistanceU;
        }
    }

    private void OnDestroy() {
        ReleaseMesh();
    }

    // private members  ########################################################
    private MonkeyController monkey;
    private Mesh ownedMesh;
    private bool slippedThisPass;
    private float reentryBelowU;

    // private methods  ########################################################
    private bool ContainsWithMargin(Vector3 position, float margin,
        float below) {
        Vector3 offset = position - transform.position;
        if (Mathf.Abs(new Vector2(offset.x, offset.z).magnitude - radiusU)
            > radialToleranceU + margin) return false;
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(centerAngleDeg, angle);
        float angularMargin = margin / radiusU * Mathf.Rad2Deg;
        if (arcDeg < 360f && Mathf.Abs(delta) > arcDeg * 0.5f + angularMargin)
            return false;
        float t = arcDeg >= 360f ? Mathf.Repeat(angle, 360f) / 360f
            : Mathf.Clamp01(delta / arcDeg + 0.5f);
        GetEdges(t, out float bottom, out float top);
        return offset.y >= bottom - margin - below && offset.y <= top + margin;
    }

    // independent meandering edges & rounded ends make a splash on bark
    private void GetEdges(float t, out float bottom, out float top) {
        float phase = shapeSeed * 0.61803399f;
        float a = t * Mathf.PI * 2f;
        float taper = arcDeg >= 360f ? 1f
            : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(t * 2f - 1f, 2f)));
        float center = heightU * 0.12f
            * (Mathf.Sin(a * 2f + phase) + 0.45f * Mathf.Sin(a * 5f - phase));
        float upper = 1f + 0.16f * Mathf.Sin(a * 3f + phase)
            + 0.08f * Mathf.Sin(a * 7f + phase * 2f);
        float lower = 1f + 0.18f * Mathf.Sin(a * 4f - phase)
            + 0.08f * Mathf.Cos(a * 6f + phase);
        bottom = center - heightU * 0.5f * taper * lower;
        top = center + heightU * 0.5f * taper * upper;
    }

    private void ReleaseMesh() {
        if (ownedMesh == null) return;
        if (Application.isPlaying) Destroy(ownedMesh);
        else DestroyImmediate(ownedMesh);
        ownedMesh = null;
    }

    private void BuildMesh(Material material) {
        int steps = Mathf.Max(96, Mathf.CeilToInt(arcDeg));
        Vector3[] vertices = new Vector3[(steps + 1) * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[steps * 6];
        for (int i = 0; i <= steps; i++) {
            float t = i / (float)steps;
            float degrees = centerAngleDeg + (t - 0.5f) * arcDeg;
            float angle = degrees * Mathf.Deg2Rad;
            Vector3 normal = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float shapeT = arcDeg >= 360f
                ? Mathf.Repeat(degrees, 360f) / 360f : t;
            GetEdges(shapeT, out float bottom, out float top);
            vertices[2 * i] = normal * radiusU + Vector3.up * bottom;
            vertices[2 * i + 1] = normal * radiusU + Vector3.up * top;
            normals[2 * i] = normals[2 * i + 1] = normal;
            uv[2 * i] = new Vector2(t, 0f);
            uv[2 * i + 1] = new Vector2(t, 1f);
            if (i == steps) continue;
            int v = i * 2;
            int j = i * 6;
            triangles[j] = v;
            triangles[j + 1] = v + 2;
            triangles[j + 2] = v + 1;
            triangles[j + 3] = v + 1;
            triangles[j + 4] = v + 2;
            triangles[j + 5] = v + 3;
        }
        Mesh mesh = new() { name = "Irregular wet bark", vertices = vertices,
            normals = normals, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        Renderer surface = GetComponent<Renderer>();
        if (surface == null) surface = gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = mesh;
        surface.sharedMaterial = material;
        ReleaseMesh();
        ownedMesh = mesh;
    }
}
