using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Cosmetic mesh and face shared by the chasing and falling cobras.
/// The existing controllers continue to own movement and all hit shapes.
/// </summary>
public sealed class CartoonCobraVisual: IDisposable {
    [Serializable]
    public sealed class Settings {
        public bool enabled = true;
        public GameObject referenceHead;
        public GameObject referenceBody;
        public Color skin = new Color(0.25f, 0.66f, 0.055f);
        public Color belly = new Color(1f, 0.83f, 0.32f);
        public Color spots = new Color(0.52f, 0.85f, 0.16f);
        [Range(0f, 1f)] public float smoothness = 0.38f;
        [Range(0.5f, 2f)] public float headScale = 1f;
    }

    public CartoonCobraVisual(Transform owner, Material source, Settings settings) {
        this.owner = owner;
        this.settings = settings;
        skin = MakeMaterial(source, settings.skin, "Cartoon snake green");
        belly = MakeMaterial(source, settings.belly, "Cartoon snake belly");
        spots = MakeMaterial(source, settings.spots, "Cartoon snake spots");
        skinPattern = new Texture2D(128, 128, TextureFormat.RGBA32, false) {
            name = "Rounded green snake markings",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };
        Color[] pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++) {
            for (int x = 0; x < 128; x++) {
                float u = (x / 127f - 0.75f) / 0.16f;
                float v = (y / 127f - 0.5f) / 0.28f;
                float spot = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, u * u + v * v));
                pixels[y * 128 + x] = Color.Lerp(settings.skin, settings.spots, spot);
            }
        }
        skinPattern.SetPixels(pixels);
        skinPattern.Apply(false, true);
        skin.SetTexture("_BaseMap", skinPattern);
        skin.SetTexture("_MainTex", skinPattern);
        skin.SetColor("_BaseColor", Color.white);
        skin.SetColor("_Color", Color.white);
        dark = MakeMaterial(source, new Color(0.07f, 0.16f, 0.025f), "Snake face");
        ivory = MakeMaterial(source, new Color(1f, 0.96f, 0.78f), "Snake ivory");
        pupil = MakeMaterial(source, new Color(0.09f, 0.055f, 0.025f), "Snake pupils");
        white = MakeMaterial(source, Color.white, "Snake eye highlights");

        GameObject body = new GameObject("CartoonSnakeBody");
        body.transform.SetParent(owner, false);
        mesh = new Mesh { name = "Continuous cartoon snake body" };
        mesh.MarkDynamic();
        body.AddComponent<MeshFilter>().sharedMesh = mesh;
        body.AddComponent<MeshRenderer>().sharedMaterials = new[] { skin, belly, spots };
    }

    /// <summary>Face coordinates: +y toward the head, -z away from the tree.</summary>
    public void BuildHead(Transform anchor, float diameter) {
        GameObject face = settings.referenceHead != null
            ? Object.Instantiate(settings.referenceHead)
            : new GameObject("CartoonSnakeHead");
        face.name = "CartoonSnakeHead";
        face.transform.SetParent(owner, false);
        face.transform.position = anchor.position;
        face.transform.rotation = anchor.rotation;
        head = face.transform;
        Vector3 scale = owner.lossyScale;
        float size = diameter * settings.headScale;
        face.transform.localScale = new Vector3(
            size / scale.x, size / scale.y, size / scale.z
        );
        if (settings.referenceHead != null) {
            Mesh sourceMesh = face.GetComponent<MeshFilter>().sharedMesh;
            List<Vector3> collar = new();
            foreach (Vector3 point in sourceMesh.vertices) {
                if (point.y <= sourceMesh.bounds.min.y + 0.001f) {
                    collar.Add(new Vector3(point.x, sourceMesh.bounds.min.y, point.z));
                    referenceNeckRadius = Mathf.Max(referenceNeckRadius,
                        new Vector2(point.x, point.z).magnitude * size);
                }
            }
            referenceNeckRing = new Vector3[STRIDE];
            for (int edge = 0; edge <= SIDES; edge++) {
                float angle = (edge % SIDES) * Mathf.PI * 2f / SIDES;
                Vector2 direction = new Vector2(-Mathf.Cos(angle), -Mathf.Sin(angle));
                float closestAngle = float.PositiveInfinity;
                foreach (Vector3 point in collar) {
                    float difference = Mathf.Abs(Vector2.SignedAngle(direction, new Vector2(point.x, point.z)));
                    if (difference < closestAngle) {
                        closestAngle = difference;
                        referenceNeckRing[edge] = point;
                    }
                }
            }
            return;
        }
        Transform root = face.transform;

        Part(root, "Rounded hood", skin, new Vector3(0f, -0.35f, 0.18f),
            new Vector3(2.25f, 1.85f, 0.34f));
        Part(root, "Hood inset", spots, new Vector3(0f, -0.35f, -0.005f),
            new Vector3(1.91f, 1.55f, 0.15f));
        Part(root, "Head", skin, new Vector3(0f, 0.15f, -0.13f),
            new Vector3(1.45f, 1.05f, 0.93f));
        Part(root, "Yellow jaw", belly, new Vector3(0f, -0.13f, -0.45f),
            new Vector3(1.30f, 0.43f, 0.58f));
        Part(root, "Rounded muzzle", skin, new Vector3(0f, 0.035f, -0.57f),
            new Vector3(1.15f, 0.50f, 0.49f));
        Part(root, "Smile", dark, new Vector3(0f, -0.19f, -0.721f),
            new Vector3(0.86f, 0.028f, 0.035f));

        for (int side = -1; side <= 1; side += 2) {
            Part(root, "Eye white", ivory, new Vector3(side * 0.36f, 0.34f, -0.51f),
                new Vector3(0.43f, 0.47f, 0.22f));
            Part(root, "Pupil", pupil, new Vector3(side * 0.32f, 0.30f, -0.628f),
                new Vector3(0.19f, 0.28f, 0.075f), true);
            Part(root, "Eye shine", white, new Vector3(side * 0.32f - 0.035f, 0.37f, -0.672f),
                new Vector3(0.065f, 0.075f, 0.025f), true);
            Transform brow = Part(root, "Determined brow", skin,
                new Vector3(side * 0.36f, 0.53f, -0.565f),
                new Vector3(0.56f, 0.18f, 0.24f));
            brow.localRotation = Quaternion.Euler(0f, 0f, side * 23f);
            Part(root, "Nostril", dark, new Vector3(side * 0.14f, 0.10f, -0.784f),
                new Vector3(0.06f, 0.075f, 0.025f), true);
            Transform tooth = Part(root, "Little fang", ivory,
                new Vector3(side * 0.35f, -0.30f, -0.69f),
                new Vector3(0.095f, 0.24f, 0.10f));
            tooth.localRotation = Quaternion.Euler(0f, 0f, side * -9f);
        }
    }

    public void PoseHead(Vector3 position, Quaternion rotation) {
        if (head != null) {
            head.SetPositionAndRotation(position, rotation);
        }
    }

    /// <summary>Replace visible beads with a continuous two-tone tube.</summary>
    public void UpdateBody(Vector3[] centers, float[] radii, Vector3[] outward, bool bellyInward = false) {
        if (centers.Length < 2) {
            return;
        }
        if (head != null && referenceNeckRadius > 0f && centers.Length > 9) {
            centers = (Vector3[])centers.Clone();
            radii = (float[])radii.Clone();
            const int neckRings = 8;
            Vector3 start = head.position;
            Vector3 end = centers[neckRings];
            float length = Vector3.Distance(start, end);
            Vector3 startTangent = -head.up * length;
            Vector3 endTangent = (centers[neckRings + 1] - centers[neckRings - 1]).normalized * length;
            for (int i = 0; i < neckRings; i++) {
                float t = i / (float)neckRings;
                float t2 = t * t;
                float t3 = t2 * t;
                centers[i] = (2f * t3 - 3f * t2 + 1f) * start
                    + (t3 - 2f * t2 + t) * startTangent
                    + (-2f * t3 + 3f * t2) * end
                    + (t3 - t2) * endTangent;
                radii[i] = Mathf.Lerp(referenceNeckRadius, radii[i], t);
            }
        }
        if (vertices == null || vertices.Length != (centers.Length - 1) * STRIDE + 2) {
            BuildTopology(centers.Length);
        }
        for (int ring = 0; ring < centers.Length - 1; ring++) {
            Vector3 tangent = ring == 0 ? centers[0] - centers[1]
                : centers[ring - 1] - centers[ring + 1];
            if (ring == 0 && referenceNeckRadius > 0f) {
                tangent = head.up;
            }
            tangent.Normalize();
            Vector3 front = Vector3.ProjectOnPlane(bellyInward ? -outward[ring] : outward[ring], tangent).normalized;
            if (front.sqrMagnitude < 0.01f) {
                front = Vector3.ProjectOnPlane(Vector3.forward, tangent).normalized;
            }
            Vector3 side = Vector3.Cross(tangent, front).normalized;
            for (int edge = 0; edge <= SIDES; edge++) {
                float angle = edge == SIDES ? 0f : edge * Mathf.PI * 2f / SIDES;
                Vector3 normal = side * Mathf.Cos(angle) + front * Mathf.Sin(angle);
                int index = ring * STRIDE + edge;
                Vector3 point = ring == 0 && referenceNeckRing != null
                    ? head.TransformPoint(referenceNeckRing[edge])
                    : centers[ring] + normal * radii[ring];
                vertices[index] = owner.InverseTransformPoint(point);
                normals[index] = owner.InverseTransformDirection(normal);
            }
        }
        int firstCap = vertices.Length - 2;
        int lastCap = vertices.Length - 1;
        vertices[firstCap] = owner.InverseTransformPoint(centers[0]);
        vertices[lastCap] = owner.InverseTransformPoint(centers[centers.Length - 1]);
        normals[firstCap] = owner.InverseTransformDirection((centers[0] - centers[1]).normalized);
        normals[lastCap] = owner.InverseTransformDirection((centers[centers.Length - 1] - centers[centers.Length - 2]).normalized);
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.RecalculateBounds();
    }

    public void Dispose() {
        Object.Destroy(mesh);
        Object.Destroy(skinPattern);
        foreach (Material material in materials) {
            Object.Destroy(material);
        }
    }

    private const int SIDES = 24;
    private const int STRIDE = SIDES + 1;
    private readonly Transform owner;
    private readonly Settings settings;
    private readonly Mesh mesh;
    private readonly Texture2D skinPattern;
    private readonly Material skin, belly, spots, dark, ivory, pupil, white;
    private readonly List<Material> materials = new();
    private Vector3[] vertices;
    private Vector3[] normals;
    private Transform head;
    private float referenceNeckRadius;
    private Vector3[] referenceNeckRing;

    private Material MakeMaterial(Material source, Color color, string name) {
        Material material = new Material(source) { name = name };
        if (settings.referenceHead != null) {
            material.shader = settings.referenceHead.GetComponent<Renderer>().sharedMaterial.shader;
            if (material.HasProperty("_SurfaceLighting")) {
                material.SetFloat("_SurfaceLighting", 1f);
            }
        }
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) {
            material.SetFloat("_Smoothness", settings.smoothness);
        }
        materials.Add(material);
        return material;
    }

    private static Transform Part(Transform parent, string name, Material material,
        Vector3 position, Vector3 scale, bool keepColor = false) {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        if (keepColor) {
            MaterialPropertyBlock block = new();
            block.SetColor("_BaseColor", material.GetColor("_BaseColor"));
            renderer.SetPropertyBlock(block);
        }
        return part.transform;
    }

    private void BuildTopology(int count) {
        int rings = count - 1;
        vertices = new Vector3[rings * STRIDE + 2];
        normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        for (int ring = 0; ring < rings; ring++) {
            for (int edge = 0; edge <= SIDES; edge++) {
                uv[ring * STRIDE + edge] = new Vector2(edge / (float)SIDES, ring * 0.09f);
            }
        }
        List<int>[] triangles = { new(), new(), new() };
        for (int ring = 0; ring < rings - 1; ring++) {
            for (int edge = 0; edge < SIDES; edge++) {
                int next = edge + 1;
                float angle = (edge + 0.5f) * Mathf.PI * 2f / SIDES;
                int material = Mathf.Sin(angle) > 0.5f ? 1 : 0;
                List<int> indices = triangles[material];
                int a = ring * STRIDE + edge;
                int b = ring * STRIDE + next;
                int c = (ring + 1) * STRIDE + edge;
                int d = (ring + 1) * STRIDE + next;
                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(b); indices.Add(d); indices.Add(c);
            }
        }
        // Both ends are closed. A single tail vertex avoids a ring of zero-area faces.
        int firstCap = vertices.Length - 2;
        int lastCap = vertices.Length - 1;
        uv[firstCap] = new Vector2(0.5f, 0f);
        uv[lastCap] = new Vector2(0.5f, count * 0.09f);
        for (int edge = 0; edge < SIDES; edge++) {
            int next = edge + 1;
            float angle = (edge + 0.5f) * Mathf.PI * 2f / SIDES;
            List<int> indices = triangles[Mathf.Sin(angle) > 0.5f ? 1 : 0];
            indices.Add(edge); indices.Add(firstCap); indices.Add(next);
            indices.Add((rings - 1) * STRIDE + edge);
            indices.Add((rings - 1) * STRIDE + next);
            indices.Add(lastCap);
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uv;
        Color[] visibility = new Color[vertices.Length];
        for (int i = 0; i < visibility.Length; i++) {
            visibility[i] = Color.white;
        }
        mesh.colors = visibility;
        mesh.subMeshCount = 3;
        for (int i = 0; i < triangles.Length; i++) {
            mesh.SetTriangles(triangles[i], i);
        }
    }
}
