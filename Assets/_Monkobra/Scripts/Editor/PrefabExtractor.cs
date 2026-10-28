using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot Editor tool that turns the inline objects of the open scene into
/// nested prefabs, one menu item per group. Every leaf is saved as its own
/// prefab first, then the group root, so the leaves nest inside the root.
/// <para>
/// A scene reference that points out of a group root would be dropped by Unity
/// when the root is saved. The tool records each one before the save and
/// writes it back afterwards as an override on the scene instance. The active
/// state of every object is never changed.
/// </para>
/// Retired in the last Movement of the prefab split plan.
/// </summary>
public static class PrefabExtractor {
    // Menu Items  #############################################################
    [MenuItem("Monkobra/Extract/1 World")]
    public static void ExtractWorld() {
        const string dir = PREFAB_ROOT + "World/";
        GameObject root = CreateGroup("World");
        Adopt(root, "Directional Light", "DirectionalLight", dir);
        Adopt(root, "Global Volume", "GlobalVolume", dir);
        Adopt(root, "EventSystem", "EventSystem", dir);
        SaveGroup(root, dir + "World.prefab");
    }

    [MenuItem("Monkobra/Extract/2 Environment")]
    public static void ExtractEnvironment() {
        const string dir = PREFAB_ROOT + "Environment/";
        GameObject root = FindByPath("Envs");
        if (root == null) {
            Debug.LogError("PrefabExtractor:\tfail to find GO: Envs");
            return;
        }
        SaveLeaf(FindByPath("Envs/DTSPool"), dir + "DTSPool.prefab");
        SaveLeaf(FindByPath("Envs/StarterZoneExitTrigger"),
                 dir + "StarterZoneExitTrigger.prefab");
        SaveGroup(root, dir + "Envs.prefab");
    }

    [MenuItem("Monkobra/Extract/3 Entities")]
    public static void ExtractEntities() {
        GameObject root = FindByPath("Entities");
        if (root == null) {
            Debug.LogError("PrefabExtractor:\tfail to find GO: Entities");
            return;
        }
        SaveGroup(root, PREFAB_ROOT + "Player/Entities.prefab");
    }

    [MenuItem("Monkobra/Extract/4 Systems (without Analytics)")]
    public static void ExtractSystems() {
        const string dir = PREFAB_ROOT + "Systems/";
        GameObject controller = FindByPath("GameController");
        if (controller == null) {
            Debug.LogError("PrefabExtractor:\tfail to find GO: GameController");
            return;
        }
        GameObject root = CreateGroup("Systems");
        SplitToChild<ScreensManager>(controller, root, dir);
        SplitToChild<ScoreManager>(controller, root, dir);
        SplitToChild<RampedDifficultyManager>(controller, root, dir);
        SaveLeaf(controller, dir + "GameController.prefab");
        controller.transform.SetParent(root.transform, true);
        SaveGroup(root, dir + "Systems.prefab");
    }

    [MenuItem("Monkobra/Extract/4b Merge AnalyticsManager Into GameController")]
    public static void MergeAnalyticsIntoController() {
        GameObject source = FindByPath("AnalyticsManager");
        GameObject target = FindByPath("Systems/GameController");
        if (source == null || target == null) {
            Debug.LogError(
                "PrefabExtractor:\tfail to find GO: AnalyticsManager or "
                + "Systems/GameController");
            return;
        }
        MoveComponent<AnalyticsManager>(source, target);
        if (source.GetComponents<Component>().Length == 1) {
            UnityEngine.Object.DestroyImmediate(source);
        }
        PrefabUtility.ApplyPrefabInstance(
            target, InteractionMode.AutomatedAction);
    }

    [MenuItem("Monkobra/Extract/5 UI")]
    public static void ExtractUi() {
        const string dir = PREFAB_ROOT + "UI/";
        GameObject goal = FindByPath("GoalText");
        if (goal != null) {
            UnityEngine.Object.DestroyImmediate(goal);
            Log("deleted orphan GO: GoalText");
        }
        GameObject root = FindByPath("Canvas");
        if (root == null) {
            Debug.LogError("PrefabExtractor:\tfail to find GO: Canvas");
            return;
        }
        SaveLeaf(FindByPath("Canvas/ScoreHeight/HeightDisplay"),
                 dir + "HeightDisplay.prefab");
        SaveLeaf(FindByPath("Canvas/ScoreHeight/ScoreHUD"),
                 dir + "ScoreHUD.prefab");
        SaveLeaf(FindByPath("Canvas/ScoreHeight"), dir + "ScoreHeight.prefab");
        SaveLeaf(FindByPath("Canvas/LowStaminaPrompt"),
                 dir + "LowStaminaPrompt.prefab");
        SaveLeaf(FindByPath("Canvas/HackShortcutHelp"),
                 dir + "HackShortcutHelp.prefab");
        SaveLeaf(FindByPath("Canvas/Screens"), dir + "Screens.prefab");
        root.name = "UI";
        SaveGroup(root, dir + "UI.prefab");
    }

    [MenuItem("Monkobra/Extract/Report External References")]
    public static void ReportSelection() {
        GameObject picked = Selection.activeGameObject;
        if (picked == null) {
            Debug.LogWarning("PrefabExtractor:\tselect a GO in the scene");
            return;
        }
        List<ExternalRef> found = CollectExternalRefs(picked);
        Log(found.Count + " reference(s) leave " + picked.name);
        foreach (ExternalRef r in found) {
            Log("  " + r.Describe());
        }
    }

    // Prefab Helpers  #########################################################
    private static GameObject CreateGroup(string groupName) {
        GameObject existing = FindByPath(groupName);
        if (existing != null) {
            return existing;
        }
        GameObject group = new GameObject(groupName);
        Log("created group GO: " + groupName);
        return group;
    }

    private static void Adopt(
            GameObject root, string path, string prefabName, string dir) {
        GameObject child = FindByPath(path);
        if (child == null) {
            Debug.LogWarning("PrefabExtractor:\tfail to find GO: " + path);
            return;
        }
        SaveLeaf(child, dir + prefabName + ".prefab");
        child.transform.SetParent(root.transform, true);
    }

    /// <summary>Saves go as a prefab and connects it, unless already one.
    /// </summary>
    private static void SaveLeaf(GameObject go, string assetPath) {
        if (go == null) {
            Debug.LogWarning("PrefabExtractor:\tskip leaf, GO not found: "
                             + assetPath);
            return;
        }
        if (PrefabUtility.IsPartOfPrefabInstance(go)) {
            Log("skip, already a prefab instance: " + go.name);
            return;
        }
        SaveWithRefs(go, assetPath);
    }

    private static void SaveGroup(GameObject root, string assetPath) {
        SaveWithRefs(root, assetPath);
        EditorUtility.SetDirty(root);
        Log("group saved: " + assetPath + ", save the scene to keep it");
    }

    /// <summary>Records references leaving go, saves it, then writes them
    /// back as scene overrides.</summary>
    private static void SaveWithRefs(GameObject go, string assetPath) {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null) {
            Debug.LogWarning(
                "PrefabExtractor:\tprefab exists, left alone: " + assetPath);
            return;
        }
        EnsureFolder(assetPath);
        List<ExternalRef> outgoing = CollectExternalRefs(go);
        PrefabUtility.SaveAsPrefabAssetAndConnect(
            go, assetPath, InteractionMode.AutomatedAction);
        foreach (ExternalRef r in outgoing) {
            r.Restore();
        }
        Log("saved " + assetPath + ", restored " + outgoing.Count
            + " outgoing reference(s)");
    }

    private static void EnsureFolder(string assetPath) {
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length - 1; i++) {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    // Component Moves  ########################################################
    /// <summary>Moves the T component of source onto a new child GO of the
    /// group root, saves that child as a prefab, and repoints references.
    /// </summary>
    private static void SplitToChild<T>(
            GameObject source, GameObject groupRoot, string dir)
            where T : Component {
        if (source.GetComponent<T>() == null) {
            Debug.LogWarning("PrefabExtractor:\tno " + typeof(T).Name
                             + " on " + source.name);
            return;
        }
        GameObject child = new GameObject(typeof(T).Name);
        child.transform.SetParent(groupRoot.transform, false);
        MoveComponent<T>(source, child);
        SaveLeaf(child, dir + typeof(T).Name + ".prefab");
    }

    /// <summary>Copies the T component of source onto target, repoints every
    /// scene reference to it, then removes the original.</summary>
    private static void MoveComponent<T>(GameObject source, GameObject target)
            where T : Component {
        T original = source.GetComponent<T>();
        if (original == null) {
            return;
        }
        ComponentUtility.CopyComponent(original);
        ComponentUtility.PasteComponentAsNew(target);
        T fresh = target.GetComponent<T>();
        RepointReferences(original, fresh);
        UnityEngine.Object.DestroyImmediate(original);
        Log("moved " + typeof(T).Name + " from " + source.name + " to "
            + target.name);
    }

    private static void RepointReferences(
            UnityEngine.Object from, UnityEngine.Object to) {
        int repointed = 0;
        foreach (GameObject root in
                 SceneManager.GetActiveScene().GetRootGameObjects()) {
            foreach (Component c in
                     root.GetComponentsInChildren<Component>(true)) {
                if (c == null || c == to) {
                    continue;
                }
                SerializedObject so = new SerializedObject(c);
                SerializedProperty p = so.GetIterator();
                bool changed = false;
                while (p.NextVisible(true)) {
                    if (p.propertyType == SerializedPropertyType.ObjectReference
                            && p.objectReferenceValue == from) {
                        p.objectReferenceValue = to;
                        changed = true;
                        repointed++;
                    }
                }
                if (changed) {
                    so.ApplyModifiedProperties();
                }
            }
        }
        Log("repointed " + repointed + " reference(s) to "
            + to.GetType().Name);
    }

    // Reference Scan  #########################################################
    private static List<ExternalRef> CollectExternalRefs(GameObject root) {
        List<ExternalRef> found = new List<ExternalRef>();
        foreach (Component c in root.GetComponentsInChildren<Component>(true)) {
            if (c == null) {
                continue;
            }
            SerializedObject so = new SerializedObject(c);
            SerializedProperty p = so.GetIterator();
            while (p.NextVisible(true)) {
                if (p.propertyType != SerializedPropertyType.ObjectReference) {
                    continue;
                }
                UnityEngine.Object target = p.objectReferenceValue;
                if (target == null || !IsSceneObject(target)) {
                    continue;
                }
                if (IsInside(target, root)) {
                    continue;
                }
                found.Add(new ExternalRef(c, p.propertyPath, target));
            }
        }
        return found;
    }

    private static bool IsSceneObject(UnityEngine.Object target) {
        return !EditorUtility.IsPersistent(target)
            && (target is GameObject || target is Component);
    }

    private static bool IsInside(UnityEngine.Object target, GameObject root) {
        Transform t = target is GameObject
            ? ((GameObject)target).transform
            : ((Component)target).transform;
        return t.IsChildOf(root.transform);
    }

    // Lookup  #################################################################
    /// <summary>Finds a scene object by slash path from a root object, also
    /// when inactive.</summary>
    private static GameObject FindByPath(string path) {
        string[] parts = path.Split('/');
        foreach (GameObject root in
                 SceneManager.GetActiveScene().GetRootGameObjects()) {
            if (root.name != parts[0]) {
                continue;
            }
            if (parts.Length == 1) {
                return root;
            }
            Transform found = root.transform.Find(
                string.Join("/", parts, 1, parts.Length - 1));
            return found == null ? null : found.gameObject;
        }
        return null;
    }

    private static void Log(string message) {
        if (Debug.isDebugBuild) {
            Debug.Log("PrefabExtractor:\t" + message);
        }
    }

    // nested types  ###########################################################
    private class ExternalRef {
        private readonly Component owner;
        private readonly string propertyPath;
        private readonly UnityEngine.Object target;

        public ExternalRef(
                Component owner, string propertyPath,
                UnityEngine.Object target) {
            this.owner = owner;
            this.propertyPath = propertyPath;
            this.target = target;
        }

        public string Describe() {
            return owner.gameObject.name + "/" + owner.GetType().Name + "."
                + propertyPath + " -> " + target.name;
        }

        public void Restore() {
            if (owner == null || target == null) {
                return;
            }
            SerializedObject so = new SerializedObject(owner);
            SerializedProperty p = so.FindProperty(propertyPath);
            if (p == null || p.objectReferenceValue == target) {
                return;
            }
            p.objectReferenceValue = target;
            so.ApplyModifiedProperties();
        }
    }

    // constants  ##############################################################
    private const string PREFAB_ROOT = "Assets/_Monkobra/Prefabs/";
}
