using System.Collections.Generic;
using UnityEngine;

/// <summary>Raised, irregular bark that smoothly slows movement in every direction while touched.</summary>
public class RoughBarkZone: MonoBehaviour {
    // Public Members  #########################################################
    public float HeightU => heightU;
    public static float GetVerticalExtent(float height) => height * 0.85f;

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

    /// <summary>Raised, irregular bark that smoothly slows movement in every direction while touched.</summary>
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


    // MonoBehaviour Lifecycle  ################################################
    private void Start() {
        monkey = FindFirstObjectByType<MonkeyController>();
    }

    private void FixedUpdate() {
        if (monkey == null) return;
        bool touching = isActiveAndEnabled && GameController.I != null
            && GameController.I.IsRunLive && !GameController.I.IsLeaping
            && Contains(monkey.transform.position);
        monkey.SetRoughBarkContact(this, touching);
    }

    private void OnDisable() {
        if (monkey != null) monkey.SetRoughBarkContact(this, false);
    }
    private void OnDestroy() {
        ReleaseMesh();
    }

    // private members  ########################################################
    private MonkeyController monkey;
    private Mesh ownedMesh;
    private readonly List<Vector2> trunkProfile = new();

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
        ReadTrunkProfile();
        const int rows = 12;
        int columns = Mathf.Max(48, Mathf.CeilToInt(arcDeg));
        Vector3[] vertices = new Vector3[(columns + 1) * (rows + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[columns * rows * 6];
        float phase = shapeSeed * 0.61803399f;
        for (int x = 0; x <= columns; x++) {
            float t = x / (float)columns;
            float degrees = centerAngleDeg + (t - 0.5f) * arcDeg;
            float angle = degrees * Mathf.Deg2Rad;
            Vector3 normal = new(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float shapeT = arcDeg >= 360f ? Mathf.Repeat(degrees, 360f) / 360f : t;
            GetEdges(shapeT, out float bottom, out float top);
            for (int y = 0; y <= rows; y++) {
                float v = y / (float)rows;
                float edge = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(v * Mathf.PI);
                float bumps = 0.018f + 0.012f * Mathf.Sin(t * 47f + phase)
                    * Mathf.Sin(v * 23f - phase);
                int index = x * (rows + 1) + y;
                // Follow the actual faceted trunk, rather than floating on
                // an enclosing smooth cylinder. Relief remains shallow;
                // the bark normal map supplies the smaller cracks and ridges.
                vertices[index] = normal * (ConformedRadius(normal) + 0.005f + bumps * edge)
                    + Vector3.up * Mathf.Lerp(bottom, top, v);
                uv[index] = new Vector2(t * radiusU * arcDeg * Mathf.Deg2Rad * 0.25f,
                    Mathf.Lerp(bottom, top, v) * 0.25f);
                if (x == columns || y == rows) continue;
                int j = (x * rows + y) * 6;
                int next = index + rows + 1;
                triangles[j] = index; triangles[j + 1] = next; triangles[j + 2] = index + 1;
                triangles[j + 3] = index + 1; triangles[j + 4] = next; triangles[j + 5] = next + 1;
            }
        }
        Mesh mesh = new() { name = "Raised rough bark", vertices = vertices, uv = uv, triangles = triangles };
        mesh.RecalculateNormals();
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

    private void ReadTrunkProfile() {
        trunkProfile.Clear();
        if (transform.parent == null) return;
        foreach (MeshFilter filter in transform.parent.GetComponentsInChildren<MeshFilter>()) {
            if (filter.sharedMesh == null || filter.sharedMesh.name != "Cylinder"
                || Mathf.Min(Mathf.Abs(filter.transform.lossyScale.x),
                    Mathf.Abs(filter.transform.lossyScale.z)) < radiusU) continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices) {
                Vector3 offset = filter.transform.TransformPoint(vertex) - transform.position;
                Vector2 point = new(offset.x, offset.z);
                if (point.magnitude < radiusU * 0.5f) continue;
                bool duplicate = false;
                foreach (Vector2 existing in trunkProfile) {
                    if ((existing - point).sqrMagnitude < 0.0001f) { duplicate = true; break; }
                }
                if (!duplicate) trunkProfile.Add(point);
            }
            if (trunkProfile.Count >= 3) break;
        }
        trunkProfile.Sort((a, b) => Mathf.Atan2(a.x, a.y).CompareTo(Mathf.Atan2(b.x, b.y)));
    }

    private float ConformedRadius(Vector3 normal) {
        Vector2 ray = new(normal.x, normal.z);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < trunkProfile.Count; i++) {
            Vector2 a = trunkProfile[i];
            Vector2 edge = trunkProfile[(i + 1) % trunkProfile.Count] - a;
            float divisor = ray.x * edge.y - ray.y * edge.x;
            if (Mathf.Abs(divisor) < 0.00001f) continue;
            float distance = (a.x * edge.y - a.y * edge.x) / divisor;
            float along = (a.x * ray.y - a.y * ray.x) / divisor;
            if (distance > 0f && along >= -0.0001f && along <= 1.0001f)
                nearest = Mathf.Min(nearest, distance);
        }
        return float.IsPositiveInfinity(nearest) ? radiusU : nearest;
    }
}
