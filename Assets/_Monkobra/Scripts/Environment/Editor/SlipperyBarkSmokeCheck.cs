using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Runs a scoped play-mode smoke check in a batch Editor, using
/// a virtual keyboard and the real monkey/scene. Never saves runtime edits.</summary>
[InitializeOnLoad]
public static class SlipperyBarkSmokeCheck {
    private const string RunningKey = "Monkobra.SlipperySmoke.Running";
    private const string QuittingKey = "Monkobra.SlipperySmoke.Quitting";
    private static MonkeyController monkey;
    private static Rigidbody body;
    private static Keyboard keyboard;
    private static int stage;
    private static double deadline;
    private static float peakY;
    private static float minY;
    private static bool sawSlip;
    private static int slipCount;
    private static float slideStartTime;
    private static Vector3 slideStartPosition;
    private static float lateralTravel;
    private static float approachY;
    private static bool climbedBeforeSkid;
    private static string runtimeError;
    private static float downhillSkidStartY;
    private static float downhillSkidEndY;
    private static bool sawDownhillSkid;
    private static bool finishedDownhillSkid;
    private static float downwardSpeedAtEntry;

    static SlipperyBarkSmokeCheck() {
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    public static void RunBatch() {
        EditorSceneManager.OpenScene("Assets/_Monkobra/Scenes/SlipTestScene.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    public static void RunLevelOneBatch() {
        EditorSceneManager.OpenScene("Assets/_Monkobra/Scenes/Lv1Scene.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.EnterPlaymode();
    }

    public static void IntegrateAndCheckLevelOneBatch() {
        SlipperyBarkSetup.InstallLevelOneBatch();
        RunLevelOneBatch();
    }

    public static void RunVisualBatch() {
        SlipperyBarkSetup.InstallBatch();
        SlipperyBarkSetup.RenderWetPreview();
        RunBatch();
    }

    private static void OnPlayState(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredEditMode
            && SessionState.GetBool(QuittingKey, false)) {
            SessionState.SetBool(QuittingKey, false);
            int result = SessionState.GetInt(QuittingKey + ".Code", 1);
            EditorApplication.delayCall += () => EditorApplication.Exit(result);
            return;
        }
        if (!SessionState.GetBool(RunningKey, false)
            || state != PlayModeStateChange.EnteredPlayMode) {
            return;
        }
        deadline = EditorApplication.timeSinceStartup + 20;
        stage = 0;
        EditorApplication.update += Tick;
    }

    private static void Tick() {
        try {
            if (EditorApplication.timeSinceStartup > deadline) {
                throw new Exception("Timed out at smoke stage " + stage
                    + "; y=" + (body != null ? body.position.y : 0f)
                    + "; time=" + Time.time + "; state=" + GameController.I?.State
                    + "; up=" + monkey?.IsClimbingUp + "; sawSlip=" + sawSlip);
            }
            Require(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (stage == 0) {
                monkey = UnityEngine.Object.FindFirstObjectByType<MonkeyController>();
                if (monkey == null || GameController.I == null) {
                    return;
                }
                body = monkey.GetComponent<Rigidbody>();
                Application.logMessageReceived += (message, stack, type) => {
                    if (type == LogType.Exception || type == LogType.Error)
                        runtimeError = message;
                };
                // Isolate slippery bark from unrelated collision penalties.
                foreach (BranchController branch in UnityEngine.Object.FindObjectsByType<BranchController>(FindObjectsSortMode.None)) {
                    branch.gameObject.SetActive(false);
                }
                InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.EnableDevice(keyboard);
                Application.runInBackground = true;
                MoveBodyToY(5.6f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                UpdateGameInput();
                peakY = minY = body.position.y;
                monkey.SlipStarted += () => {
                    peakY = minY = body.position.y;
                    sawSlip = true;
                    slipCount++;
                    slideStartTime = Time.time;
                    slideStartPosition = body.position;
                    approachY = body.position.y;
                };
                monkey.SlipSlideStarted += () => {
                    if (slipCount == 1) {
                        peakY = minY = body.position.y;
                        climbedBeforeSkid = peakY > approachY + 0.03f;
                    }
                    else if (slipCount == 2) {
                        downhillSkidStartY = body.position.y;
                        sawDownhillSkid = true;
                        downwardSpeedAtEntry = (float)typeof(MonkeyController)
                            .GetField("climbSpeedU", BindingFlags.Instance | BindingFlags.NonPublic)
                            .GetValue(monkey);
                    }
                };
                stage = 1;
                return;
            }
            if (stage == 1) {
                InputSystem.QueueStateEvent(keyboard, monkey.IsSlipping
                    ? new KeyboardState(Key.W, Key.D) : new KeyboardState(Key.W));
                UpdateGameInput();
                if (monkey.IsSlipping) {
                    sawSlip = true;
                    minY = Mathf.Min(minY, body.position.y);
                    Vector3 side = body.position - slideStartPosition;
                    side.y = 0f;
                    lateralTravel = Mathf.Max(lateralTravel, side.magnitude);
                }
                if (!sawSlip || monkey.IsSlipping) {
                    return;
                }
                Require(peakY - minY > 0.18f && peakY - minY < 0.4f,
                    "Wet grip must briefly slide about 0.3 units; measured "
                        + (peakY - minY) + " (" + peakY + " to " + minY + ").");
                Require(climbedBeforeSkid,
                    "Wet grip must continue climbing before the brief skid.");
                Require(Time.time - slideStartTime >= 0.38f
                    && Time.time - slideStartTime < 0.65f,
                    "Approach, short skid and regrip must form one continuous motion.");
                Require(body.position.y > minY + 0.03f,
                    "Held climb must already resume before regrip ends.");
                Require(lateralTravel > 0.15f, "Circling must remain available during a slide.");
                Require(GameController.I.State == GameState.StarterZone,
                    "Tutorial must remain inside the protected starter zone.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                UpdateGameInput();
                stage = 2;
                return;
            }
            if (stage == 2) {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                UpdateGameInput();
                Require(slipCount == 1, "One traversal must remain passable without a slip loop.");
                if (body.position.y < 11f) return;
                MoveBodyToY(12f);
                stage = 3;
                return;
            }
            if (stage == 3) {
                // Leave the enlarged contact region, then genuinely re-enter
                // from above. Wet bark must not be consumed by its first use.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                UpdateGameInput();
                if (slipCount < 2) return;
                stage = 4;
                return;
            }
            if (monkey.IsSlipping) {
                if (sawDownhillSkid && !monkey.IsWetGripSliding && !finishedDownhillSkid) {
                    downhillSkidEndY = body.position.y;
                    finishedDownhillSkid = true;
                }
                return;
            }
            Require(sawDownhillSkid && finishedDownhillSkid,
                "Returning downward through the same water must visibly skid.");
            GameBalanceConfig liveBalance = (GameBalanceConfig)typeof(MonkeyController)
                .GetField("balanceConfig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monkey);
            float normalDescent = -downwardSpeedAtEntry * liveBalance.SlipSlideDurationS;
            Require(downhillSkidStartY - downhillSkidEndY > normalDescent + 0.15f,
                "Downhill skid must add travel to the regular descent, not replace it with a slow drop.");
            CheckRandomPatches();
            CheckFallingCobraDisappears();
            Require(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            Finish(0, "PASS: Lv1 slippery bark checks; falling cobra disappears after one hit, with one heavy-hit event for either callback order and body contact.");
        }
        catch (Exception error) {
            Finish(1, error.ToString());
        }
    }

    private static void CheckRandomPatches() {
        SlipperyBarkSpawner spawner = UnityEngine.Object.FindFirstObjectByType<SlipperyBarkSpawner>();
        Require(spawner != null, "Spawner must be wired in the scene.");
        FieldInfo configField = typeof(SlipperyBarkSpawner).GetField("balanceConfig", BindingFlags.Instance | BindingFlags.NonPublic);
        GameBalanceConfig config = (GameBalanceConfig)configField.GetValue(spawner);
        Require(config.SlipperySpawnProbabilityCurve.Evaluate(1f)
            > config.SlipperySpawnProbabilityCurve.Evaluate(0f), "Higher difficulty must raise slippery probability.");
        GameBalanceConfig testConfig = UnityEngine.Object.Instantiate(config);
        typeof(GameBalanceConfig).GetField("slipperySpawnProbabilityCurve", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(testConfig, AnimationCurve.Constant(0f, 1f, 1f));
        configField.SetValue(spawner, testConfig);
        TreeSegmentController highest = null;
        foreach (TreeSegmentController segment in UnityEngine.Object.FindObjectsByType<TreeSegmentController>(FindObjectsSortMode.None)) {
            if (highest == null || segment.transform.position.y > highest.transform.position.y) {
                highest = segment;
            }
        }
        Require(highest != null, "Tree pool must have live segments.");
        typeof(GameBalanceConfig).GetField("branchGenerationAttemptCount", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(testConfig, 0);
        typeof(TreeSegmentController).GetField("balanceConfig", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(highest, testConfig);
        foreach (SpiderWebSpawner webs in UnityEngine.Object.FindObjectsByType<SpiderWebSpawner>(FindObjectsSortMode.None)) {
            webs.enabled = false;
        }
        foreach (FallingCobraSpawner cobras in UnityEngine.Object.FindObjectsByType<FallingCobraSpawner>(FindObjectsSortMode.None)) {
            cobras.enabled = false;
        }
        highest.Restart(new Vector3(0f, 150f, 0f));
        foreach (BranchController branch in highest.GetComponentsInChildren<BranchController>()) {
            branch.gameObject.SetActive(false);
        }
        foreach (SpiderWebTrap web in highest.GetComponentsInChildren<SpiderWebTrap>()) {
            web.gameObject.SetActive(false);
        }
        foreach (FallingCobraController cobra in highest.GetComponentsInChildren<FallingCobraController>()) {
            cobra.gameObject.SetActive(false);
        }
        // A fresh clear segment with guaranteed rolls tests the actual event
        // and recycling path, without depending on a lucky random seed.
        highest.Restart(new Vector3(0f, 170f, 0f));
        SlipperyBarkZone[] patches = highest.GetComponentsInChildren<SlipperyBarkZone>();
        Require(patches.Length > 0, "Guaranteed high roll must create a patch.");
        foreach (SlipperyBarkZone patch in patches) {
            Mesh mesh = patch.GetComponent<MeshFilter>().sharedMesh;
            Require(mesh != null && mesh.vertexCount >= 6,
                "Partial patch must carry a generated mesh.");
            float arc = (float)typeof(SlipperyBarkZone).GetField("arcDeg", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(patch);
            Require(arc < 180f, "High patches must leave a dry route.");
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            // The very first triangle tapers to a point; use an interior one.
            Vector3 normal = Vector3.Cross(vertices[triangles[7]] - vertices[triangles[6]],
                vertices[triangles[8]] - vertices[triangles[6]]);
            Vector3 radial = vertices[triangles[6]];
            radial.y = 0f;
            Require(Vector3.Dot(normal, radial) > 0f, "Bark mesh must face outward.");
            Require(Mathf.Abs(vertices[0].y - vertices[vertices.Length / 2].y) > 0.1f,
                "Puddle edge must be irregular rather than a straight stripe.");
            int middle = (vertices.Length / 4) * 2;
            Vector3 wet = patch.transform.position + (vertices[middle] + vertices[middle + 1]) * 0.5f;
            Require(patch.Contains(wet), "Visible puddle interior must count as wet.");
            Vector3 dry = wet + Vector3.up * 5f;
            Require(!patch.Contains(dry), "Dry space beyond the outline must not slip.");
        }
        SlipperyBarkZone tutorial = spawner.GetComponentInChildren<SlipperyBarkZone>();
        MaterialPropertyBlock block = new();
        tutorial.GetComponent<Renderer>().GetPropertyBlock(block);
        Require(block.isEmpty, "Repeated contact must not fade or consume the wet material.");
        UnityEngine.Random.State originalRandom = UnityEngine.Random.state;
        UnityEngine.Random.InitState(528);
        float minHeight = float.PositiveInfinity, maxHeight = 0f;
        float minArc = float.PositiveInfinity, maxArc = 0f;
        for (int i = 0; i < 12; i++) {
            highest.Restart(new Vector3(0f, 190f + i * 12f, 0f));
            foreach (SlipperyBarkZone patch in highest.GetComponentsInChildren<SlipperyBarkZone>()) {
                minHeight = Mathf.Min(minHeight, patch.HeightU);
                maxHeight = Mathf.Max(maxHeight, patch.HeightU);
                float arc = (float)typeof(SlipperyBarkZone).GetField("arcDeg",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(patch);
                minArc = Mathf.Min(minArc, arc);
                maxArc = Mathf.Max(maxArc, arc);
                Require(arc <= 150f, "Even a large puddle must leave a dry route.");
                Require(patch.HeightU >= 1.2f && patch.HeightU <= 4.2f,
                    "Puddle size must stay inside its authored range.");
            }
        }
        UnityEngine.Random.state = originalRandom;
        Require(maxHeight - minHeight > 1.5f && maxArc - minArc > 40f,
            "Generation must produce genuinely different large and small puddles.");
        configField.SetValue(spawner, config);
        UnityEngine.Object.Destroy(testConfig);
    }

    private static void CheckFallingCobraDisappears() {
        HitBranchSensor sensor = monkey.GetComponentInChildren<HitBranchSensor>(true);
        Require(sensor != null, "Monkey must have a hit sensor.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Monkobra/Prefabs/Mob/FallingCobra.prefab");
        Require(prefab != null, "Falling cobra prefab must exist.");
        for (int order = 0; order < 3; order++) {
            GameObject instance = UnityEngine.Object.Instantiate(prefab,
                new Vector3(100f, 100f, 100f), Quaternion.identity);
            FallingCobraController cobra = instance.GetComponent<FallingCobraController>();
            CapsuleCollider capsule = instance.GetComponent<CapsuleCollider>();
            int hits = 0;
            bool heavyHit = true;
            Action<Collider, HitBranchSensor.HitKind> onHit = (source, kind) => {
                if (source == capsule) {
                    heavyHit &= kind == HitBranchSensor.HitKind.FallingCobraController;
                    hits++;
                }
            };
            sensor.Hit += onHit;
            try {
                typeof(FallingCobraController).GetField("playerSensor",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cobra, sensor);
                typeof(FallingCobraController).GetMethod("StartFalling",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cobra, null);
                Collider contact = order == 2 ? monkey.GetComponent<Collider>()
                    : sensor.GetComponent<Collider>();
                Require(contact != null, "Test contact must exist.");
                if (order == 0) sensor.SendMessage("OnTriggerEnter", capsule);
                else cobra.SendMessage("OnTriggerEnter", contact);
                Require(!instance.activeSelf && !capsule.enabled && hits == 1 && heavyHit,
                    "First contact must report one hit then immediately hide the snake.");
                typeof(FallingCobraController).GetMethod("OnTriggerEnter",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cobra, new object[] { contact });
                typeof(FallingCobraController).GetMethod("OnTriggerStay",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cobra, new object[] { contact });
                sensor.SendMessage("OnTriggerEnter", capsule);
                Vector3 stopped = instance.GetComponent<Rigidbody>().position;
                typeof(FallingCobraController).GetMethod("FixedUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cobra, null);
                Require(hits == 1 && heavyHit && instance.GetComponent<Rigidbody>().position == stopped,
                    "Hidden snake must neither continue falling nor hit again.");
            }
            finally {
                sensor.Hit -= onHit;
                UnityEngine.Object.Destroy(instance);
            }
        }
    }

    private static void MoveBodyToY(float y) {
        Vector3 point = body.position;
        point.y = y;
        body.position = point;
        monkey.transform.position = point;
        Physics.SyncTransforms();
    }

    // The public parameterless update selects Editor state inside an
    // EditorApplication callback. Explicit Dynamic updates exercise game
    // state; the installed Input System exposes this overload internally.
    private static void UpdateGameInput() {
        typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null)
            .Invoke(null, new object[] { InputUpdateType.Dynamic });
    }

    private static void Require(bool condition, string message) {
        if (!condition) {
            throw new Exception(message);
        }
    }

    private static void Finish(int exitCode, string message) {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RunningKey, false);
        File.WriteAllText("Temp/SlipperyBarkSmoke.txt", message);
        Debug.Log("Slippery bark smoke: " + message);
        if (keyboard != null) {
            InputSystem.RemoveDevice(keyboard);
        }
        // Leave Play mode first so Unity can discard runtime scene edits
        // and close cleanly without creating a scene-recovery prompt.
        SessionState.SetInt(QuittingKey + ".Code", exitCode);
        SessionState.SetBool(QuittingKey, true);
        EditorApplication.ExitPlaymode();
    }
}
