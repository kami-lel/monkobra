using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-off editor tool, menu <c>Monkobra/Merge WebDemo Into Lv1Scene</c>:
/// carries the spider web setup of <c>WebDemo</c> over into
/// <c>Lv1Scene</c>.
/// <para>
/// WebDemo is a copy of an older Lv1Scene plus that setup, so only the setup
/// moves, onto Lv1Scene's own objects: <see cref="SpiderWebSpawner"/> is
/// added to <c>Envs/Tree</c> with WebDemo's field values, and the
/// <c>Canvas/SpiderWebPrompt</c> subtree is copied under Lv1Scene's Canvas
/// at the same sibling index. The monkey already carries
/// <see cref="SpiderWebStruggle"/> from its prefab.
/// </para>
/// <para>
/// Every reference the copies still hold into WebDemo is rewired to its
/// Lv1Scene counterpart, then every serialized field of every copied object
/// is compared against WebDemo. Lv1Scene is saved only if all of it checks
/// out, WebDemo is closed and never saved. A Lv1Scene that already holds the
/// setup is left untouched, so running it twice is harmless.
/// </para>
/// </summary>
public static class WebDemoMergeTool {
    // Menu Items  #############################################################
    [MenuItem(MENU_PATH)]
    private static void Merge() {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            Debug.Log(LOG_PREFIX + "cancelled, nothing changed");
            return;
        }

        Scene target = EditorSceneManager.OpenScene(
            TARGET_SCENE_PATH,
            OpenSceneMode.Single
        );
        Scene source = EditorSceneManager.OpenScene(
            SOURCE_SCENE_PATH,
            OpenSceneMode.Additive
        );
        if (!target.IsValid() || !source.IsValid()) {
            Report(
                "failed to open "
                    + TARGET_SCENE_PATH
                    + " or "
                    + SOURCE_SCENE_PATH
                    + ", nothing changed",
                isError: true
            );
            return;
        }
        // new objects land in the active scene before any reparenting
        SceneManager.SetActiveScene(target);

        var errors = new List<string>();
        MergeOutcome outcome = TryMerge(source, target, errors);

        if (source.isDirty) {
            Debug.LogWarning(
                LOG_PREFIX + "WebDemo changed in memory, it is not saved"
            );
        }

        switch (outcome) {
            case MergeOutcome.AlreadyMerged:
                EditorSceneManager.CloseScene(source, true);
                Report(
                    "Lv1Scene already holds the spider web setup, nothing "
                        + "changed. If it is a partial merge, remove "
                        + "SpiderWebSpawner from Envs/Tree and "
                        + "Canvas/SpiderWebPrompt, then run again",
                    isError: false
                );
                return;

            case MergeOutcome.Failed:
                foreach (string error in errors) {
                    Debug.LogError(LOG_PREFIX + error);
                }
                // both scenes stay open, so the failure can be inspected
                Report(
                    $"{errors.Count} problem(s), see the errors above. "
                        + "Lv1Scene is NOT saved and WebDemo is untouched. "
                        + "To discard the half merge, reopen Lv1Scene and "
                        + "choose Don't Save",
                    isError: true
                );
                return;
        }

        if (!EditorSceneManager.SaveScene(target)) {
            Report(
                "merged, but saving Lv1Scene failed, it stays open unsaved",
                isError: true
            );
            return;
        }
        // Lv1Scene is saved first, so closing WebDemo cuts no live reference
        EditorSceneManager.CloseScene(source, true);
        Report(
            "done: saved Lv1Scene, closed WebDemo without saving",
            isError: false
        );
    }

    [MenuItem(MENU_PATH, true)]
    private static bool CanMerge() {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    // constants  ##############################################################
    private const string MENU_PATH = "Monkobra/Merge WebDemo Into Lv1Scene";
    private const string LOG_PREFIX = "WebDemoMergeTool:\t";
    private const string SOURCE_SCENE_PATH =
        "Assets/_Monkobra/Scenes/WebDemo.unity";
    private const string TARGET_SCENE_PATH =
        "Assets/_Monkobra/Scenes/Lv1Scene.unity";
    private const string TREE_PATH = "Envs/Tree";
    private const string CANVAS_PATH = "Canvas";
    private const string PROMPT_PATH = "Canvas/SpiderWebPrompt";

    // private members  ########################################################
    private enum MergeOutcome {
        Merged,
        AlreadyMerged,
        Failed,
    }

    // private methods  ########################################################
    private static MergeOutcome TryMerge(
        Scene source,
        Scene target,
        List<string> errors
    ) {
        // Locate  -------------------------------------------------------------
        Transform sourceTree = FindByPath(source, TREE_PATH, errors);
        Transform sourceCanvas = FindByPath(source, CANVAS_PATH, errors);
        Transform sourcePrompt = FindByPath(source, PROMPT_PATH, errors);
        Transform targetTree = FindByPath(target, TREE_PATH, errors);
        Transform targetCanvas = FindByPath(target, CANVAS_PATH, errors);
        SpiderWebStruggle sourceStruggle =
            FindSingle<SpiderWebStruggle>(source, errors);
        SpiderWebStruggle targetStruggle =
            FindSingle<SpiderWebStruggle>(target, errors);
        if (errors.Count > 0) {
            return MergeOutcome.Failed;
        }

        SpiderWebSpawner sourceSpawner =
            sourceTree.GetComponent<SpiderWebSpawner>();
        if (sourceSpawner == null) {
            errors.Add("WebDemo: no SpiderWebSpawner on " + TREE_PATH);
        }
        if (sourcePrompt.GetComponent<SpiderWebPrompt>() == null) {
            errors.Add("WebDemo: no SpiderWebPrompt on " + PROMPT_PATH);
        }
        if (errors.Count > 0) {
            return MergeOutcome.Failed;
        }

        // Already Merged Guard  -----------------------------------------------
        if (
            targetTree.GetComponent<SpiderWebSpawner>() != null
            || FindAll<SpiderWebPrompt>(target).Count > 0
        ) {
            return MergeOutcome.AlreadyMerged;
        }

        // 1 undo step for the whole merge, until Lv1Scene is saved
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Merge WebDemo Into Lv1Scene");
        int undoGroup = Undo.GetCurrentGroup();

        // Spawner  ------------------------------------------------------------
        SpiderWebSpawner targetSpawner =
            Undo.AddComponent<SpiderWebSpawner>(targetTree.gameObject);
        CopyFields(sourceSpawner, targetSpawner);
        Debug.Log(
            LOG_PREFIX + "added SpiderWebSpawner to Lv1Scene " + TREE_PATH
        );

        // Prompt UI  ----------------------------------------------------------
        // Instantiate already points references inside the subtree at the
        // copies, the ones leading out of it still point into WebDemo
        GameObject targetPrompt = Object.Instantiate(
            sourcePrompt.gameObject,
            targetCanvas,
            false
        );
        Undo.RegisterCreatedObjectUndo(targetPrompt, "Copy SpiderWebPrompt");
        targetPrompt.name = sourcePrompt.name;
        targetPrompt.transform.SetSiblingIndex(
            sourcePrompt.GetSiblingIndex()
        );
        Debug.Log(
            LOG_PREFIX
                + "copied "
                + PROMPT_PATH
                + " into Lv1Scene, sibling index "
                + targetPrompt.transform.GetSiblingIndex()
        );

        // Counterparts  -------------------------------------------------------
        // WebDemo object to the Lv1Scene object standing in for it
        var counterparts = new Dictionary<Object, Object>();
        MapHierarchy(
            sourcePrompt,
            targetPrompt.transform,
            counterparts,
            errors
        );
        MapComponents(
            sourceTree.gameObject,
            targetTree.gameObject,
            counterparts,
            errors
        );
        MapComponents(
            sourceCanvas.gameObject,
            targetCanvas.gameObject,
            counterparts,
            errors
        );
        MapComponents(
            sourceStruggle.gameObject,
            targetStruggle.gameObject,
            counterparts,
            errors
        );
        if (errors.Count > 0) {
            return MergeOutcome.Failed;
        }

        // every copied object, paired w/ its WebDemo original
        var copies = new List<(Object original, Object copy)> {
            (sourceSpawner, targetSpawner),
        };
        foreach (
            Transform part in sourcePrompt.GetComponentsInChildren<Transform>(
                true
            )
        ) {
            copies.Add((part.gameObject, counterparts[part.gameObject]));
            foreach (Component component in part.GetComponents<Component>()) {
                copies.Add((component, counterparts[component]));
            }
        }

        // Rewire & Verify  ----------------------------------------------------
        foreach ((Object original, Object copy) pair in copies) {
            RewireReferences(pair.copy, source, counterparts, errors);
        }
        int checkedCount = 0;
        foreach ((Object original, Object copy) pair in copies) {
            checkedCount += CompareFields(
                pair.original,
                pair.copy,
                counterparts,
                errors
            );
        }
        Undo.CollapseUndoOperations(undoGroup);

        if (errors.Count > 0) {
            return MergeOutcome.Failed;
        }
        Debug.Log(
            LOG_PREFIX
                + $"verified {checkedCount} fields on {copies.Count} objects, "
                + "all match WebDemo"
        );
        return MergeOutcome.Merged;
    }

    // Lookup  -----------------------------------------------------------------
    // demands 1 match per level, so same-named siblings fail loudly rather
    // than pick either: Lv1Scene's Canvas has 2 children named ProgressBar
    private static Transform FindByPath(
        Scene scene,
        string path,
        List<string> errors
    ) {
        string[] names = path.Split('/');
        var matches = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.name == names[0]) {
                matches.Add(root.transform);
            }
        }
        for (
            int depth = 1;
            depth < names.Length && matches.Count == 1;
            depth++
        ) {
            Transform parent = matches[0];
            matches.Clear();
            foreach (Transform child in parent) {
                if (child.name == names[depth]) {
                    matches.Add(child);
                }
            }
        }

        if (matches.Count != 1) {
            errors.Add(
                $"{scene.name}: expected 1 object at {path}, "
                    + $"found {matches.Count} on the way"
            );
            return null;
        }
        return matches[0];
    }

    private static List<T> FindAll<T>(Scene scene) where T: Component {
        var found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects()) {
            found.AddRange(root.GetComponentsInChildren<T>(true));
        }
        return found;
    }

    private static T FindSingle<T>(Scene scene, List<string> errors)
        where T: Component {
        List<T> found = FindAll<T>(scene);
        if (found.Count != 1) {
            errors.Add(
                $"{scene.name}: expected 1 {typeof(T).Name}, "
                    + $"found {found.Count}"
            );
            return null;
        }
        return found[0];
    }

    // Copy  -------------------------------------------------------------------
    // top-level properties only, each copy carries its children along
    private static void CopyFields(Object source, Object target) {
        var sourceSerialized = new SerializedObject(source);
        var targetSerialized = new SerializedObject(target);
        SerializedProperty property = sourceSerialized.GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren)) {
            enterChildren = false;
            if (!UNCOPIED_PROPERTIES.Contains(property.propertyPath)) {
                targetSerialized.CopyFromSerializedProperty(property);
            }
        }
        targetSerialized.ApplyModifiedProperties();
    }

    // identity of the component itself, never taken from the original
    private static readonly HashSet<string> UNCOPIED_PROPERTIES = new() {
        "m_GameObject",
        "m_Script",
        "m_CorrespondingSourceObject",
        "m_PrefabInstance",
        "m_PrefabAsset",
    };

    // Counterpart Mapping  ----------------------------------------------------
    // same structure on both sides, so children pair up by index, which also
    // copes w/ same-named siblings
    private static void MapHierarchy(
        Transform source,
        Transform target,
        Dictionary<Object, Object> counterparts,
        List<string> errors
    ) {
        MapComponents(
            source.gameObject,
            target.gameObject,
            counterparts,
            errors
        );
        if (source.childCount != target.childCount) {
            errors.Add(
                $"child count differs under {Describe(source.gameObject)}: "
                    + $"{source.childCount} vs {target.childCount}"
            );
            return;
        }
        for (int i = 0; i < source.childCount; i++) {
            MapHierarchy(
                source.GetChild(i),
                target.GetChild(i),
                counterparts,
                errors
            );
        }
    }

    // components pair up by index, each pair must share a type
    private static void MapComponents(
        GameObject source,
        GameObject target,
        Dictionary<Object, Object> counterparts,
        List<string> errors
    ) {
        counterparts[source] = target;
        Component[] sourceComponents = source.GetComponents<Component>();
        Component[] targetComponents = target.GetComponents<Component>();
        if (sourceComponents.Length != targetComponents.Length) {
            errors.Add(
                $"component count differs on {Describe(source)}: "
                    + $"{sourceComponents.Length} in WebDemo vs "
                    + $"{targetComponents.Length} in Lv1Scene"
            );
            return;
        }
        for (int i = 0; i < sourceComponents.Length; i++) {
            Component sourceComponent = sourceComponents[i];
            Component targetComponent = targetComponents[i];
            if (sourceComponent == null || targetComponent == null) {
                errors.Add($"missing script on {Describe(source)}");
                continue;
            }
            if (sourceComponent.GetType() != targetComponent.GetType()) {
                errors.Add(
                    $"component {i} on {Describe(source)} is "
                        + $"{sourceComponent.GetType().Name} in WebDemo vs "
                        + $"{targetComponent.GetType().Name} in Lv1Scene"
                );
                continue;
            }
            counterparts[sourceComponent] = targetComponent;
        }
    }

    // Rewire  -----------------------------------------------------------------
    // a reference still into WebDemo would be cut on save, so point it at the
    // counterpart, or report it as a manual step when there is none
    private static void RewireReferences(
        Object copy,
        Scene source,
        Dictionary<Object, Object> counterparts,
        List<string> errors
    ) {
        var serialized = new SerializedObject(copy);
        SerializedProperty property = serialized.GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren)) {
            enterChildren =
                property.propertyType == SerializedPropertyType.Generic;
            if (
                property.propertyType != SerializedPropertyType.ObjectReference
            ) {
                continue;
            }

            Object reference = property.objectReferenceValue;
            if (!IsInScene(reference, source)) {
                continue;
            }
            if (counterparts.TryGetValue(reference, out Object counterpart)) {
                property.objectReferenceValue = counterpart;
                Debug.Log(
                    LOG_PREFIX
                        + $"rewired {Describe(copy)} > {property.displayName}: "
                        + $"WebDemo {Describe(reference)} -> "
                        + $"Lv1Scene {Describe(counterpart)}"
                );
            } else {
                errors.Add(
                    $"manual step: {Describe(copy)} > {property.displayName} "
                        + $"points at WebDemo {Describe(reference)}, which "
                        + "has no Lv1Scene counterpart, assign it by hand"
                );
            }
        }
        serialized.ApplyModifiedProperties();
    }

    // Verify  -----------------------------------------------------------------
    // every leaf property must equal the original's, a reference must equal
    // the original's counterpart, or the original itself for an asset
    /// <returns>count of leaf properties compared</returns>
    private static int CompareFields(
        Object original,
        Object copy,
        Dictionary<Object, Object> counterparts,
        List<string> errors
    ) {
        var originalSerialized = new SerializedObject(original);
        var copySerialized = new SerializedObject(copy);
        SerializedProperty property = originalSerialized.GetIterator();
        bool enterChildren = true;
        int checkedCount = 0;
        while (property.Next(enterChildren)) {
            enterChildren =
                property.propertyType == SerializedPropertyType.Generic;
            // a container's leaves are compared one by one
            if (enterChildren) {
                continue;
            }

            checkedCount++;
            SerializedProperty copyProperty =
                copySerialized.FindProperty(property.propertyPath);
            if (copyProperty == null) {
                errors.Add(
                    $"{Describe(copy)} lacks field {property.propertyPath}"
                );
                continue;
            }

            bool isEqual;
            if (
                property.propertyType == SerializedPropertyType.ObjectReference
            ) {
                Object expected = property.objectReferenceValue;
                if (
                    expected != null
                    && counterparts.TryGetValue(expected, out Object mapped)
                ) {
                    expected = mapped;
                }
                isEqual = copyProperty.objectReferenceValue == expected;
            } else {
                isEqual = SerializedProperty.DataEquals(
                    property,
                    copyProperty
                );
            }

            if (!isEqual) {
                errors.Add(
                    $"{Describe(copy)} field {property.propertyPath} "
                        + "differs from WebDemo"
                );
            }
        }
        return checkedCount;
    }

    // Helpers  ----------------------------------------------------------------
    private static GameObject OwnerOf(Object obj) {
        return obj switch {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            _ => null,
        };
    }

    // assets live in no scene, so they are never rewired
    private static bool IsInScene(Object obj, Scene scene) {
        if (obj == null || EditorUtility.IsPersistent(obj)) {
            return false;
        }
        GameObject owner = OwnerOf(obj);
        return owner != null && owner.scene == scene;
    }

    // hierarchy path, plus the component type for a component
    private static string Describe(Object obj) {
        GameObject owner = OwnerOf(obj);
        if (owner == null) {
            return obj != null ? obj.name : "None";
        }

        string path = owner.name;
        for (
            Transform parent = owner.transform.parent;
            parent != null;
            parent = parent.parent
        ) {
            path = parent.name + "/" + path;
        }
        return obj is GameObject ? path : $"{path} ({obj.GetType().Name})";
    }

    private static void Report(string message, bool isError) {
        if (isError) {
            Debug.LogError(LOG_PREFIX + message);
        } else {
            Debug.Log(LOG_PREFIX + message);
        }
        EditorUtility.DisplayDialog(
            "Merge WebDemo Into Lv1Scene",
            message,
            "OK"
        );
    }
}
