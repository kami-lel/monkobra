using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Migrates the existing feature prefab using Unity APIs and
/// preserves GUIDs and the other level systems.</summary>
public static class RoughBarkSetup {
    public const string PrefabPath = "Assets/_Monkobra/Prefabs/Environment/RoughBarkSystem.prefab";
    public const string MaterialPath = "Assets/_Monkobra/Materials/Environment/RoughBark.mat";

    public static void UpgradeBatch() {
        MoveAsset("Assets/_Monkobra/Prefabs/Environment/SlipperyBarkSystem.prefab", PrefabPath);
        MoveAsset("Assets/_Monkobra/Materials/Environment/SlipperyBark.mat", MaterialPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        material.name = "RoughBark";
        BakeBarkTextures(material);
        material.SetColor("_BaseColor", new Color(0.95f, 0.9f, 0.82f, 1f));
        material.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(material);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            // The old unavoidable wet tutorial ring is entirely removed.
            foreach (RoughBarkZone zone in root.GetComponentsInChildren<RoughBarkZone>(true))
                Object.DestroyImmediate(zone.gameObject);
            root.name = "RoughBarkSystem";
            SerializedObject fields = new(root.GetComponent<RoughBarkSpawner>());
            fields.FindProperty("patchHeightRangeU").vector2Value = new Vector2(5f, 8.5f);
            fields.FindProperty("patchArcRangeDeg").vector2Value = new Vector2(110f, 170f);
            fields.FindProperty("patchSeparationU").floatValue = 1f;
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene("Assets/_Monkobra/Scenes/Lv1Scene.unity");
        foreach (GameObject sceneRoot in scene.GetRootGameObjects()) {
            foreach (RoughBarkSpawner system in sceneRoot.GetComponentsInChildren<RoughBarkSpawner>(true)) {
                system.gameObject.name = "RoughBarkSystem";
                PrefabUtility.RecordPrefabInstancePropertyModifications(system.gameObject);
            }
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Rough bark ready: no starting ring; score threshold 3000; larger avoidable areas.");
    }

    private static void MoveAsset(string from, string to) {
        if (AssetDatabase.LoadMainAssetAtPath(to) != null) return;
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new System.Exception(error);
    }

    // Small tileable bark plates and fissures. Authored locally for this
    // prototype; the normal map adds relief without a thick floating shell.
    private static void BakeBarkTextures(Material material) {
        const string paintedPath = "Assets/_Monkobra/Textures/Environment/RoughBarkPainted.png";
        if (File.Exists(paintedPath)) {
            string reliefPath = "Assets/_Monkobra/Textures/Environment/RoughBarkPaintedNormal.png";
            File.Copy(paintedPath, reliefPath, true);
            AssetDatabase.ImportAsset(paintedPath);
            AssetDatabase.ImportAsset(reliefPath);
            TextureImporter colorImporter = (TextureImporter)AssetImporter.GetAtPath(paintedPath);
            colorImporter.wrapMode = TextureWrapMode.Repeat;
            colorImporter.maxTextureSize = 1024;
            colorImporter.SaveAndReimport();
            TextureImporter reliefImporter = (TextureImporter)AssetImporter.GetAtPath(reliefPath);
            reliefImporter.textureType = TextureImporterType.NormalMap;
            reliefImporter.convertToNormalmap = true;
            reliefImporter.heightmapScale = 0.08f;
            reliefImporter.sRGBTexture = false;
            reliefImporter.wrapMode = TextureWrapMode.Repeat;
            reliefImporter.maxTextureSize = 1024;
            reliefImporter.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(paintedPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(reliefPath));
            material.SetFloat("_BumpScale", 1.2f);
            material.EnableKeyword("_NORMALMAP");
            return;
        }
        const int size = 512;
        const string folder = "Assets/_Monkobra/Textures/Environment";
        if (!AssetDatabase.IsValidFolder("Assets/_Monkobra/Textures"))
            AssetDatabase.CreateFolder("Assets/_Monkobra", "Textures");
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/_Monkobra/Textures", "Environment");
        float[] height = new float[size * size];
        Color[] colors = new Color[height.Length];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float u = x / (float)size, v = y / (float)size;
                float warped = u + 0.018f * Mathf.Sin(v * Mathf.PI * 4f)
                    + 0.012f * Mathf.Sin(v * Mathf.PI * 10f + u * Mathf.PI * 4f);
                float gx = warped * 8f, gy = v * 4f;
                int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
                float first = float.PositiveInfinity, second = float.PositiveInfinity;
                for (int j = -1; j <= 1; j++) {
                    for (int i = -1; i <= 1; i++) {
                        int px = cx + i, py = cy + j;
                        int hx = (px % 8 + 8) % 8, hy = (py % 4 + 4) % 4;
                        float rx = Mathf.Repeat(Mathf.Sin(hx * 127.1f + hy * 311.7f) * 43758.5453f, 1f);
                        float ry = Mathf.Repeat(Mathf.Sin(hx * 269.5f + hy * 183.3f) * 43758.5453f, 1f);
                        float dx = gx - px - 0.15f - rx * 0.7f;
                        float dy = (gy - py - 0.15f - ry * 0.7f) * 1.6f;
                        float distance = dx * dx + dy * dy;
                        if (distance < first) { second = first; first = distance; }
                        else if (distance < second) second = distance;
                    }
                }
                float plate = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((second - first) * 5f));
                float grain = 0.5f + 0.5f * Mathf.Sin(warped * Mathf.PI * 96f
                    + 0.6f * Mathf.Sin(v * Mathf.PI * 12f));
                height[y * size + x] = plate * 0.8f + grain * 0.12f;
                Color raised = Color.Lerp(new Color(0.23f, 0.16f, 0.10f),
                    new Color(0.46f, 0.34f, 0.21f), 0.45f + grain * 0.3f);
                colors[y * size + x] = Color.Lerp(new Color(0.075f, 0.05f, 0.03f),
                    raised, plate);
            }
        }
        Texture2D albedo = new(size, size, TextureFormat.RGB24, false);
        Texture2D normal = new(size, size, TextureFormat.RGB24, false, true);
        try {
            albedo.SetPixels(colors); albedo.Apply();
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = height[y * size + (x + 1) % size]
                        - height[y * size + (x + size - 1) % size];
                    float dy = height[((y + 1) % size) * size + x]
                        - height[((y + size - 1) % size) * size + x];
                    Vector3 n = new Vector3(-dx * 3f, -dy * 3f, 1f).normalized;
                    colors[y * size + x] = new Color(n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                }
            }
            normal.SetPixels(colors); normal.Apply();
            string albedoPath = folder + "/RoughBarkAlbedo.png";
            string normalPath = folder + "/RoughBarkNormal.png";
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            AssetDatabase.ImportAsset(albedoPath);
            AssetDatabase.ImportAsset(normalPath);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", 1.15f);
            material.EnableKeyword("_NORMALMAP");
        }
        finally { Object.DestroyImmediate(albedo); Object.DestroyImmediate(normal); }
    }

    public static void RenderPreview() {
        GameObject root = new("Rough bark preview");
        Material trunkMaterial = new(Shader.Find("Universal Render Pipeline/Lit"));
        trunkMaterial.color = new Color(0.22f, 0.25f, 0.27f);
        RenderTexture target = new(960, 720, 24);
        Texture2D pixels = new(960, 720, TextureFormat.RGB24, false);
        try {
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.layer = 31; trunk.transform.SetParent(root.transform);
            trunk.transform.localScale = new Vector3(10f, 8f, 10f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;
            GameObject view = new("Preview camera"); view.transform.SetParent(root.transform);
            Camera camera = view.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.12f, 0.16f);
            camera.transform.position = new Vector3(0f, 0.8f, -19f);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = 6f;
            GameObject lamp = new("Light"); lamp.transform.SetParent(root.transform);
            Light light = lamp.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.7f; light.cullingMask = 1 << 31;
            lamp.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            GameObject patch = new("Rough surface"); patch.layer = 31; patch.transform.SetParent(root.transform);
            patch.AddComponent<RoughBarkZone>().Initialize(5.05f, 6.75f, 180f, 150f,
                AssetDatabase.LoadAssetAtPath<Material>(MaterialPath), true, 91);
            camera.targetTexture = target;
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); pixels.Apply();
            Directory.CreateDirectory("Temp");
            File.WriteAllBytes("Temp/RoughBarkPreview.png", pixels.EncodeToPNG());
            string previewPath = System.Environment.GetEnvironmentVariable("MONKOBRA_BARK_PREVIEW");
            if (!string.IsNullOrEmpty(previewPath)) File.WriteAllBytes(previewPath, pixels.EncodeToPNG());
        }
        finally {
            RenderTexture.active = null; Object.DestroyImmediate(root);
            Object.DestroyImmediate(trunkMaterial); target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
        }
    }
}
