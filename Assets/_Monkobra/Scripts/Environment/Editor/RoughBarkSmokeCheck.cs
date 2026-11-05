using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Play-mode regression checks for score gating, continuous
/// slowdown, recovery and an avoidable rough surface. Runtime edits are discarded.</summary>
[InitializeOnLoad]
public static class RoughBarkSmokeCheck {
    private const string RunKey = "Monkobra.RoughBark.Check";
    private const string QuitKey = "Monkobra.RoughBark.Quit";
    private static MonkeyController monkey;
    private static Rigidbody body;
    private static Keyboard keyboard;
    private static RoughBarkZone testZone;
    private static int stage;
    private static double deadline;
    private static float phaseTime, lastY, previousScale, phaseAngle;
    private static string runtimeError;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static RoughBarkSmokeCheck() { EditorApplication.playModeStateChanged += OnPlayState; }

    public static void UpgradeAndRunBatch() {
        RoughBarkSetup.UpgradeBatch();
        RoughBarkSetup.RenderPreview();
        RunBatch();
    }

    public static void RunBatch() {
        EditorSceneManager.OpenScene("Assets/_Monkobra/Scenes/Lv1Scene.unity");
        SessionState.SetBool(RunKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayState(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(QuitKey, false)) {
            SessionState.SetBool(QuitKey, false);
            int result = SessionState.GetInt(QuitKey + "Code", 1);
            EditorApplication.delayCall += () => EditorApplication.Exit(result);
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunKey, false)) return;
        deadline = EditorApplication.timeSinceStartup + 25;
        EditorApplication.update += Tick;
    }

    private static void Tick() {
        try {
            Require(EditorApplication.timeSinceStartup < deadline, "Timed out at stage " + stage);
            Require(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            if (stage == 0) {
                monkey = UnityEngine.Object.FindFirstObjectByType<MonkeyController>();
                if (monkey == null || ScoreManager.I == null || GameController.I == null) return;
                Application.logMessageReceived += OnLog;
                body = monkey.GetComponent<Rigidbody>();
                InputSystem.settings = UnityEngine.Object.Instantiate(InputSystem.settings);
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.EnableDevice(keyboard); Application.runInBackground = true;
                Press(Key.Enter);
                Require(UnityEngine.Object.FindObjectsByType<RoughBarkZone>(FindObjectsSortMode.None).Length == 0,
                    "There must be no starting ring or early rough area.");
                SetScore(2999);
                Require(UnityEngine.Object.FindObjectsByType<RoughBarkZone>(FindObjectsSortMode.None).Length == 0,
                    "Rough bark must not spawn below 3000 total score.");
                // Remove unrelated hazards only from this temporary test run.
                foreach (BranchController branch in UnityEngine.Object.FindObjectsByType<BranchController>(FindObjectsSortMode.None)) branch.gameObject.SetActive(false);
                foreach (SpiderWebTrap web in UnityEngine.Object.FindObjectsByType<SpiderWebTrap>(FindObjectsSortMode.None)) web.gameObject.SetActive(false);
                foreach (FallingCobraController cobra in UnityEngine.Object.FindObjectsByType<FallingCobraController>(FindObjectsSortMode.None)) cobra.gameObject.SetActive(false);
                SetScore(3000);
                RoughBarkZone[] patches = UnityEngine.Object.FindObjectsByType<RoughBarkZone>(FindObjectsSortMode.None);
                Require(patches.Length > 0, "Crossing 3000 must decorate the existing forward pool.");
                foreach (RoughBarkZone patch in patches) {
                    Require(patch.transform.position.y - RoughBarkZone.GetVerticalExtent(patch.HeightU)
                        > body.position.y + 9f, "Unlocked areas must appear ahead, not under the player.");
                    float angle = (float)typeof(RoughBarkZone).GetField("centerAngleDeg", Private).GetValue(patch);
                    float arc = (float)typeof(RoughBarkZone).GetField("arcDeg", Private).GetValue(patch);
                    Require(arc >= 110f && arc <= 170f
                        && patch.HeightU >= 5f && patch.HeightU <= 8.5f,
                        "Larger rough areas must preserve at least 190 dry degrees.");
                    TreeSegmentController owner = patch.GetComponentInParent<TreeSegmentController>();
                    Require(owner != null && patch.transform.position.y
                        + RoughBarkZone.GetVerticalExtent(patch.HeightU)
                        <= owner.transform.position.y + owner.GetHeight() * 0.5f + 0.01f,
                        "A tall patch must remain owned until the player clears its upper end.");
                    Vector3 opposite = patch.transform.position + Quaternion.Euler(0f, angle + 180f, 0f) * Vector3.forward * 5.88f;
                    Require(!patch.Contains(opposite), "Every rough patch must have a dry way around.");
                    Mesh mesh = patch.GetComponent<MeshFilter>().sharedMesh;
                    float minRadius = float.PositiveInfinity, maxRadius = 0f;
                    foreach (Vector3 vertex in mesh.vertices) {
                        float radius = new Vector2(vertex.x, vertex.z).magnitude;
                        minRadius = Mathf.Min(minRadius, radius); maxRadius = Mathf.Max(maxRadius, radius);
                    }
                    Require(maxRadius - minRadius > 0.04f, "Rough bark must visibly protrude, not resemble flat water.");
                    Require(maxRadius < 5.06f, "Rough surface must hug the trunk instead of floating outside it.");
                    Material bark = patch.GetComponent<Renderer>().sharedMaterial;
                    Require(bark.GetTexture("_BaseMap") != null && bark.GetTexture("_BumpMap") != null,
                        "Rough bark must include fissure color and normal-map relief.");
                }
                StaminaBarController.I.AddStamina(100f);
                MoveToY(5.6f);
                float facing = Mathf.Atan2(body.position.x, body.position.z) * Mathf.Rad2Deg;
                GameObject test = new("Rough contact check");
                test.transform.position = Vector3.up * 6.6f;
                testZone = test.AddComponent<RoughBarkZone>();
                testZone.Initialize(5.05f, 5.5f, facing, 150f,
                    AssetDatabase.LoadAssetAtPath<Material>(RoughBarkSetup.MaterialPath), true);
                phaseTime = Time.time; lastY = body.position.y; previousScale = 1f;
                Press(Key.W); stage = 1; return;
            }
            if (stage == 1) {
                Press(Key.W);
                Require(body.position.y >= lastY - 0.001f, "Uphill rough movement must never skid backward.");
                Require(monkey.RoughBarkSpeedScale <= previousScale + 0.001f,
                    "Contact should smoothly approach a steady speed.");
                lastY = body.position.y; previousScale = monkey.RoughBarkSpeedScale;
                if (Time.time - phaseTime < 0.4f) return;
                Require(Mathf.Abs(monkey.RoughBarkSpeedScale - 0.35f) < 0.015f,
                    "Contact must settle at 35 percent vertical speed.");
                testZone.gameObject.SetActive(false);
                phaseTime = Time.time; stage = 2; return;
            }
            if (stage == 2) {
                Press(Key.W);
                Require(body.position.y >= lastY - 0.001f, "Leaving rough bark must not move the player backward.");
                lastY = body.position.y;
                if (Time.time - phaseTime < 0.4f) return;
                Require(Mathf.Abs(monkey.RoughBarkSpeedScale - 1f) < 0.01f,
                    "Leaving or recycling a zone must restore normal movement.");
                MoveToY(7f); lastY = body.position.y;
                testZone.gameObject.SetActive(true);
                phaseTime = Time.time; Press(Key.S); stage = 3; return;
            }
            if (stage == 3) {
                Press(Key.S);
                Require(body.position.y <= lastY + 0.001f, "Downward input must remain responsive on rough bark.");
                lastY = body.position.y;
                if (Time.time - phaseTime < 0.4f) return;
                Require(Mathf.Abs(monkey.RoughBarkSpeedScale - 0.35f) < 0.015f,
                    "Downward contact must slow smoothly too.");
                phaseAngle = body.rotation.eulerAngles.y; phaseTime = Time.time;
                Press(Key.D); stage = 4; return;
            }
            if (stage == 4 || stage == 5) {
                Press(stage == 4 ? Key.D : Key.A);
                float elapsed = Time.time - phaseTime;
                if (elapsed < 0.4f) return;
                var config = (MonkeyConfig)typeof(MonkeyController).GetField("config", Private).GetValue(monkey);
                float turn = Mathf.DeltaAngle(phaseAngle, body.rotation.eulerAngles.y);
                float ratio = Mathf.Abs(turn) / (config.RotationSpeedDeg * elapsed);
                Require(ratio > 0.27f && ratio < 0.43f,
                    "Both sideways directions must move at 35 percent speed; actual ratio " + ratio);
                Require(stage == 4 ? turn < 0f : turn > 0f, "Sideways controls must retain their direction.");
                phaseAngle = body.rotation.eulerAngles.y; phaseTime = Time.time;
                if (stage == 4) { stage = 5; Press(Key.A); return; }
                testZone.gameObject.SetActive(false); stage = 6; Press(Key.A); return;
            }
            Press(Key.A);
            if (Time.time - phaseTime < 0.4f) return;
            Require(Mathf.Abs(monkey.RoughBarkSpeedScale - 1f) < 0.01f,
                "Leaving must restore horizontal and vertical speed together.");
            Finish(0, "PASS: no early ring; 2999 points has no rough bark; 3000 unlocks forward patches; large raised textured meshes leave a dry route; smooth 35% up/down/left/right speed, no backward skid and full recovery.");
        }
        catch (Exception error) { Finish(1, error.ToString()); }
    }

    private static void SetScore(long value) {
        typeof(ScoreManager).GetField("<RewardScore>k__BackingField", Private).SetValue(ScoreManager.I, value);
        var changed = (Action<long>)typeof(ScoreManager).GetField("ScoreChanged", Private).GetValue(ScoreManager.I);
        changed?.Invoke(ScoreManager.I.TotalScore);
    }

    private static void Press(params Key[] keys) {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { InputUpdateType.Dynamic });
    }

    private static void MoveToY(float y) {
        Vector3 point = body.position; point.y = y;
        body.position = point; monkey.transform.position = point; Physics.SyncTransforms();
    }

    private static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception) runtimeError = message;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static void Finish(int code, string message) {
        EditorApplication.update -= Tick; SessionState.SetBool(RunKey, false);
        Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/RoughBarkSmoke.txt", message);
        Debug.Log("Rough bark smoke: " + message);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        SessionState.SetBool(QuitKey, true); SessionState.SetInt(QuitKey + "Code", code);
        EditorApplication.ExitPlaymode();
    }
}
