using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Installs the prototype through Unity's prefab and scene APIs.
/// Runs once when the saved SlipTestScene is open, or from the Tools menu.</summary>
[InitializeOnLoad]
public static class SlipperyBarkSetup {
    private const string PrefabPath = "Assets/_Monkobra/Prefabs/Environment/SlipperyBarkSystem.prefab";
    private const string ScenePath = "Assets/_Monkobra/Scenes/SlipTestScene.unity";
    private const string InstalledKey = "Monkobra.SlipperyBark.Installed.v1";

    static SlipperyBarkSetup() {
        EditorApplication.delayCall += AutoInstall;
    }

    public static void InstallBatch() {
        EditorSceneManager.OpenScene(ScenePath);
        Install();
    }

    private static void AutoInstall() {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || SessionState.GetBool(InstalledKey, false)
            || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
            || SceneManager.GetActiveScene().path != ScenePath) {
            return;
        }
        Install();
    }

    [MenuItem("Tools/Monkobra/Install Slippery Bark")]
    public static void Install() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        Scene current = SceneManager.GetActiveScene();
        if (current.path != ScenePath) {
            Debug.LogWarning("Slippery Bark: open SlipTestScene before installing.");
            return;
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/_Monkobra/Materials/Environment/SlipperyBark.mat");
        GameBalanceConfig balance = AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(
            "Assets/_Monkobra/Settings/GameBalanceConfig.asset");
        if (material == null || balance == null) {
            Debug.LogError("Slippery Bark: missing material or balance asset.");
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) {
            GameObject root = new("SlipperyBarkSystem");
            try {
                GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                band.name = "SlipperyTutorialRing";
                Object.DestroyImmediate(band.GetComponent<Collider>());
                band.transform.SetParent(root.transform, false);
                band.transform.localPosition = new Vector3(0f, 8f, 0f);
                band.transform.localScale = new Vector3(10.1f, 0.3f, 10.1f);
                band.GetComponent<Renderer>().sharedMaterial = material;
                SlipperyBarkZone tutorial = band.AddComponent<SlipperyBarkZone>();
                SlipperyBarkSpawner spawner = root.AddComponent<SlipperyBarkSpawner>();
                SerializedObject fields = new(spawner);
                fields.FindProperty("balanceConfig").objectReferenceValue = balance;
                fields.FindProperty("barkMaterial").objectReferenceValue = material;
                fields.FindProperty("tutorialZone").objectReferenceValue = tutorial;
                fields.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally {
                Object.DestroyImmediate(root);
            }
        }
        if (prefab == null) {
            throw new System.InvalidOperationException("Failed to create slippery bark prefab.");
        }

        UpgradeWetSurface(material);

        InstallInScene(current, prefab);
        EditorSceneManager.SaveScene(current);
        SceneManager.SetActiveScene(current);
        SessionState.SetBool(InstalledKey, true);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/SlipperyBarkSetup.log",
            "Installed slippery bark prefab in SlipTestScene only.\n");
        Debug.Log("Slippery Bark ready: tutorial at Y=8, random partial patches above starter zone.");
    }

    // Preserve prefab identity and store a mesh for the Edit-mode preview.
    private static void UpgradeWetSurface(Material material) {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try {
            SlipperyBarkSpawner spawner = root.GetComponent<SlipperyBarkSpawner>();
            SerializedObject fields = new(spawner);
            fields.FindProperty("patchHeightRangeU").vector2Value = new Vector2(1.2f, 4.2f);
            fields.FindProperty("tutorialHeightU").floatValue = 2.6f;
            fields.FindProperty("patchArcRangeDeg").vector2Value = new Vector2(30f, 125f);
            fields.FindProperty("patchSeparationU").floatValue = 4f;
            fields.ApplyModifiedPropertiesWithoutUndo();
            SlipperyBarkZone band = root.GetComponentInChildren<SlipperyBarkZone>();
            band.transform.localScale = Vector3.one;
            band.Initialize(5.05f, 2.6f, 0f, 360f, material, true);
            if (!AssetDatabase.IsValidFolder("Assets/_Monkobra/Meshes"))
                AssetDatabase.CreateFolder("Assets/_Monkobra", "Meshes");
            if (!AssetDatabase.IsValidFolder("Assets/_Monkobra/Meshes/Environment"))
                AssetDatabase.CreateFolder("Assets/_Monkobra/Meshes", "Environment");
            const string meshPath = "Assets/_Monkobra/Meshes/Environment/SlipperyTutorialBand.asset";
            MeshFilter filter = band.GetComponent<MeshFilter>();
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) {
                saved = Object.Instantiate(filter.sharedMesh);
                AssetDatabase.CreateAsset(saved, meshPath);
            }
            else {
                EditorUtility.CopySerialized(filter.sharedMesh, saved);
            }
            filter.sharedMesh = saved;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            material.SetColor("_BaseColor", new Color(0.2f, 0.59f, 0.72f, 1f));
            material.SetFloat("_Smoothness", 0.92f);
            material.SetFloat("_GlossyReflections", 1f);
            EditorUtility.SetDirty(material);
        }
        finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void RenderWetPreview() {
        GameObject root = new("Wet bark preview");
        Material bark = new(Shader.Find("Universal Render Pipeline/Lit"));
        bark.color = new Color(0.19f, 0.22f, 0.25f);
        RenderTexture target = new(960, 720, 24);
        Texture2D pixels = new(960, 720, TextureFormat.RGB24, false);
        try {
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.layer = 31;
            trunk.transform.SetParent(root.transform);
            trunk.transform.localScale = new Vector3(10f, 8f, 10f);
            trunk.GetComponent<Renderer>().sharedMaterial = bark;
            GameObject view = new("Preview camera");
            view.transform.SetParent(root.transform);
            Camera camera = view.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.12f, 0.16f);
            camera.transform.position = new Vector3(0f, 0.7f, -19f);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            GameObject lamp = new("Wet sheen light");
            lamp.transform.SetParent(root.transform);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.cullingMask = 1 << 31;
            lamp.transform.rotation = Quaternion.Euler(35f, 145f, 0f);
            GameObject patch = new("Wet surface");
            patch.layer = 31;
            patch.transform.SetParent(root.transform);
            SlipperyBarkZone zone = patch.AddComponent<SlipperyBarkZone>();
            Material wet = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Monkobra/Materials/Environment/SlipperyBark.mat");
            for (int i = 0; i < 4; i++) {
                float height = i == 2 ? 1.2f : i == 3 ? 4.2f : 2.6f;
                float arc = i == 0 ? 360f : i == 2 ? 30f : i == 3 ? 125f : 65f;
                zone.Initialize(5.05f, height, 180f, arc,
                    wet, true, i == 0 ? 42 : 91);
                camera.targetTexture = target;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {
                        destination = target
                    });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 960f, 720f), 0, 0);
                pixels.Apply();
                string path = i == 0 ? "Temp/SlipperyRing.png"
                    : i == 2 ? "Temp/SlipperyPuddleSmall.png"
                    : i == 3 ? "Temp/SlipperyPuddleLarge.png" : "Temp/SlipperyPuddle.png";
                File.WriteAllBytes(path,
                    pixels.EncodeToPNG());
            }
        }
        finally {
            RenderTexture.active = null;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(bark);
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }

    private static void InstallInScene(Scene scene, GameObject prefab) {
        GameObject env = null;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == "Envs") {
                env = root;
            }
        }
        if (env == null) {
            throw new System.InvalidOperationException("Scene has no Envs root: " + scene.path);
        }
        if (env.GetComponentInChildren<SlipperyBarkSpawner>(true) == null) {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Install slippery bark");
            instance.transform.SetParent(env.transform, false);
        }
        // Replace only the two manually authored prototype objects, keeping
        // all other user scene changes and the material intact.
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == "SlipperyTutorialRing"
                || root.name == "SlipperyTutorialTrigger") {
                Undo.DestroyObjectImmediate(root);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
