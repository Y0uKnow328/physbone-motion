using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvatarChamber.PhysBoneMotion
{
    /// <summary>
    /// Saves PhysBone parameter edits made in Play Mode back to the edit-mode scene.
    /// Value/curve/enum fields are captured while playing and applied after EnteredEditMode.
    /// </summary>
    [InitializeOnLoad]
    public static class PhysBonePlayModeSave
    {
        private const string SnapshotKey = "AvatarChamber.PhysBoneMotion.SaveSnapshot";
        private const string PendingKey = "AvatarChamber.PhysBoneMotion.SavePending";
        private const string BaselineKey = "AvatarChamber.PhysBoneMotion.PhysBoneBaseline";
        private const string BaselineReadyKey = "AvatarChamber.PhysBoneMotion.PhysBoneBaselineReady";
        private const string TrackedKey = "AvatarChamber.PhysBoneMotion.TrackedBaselines";
        private const string ReloadKey = "AvatarChamber.PhysBoneMotion.SaveAndReload";
        private const string NamePathSeparator = "\u001F";

        // Only user-facing PhysBone configuration is written back.
        // Runtime state (bones, chain id, player id, net id, caches) and UI foldouts are excluded
        // so a Play Mode snapshot can never leave stale internal data in the edit-mode scene.
        private static readonly HashSet<string> SavableFields = new HashSet<string>
        {
            "endpointPosition",
            "multiChildType",
            "integrationType",
            "pull",
            "pullCurve",
            "spring",
            "springCurve",
            "stiffness",
            "stiffnessCurve",
            "gravity",
            "gravityCurve",
            "gravityFalloff",
            "gravityFalloffCurve",
            "immobileType",
            "immobile",
            "immobileCurve",
            "allowCollision",
            "collisionFilter.allowSelf",
            "collisionFilter.allowOthers",
            "collisionFilter.contentTypes",
            "radius",
            "radiusCurve",
            "maxStretch",
            "maxStretchCurve",
            "maxSquish",
            "maxSquishCurve",
            "stretchMotion",
            "stretchMotionCurve",
            "limitType",
            "maxAngleX",
            "maxAngleXCurve",
            "maxAngleZ",
            "maxAngleZCurve",
            "limitRotation",
            "limitRotationXCurve",
            "limitRotationYCurve",
            "limitRotationZCurve",
            "staticFreezeAxis",
            "allowGrabbing",
            "grabFilter.allowSelf",
            "grabFilter.allowOthers",
            "grabFilter.contentTypes",
            "allowPosing",
            "poseFilter.allowSelf",
            "poseFilter.allowOthers",
            "poseFilter.contentTypes",
            "snapToHand",
            "grabMovement",
            // "isAnimated" is intentionally excluded: the driver temporarily sets it to true
            // while previewing from the bone root, and saving that would persist a preview-only value.
            "resetWhenDisabled",
            "parameter",
            "showGizmos",
            "boneOpacity",
            "limitOpacity",
            "ignoreOtherPhysBones"
        };

        [Serializable]
        private class SavedProperty
        {
            public string path;
            public int type;
            public string value;
        }

        // JsonUtility cannot serialize AnimationCurve directly (it returns "{}").
        // Store the curve keys and wrap modes explicitly instead.
        [Serializable]
        private class SavedCurveKeyframe
        {
            public float time;
            public float value;
            public float inTangent;
            public float outTangent;
            public float inWeight;
            public float outWeight;
            public int weightedMode;
            public int tangentMode;
        }

        [Serializable]
        private class SavedCurve
        {
            public int preWrapMode;
            public int postWrapMode;
            public List<SavedCurveKeyframe> keys = new List<SavedCurveKeyframe>();
        }

        [Serializable]
        private class SavedSnapshot
        {
            public string scenePath;
            public string objectPath;
            public string objectNamePath;
            public int componentIndex;
            public string rootTransformPath;
            public string rootTransformNamePath;
            public List<SavedProperty> properties = new List<SavedProperty>();
        }

        [Serializable]
        private class SavedBatch
        {
            public List<SavedSnapshot> snapshots = new List<SavedSnapshot>();
        }

        static PhysBonePlayModeSave()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static bool HasPendingSave
        {
            get { return SessionState.GetBool(PendingKey, false); }
        }

        public static void QueueSave(Component physBone)
        {
            if (physBone == null)
            {
                return;
            }

            var batch = LoadBatch();
            var captured = new HashSet<string>();
            CaptureIntoBatch(batch, physBone, captured);

            // The user may edit one PhysBone, then select another before saving.
            // Track every target seen during Play Mode and stage the ones whose
            // parameters actually changed, so earlier edits are not lost.
            var tracked = LoadTracked();
            foreach (var baseline in tracked.snapshots)
            {
                var trackedComponent = ResolveSnapshotComponent(baseline);
                if (trackedComponent == null)
                {
                    continue;
                }

                var current = Capture(trackedComponent);
                if (!SnapshotsEqual(baseline, current))
                {
                    CaptureIntoBatch(batch, trackedComponent, captured);
                }
            }

            // PhysBones added while playing are not part of the edit-mode scene.
            // Capture every new component under the same avatar so they can be
            // recreated after leaving Play Mode, even if the window's target
            // still points at an older component.
            if (SessionState.GetBool(BaselineReadyKey, false))
            {
                var avatarRoot = PhysBoneReflection.FindAvatarRoot(physBone);
                if (avatarRoot != null)
                {
                    var baseline = LoadBaseline();
                    foreach (var candidate in PhysBoneReflection.CollectPhysBones(avatarRoot))
                    {
                        if (candidate == null || candidate == physBone)
                        {
                            continue;
                        }

                        var key = BuildComponentKey(candidate);
                        if (!string.IsNullOrEmpty(key) && !baseline.Contains(key))
                        {
                            CaptureIntoBatch(batch, candidate, captured);
                        }
                    }
                }
            }

            SessionState.SetString(SnapshotKey, JsonUtility.ToJson(batch));
            SessionState.SetBool(PendingKey, true);
            Debug.Log(
                "[PhysBone Motion] "
                + PhysBoneLocalization.Tr("已暂存 Play Mode 参数，退出 Play Mode 后写回场景。"));
        }

        private static void CaptureIntoBatch(
            SavedBatch batch,
            Component physBone,
            HashSet<string> captured)
        {
            var snapshot = Capture(physBone);
            var key = SnapshotIdentityKey(snapshot);
            if (!captured.Add(key))
            {
                return;
            }

            batch.snapshots.RemoveAll(item => SameSnapshotIdentity(item, snapshot));
            batch.snapshots.Add(snapshot);
        }

        /// <summary>
        /// Records the first state of every PhysBone the window targets during
        /// Play Mode. QueueSave compares against these baselines so changing a
        /// different bone afterwards does not discard earlier edits.
        /// </summary>
        public static void TrackTarget(Component physBone)
        {
            if (!EditorApplication.isPlaying || physBone == null)
            {
                return;
            }

            var tracked = LoadTracked();
            var snapshot = Capture(physBone);
            if (tracked.snapshots.Exists(item => SameSnapshotIdentity(item, snapshot)))
            {
                return;
            }

            tracked.snapshots.Add(snapshot);
            SessionState.SetString(TrackedKey, JsonUtility.ToJson(tracked));
        }

        private static SavedBatch LoadTracked()
        {
            var json = SessionState.GetString(TrackedKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new SavedBatch();
            }

            try
            {
                return JsonUtility.FromJson<SavedBatch>(json) ?? new SavedBatch();
            }
            catch
            {
                return new SavedBatch();
            }
        }

        private static string SnapshotIdentityKey(SavedSnapshot snapshot)
        {
            // Keep the sibling path in the identity: name paths are only used
            // to recover from shifted indices, and two different children can
            // legitimately share the same name path.
            return snapshot.scenePath
                + "|"
                + snapshot.objectPath
                + "|"
                + snapshot.componentIndex;
        }

        private static bool SameSnapshotIdentity(SavedSnapshot a, SavedSnapshot b)
        {
            return a != null
                && b != null
                && SnapshotIdentityKey(a) == SnapshotIdentityKey(b);
        }

        private static bool SnapshotsEqual(SavedSnapshot a, SavedSnapshot b)
        {
            if (a == null || b == null || a.properties == null || b.properties == null)
            {
                return false;
            }

            if (a.properties.Count != b.properties.Count)
            {
                return false;
            }

            var byPath = new Dictionary<string, SavedProperty>();
            foreach (var property in a.properties)
            {
                byPath[property.path] = property;
            }

            foreach (var property in b.properties)
            {
                SavedProperty previous;
                if (!byPath.TryGetValue(property.path, out previous)
                    || previous.type != property.type
                    || previous.value != property.value)
                {
                    return false;
                }
            }

            return true;
        }

        private static Component ResolveSnapshotComponent(SavedSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            Scene scene;
            if (!TryResolveScene(snapshot.scenePath, out scene))
            {
                return null;
            }

            var target = ResolveTransformPath(
                scene,
                snapshot.objectPath,
                snapshot.objectNamePath);
            var physBoneType = PhysBoneReflection.PhysBoneType;
            if (target == null || physBoneType == null)
            {
                return null;
            }

            var components = target.GetComponents(physBoneType);
            if (snapshot.componentIndex >= 0
                && snapshot.componentIndex < components.Length)
            {
                return components[snapshot.componentIndex];
            }

            // Only legacy snapshots (index -1) may fall back to the first
            // component. An out-of-range positive index means the tracked
            // component was removed; returning another component would write
            // one PhysBone's values onto a different one.
            if (snapshot.componentIndex < 0 && components.Length > 0)
            {
                return components[0];
            }

            return null;
        }

        public static void SaveAndReload(Component physBone)
        {
            QueueSave(physBone);
            SessionState.SetBool(ReloadKey, true);
            PhysBoneMotionDriver.Stop();
            EditorApplication.isPlaying = false;
        }

        public static void SaveAndExit(Component physBone)
        {
            QueueSave(physBone);
            PhysBoneMotionDriver.Stop();
            EditorApplication.isPlaying = false;
        }

        public static void ExitWithoutSaving()
        {
            CancelSave();
            PhysBoneMotionDriver.Stop();
            EditorApplication.isPlaying = false;
        }

        public static void CancelSave()
        {
            SessionState.EraseBool(PendingKey);
            SessionState.EraseString(SnapshotKey);
        }

        private static SavedBatch LoadBatch()
        {
            var json = SessionState.GetString(SnapshotKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new SavedBatch();
            }

            try
            {
                return JsonUtility.FromJson<SavedBatch>(json) ?? new SavedBatch();
            }
            catch
            {
                return new SavedBatch();
            }
        }

        private static HashSet<string> LoadBaseline()
        {
            var result = new HashSet<string>();
            var raw = SessionState.GetString(BaselineKey, string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return result;
            }

            foreach (var line in raw.Split(
                new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(line);
            }

            return result;
        }

        private static void StoreBaseline()
        {
            var type = PhysBoneReflection.PhysBoneType;
            if (type == null)
            {
                SessionState.SetBool(BaselineReadyKey, false);
                SessionState.EraseString(BaselineKey);
                return;
            }

            var keys = new List<string>();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    var components = root.GetComponentsInChildren(type, true);
                    foreach (var component in components)
                    {
                        var key = BuildComponentKey(component);
                        if (!string.IsNullOrEmpty(key))
                        {
                            keys.Add(key);
                        }
                    }
                }
            }

            SessionState.SetString(BaselineKey, string.Join("\n", keys));
            SessionState.SetBool(BaselineReadyKey, true);
        }

        private static string BuildComponentKey(Component physBone)
        {
            var type = PhysBoneReflection.PhysBoneType;
            if (physBone == null || type == null)
            {
                return null;
            }

            var components = physBone.gameObject.GetComponents(type);
            var componentIndex = Array.IndexOf(components, physBone);
            return physBone.gameObject.scene.path
                + "|"
                + BuildSiblingPath(physBone.transform)
                + "|"
                + componentIndex;
        }

        private static SavedSnapshot Capture(Component physBone)
        {
            var snapshot = new SavedSnapshot
            {
                scenePath = physBone.gameObject.scene.path,
                objectPath = BuildSiblingPath(physBone.transform),
                objectNamePath = BuildNamePath(physBone.transform),
                componentIndex = -1
            };

            var physBoneType = PhysBoneReflection.PhysBoneType;
            if (physBoneType != null)
            {
                var components = physBone.gameObject.GetComponents(physBoneType);
                snapshot.componentIndex = Array.IndexOf(components, physBone);
            }

            var serializedObject = new SerializedObject(physBone);
            serializedObject.Update();

            // Object references are normally excluded from write-back, but the
            // root transform is essential for recreating a PhysBone that was
            // added while playing. Store its path when it is not the self node.
            var rootTransformProperty = serializedObject.FindProperty("rootTransform");
            if (rootTransformProperty != null
                && rootTransformProperty.objectReferenceValue is Transform rootTransform
                && rootTransform != physBone.transform)
            {
                snapshot.rootTransformPath = BuildSiblingPath(rootTransform);
                snapshot.rootTransformNamePath = BuildNamePath(rootTransform);
            }

            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.isArray)
                {
                    enterChildren = false;
                    continue;
                }

                if (iterator.propertyType == SerializedPropertyType.Generic
                    || iterator.propertyType == SerializedPropertyType.ObjectReference
                    || iterator.propertyType == SerializedPropertyType.ArraySize)
                {
                    continue;
                }

                if (!SavableFields.Contains(iterator.propertyPath))
                {
                    continue;
                }

                string value;
                if (!TryGetValue(iterator, out value))
                {
                    continue;
                }

                snapshot.properties.Add(new SavedProperty
                {
                    path = iterator.propertyPath,
                    type = (int)iterator.propertyType,
                    value = value
                });
            }

            return snapshot;
        }

        private static bool TryGetValue(SerializedProperty property, out string value)
        {
            value = null;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    value = property.floatValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;

                case SerializedPropertyType.Integer:
                    value = property.intValue.ToString(CultureInfo.InvariantCulture);
                    return true;

                case SerializedPropertyType.Boolean:
                    value = property.boolValue ? "1" : "0";
                    return true;

                case SerializedPropertyType.String:
                    value = property.stringValue;
                    return true;

                case SerializedPropertyType.Enum:
                    value = property.intValue.ToString(CultureInfo.InvariantCulture);
                    return true;

                case SerializedPropertyType.Vector2:
                    var v2 = property.vector2Value;
                    value = Join(v2.x, v2.y);
                    return true;

                case SerializedPropertyType.Vector3:
                    var v3 = property.vector3Value;
                    value = Join(v3.x, v3.y, v3.z);
                    return true;

                case SerializedPropertyType.Color:
                    var color = property.colorValue;
                    value = Join(color.r, color.g, color.b, color.a);
                    return true;

                case SerializedPropertyType.AnimationCurve:
                    value = SerializeCurve(property.animationCurveValue);
                    return true;

                default:
                    return false;
            }
        }

        private static string Join(params float[] values)
        {
            var parts = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            }
            return string.Join("|", parts);
        }

        private static string SerializeCurve(AnimationCurve curve)
        {
            if (curve == null)
            {
                return string.Empty;
            }

            var saved = new SavedCurve
            {
                preWrapMode = (int)curve.preWrapMode,
                postWrapMode = (int)curve.postWrapMode
            };

            foreach (var key in curve.keys)
            {
                saved.keys.Add(new SavedCurveKeyframe
                {
                    time = key.time,
                    value = key.value,
                    inTangent = key.inTangent,
                    outTangent = key.outTangent,
                    inWeight = key.inWeight,
                    outWeight = key.outWeight,
                    weightedMode = (int)key.weightedMode,
                    tangentMode = key.tangentMode
                });
            }

            return JsonUtility.ToJson(saved);
        }

        private static AnimationCurve DeserializeCurve(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            SavedCurve saved;
            try
            {
                saved = JsonUtility.FromJson<SavedCurve>(value);
            }
            catch
            {
                return null;
            }

            if (saved == null)
            {
                return null;
            }

            var count = saved.keys != null ? saved.keys.Count : 0;
            var keys = new Keyframe[count];
            for (var i = 0; i < count; i++)
            {
                var source = saved.keys[i];
                if (source == null)
                {
                    keys[i] = new Keyframe();
                    continue;
                }

                var key = new Keyframe(
                    source.time,
                    source.value,
                    source.inTangent,
                    source.outTangent);
                key.inWeight = source.inWeight;
                key.outWeight = source.outWeight;
                key.weightedMode = (WeightedMode)source.weightedMode;
                key.tangentMode = source.tangentMode;
                keys[i] = key;
            }

            var curve = new AnimationCurve(keys);
            curve.preWrapMode = (WrapMode)saved.preWrapMode;
            curve.postWrapMode = (WrapMode)saved.postWrapMode;
            return curve;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // A new Play Mode session cannot need a pending reload anymore.
                SessionState.EraseBool(ReloadKey);

                // Tracked baselines are cleared on EnteredEditMode. Do not clear
                // them here: the window may sync its target before or after this
                // event, and clearing here could race with the first TrackTarget.
                StoreBaseline();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                ApplyPendingSave();
                SessionState.EraseString(BaselineKey);
                SessionState.EraseBool(BaselineReadyKey);
                SessionState.EraseString(TrackedKey);

                if (SessionState.GetBool(ReloadKey, false))
                {
                    SessionState.EraseBool(ReloadKey);
                    Debug.Log(
                        "[PhysBone Motion] "
                        + PhysBoneLocalization.Tr("已保存修改，正在重新进入 Play Mode。"));

                    // Apply the saved parameters first, then start Play Mode
                    // again on the next editor update.
                    EditorApplication.delayCall += () =>
                    {
                        if (!EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = true;
                        }
                    };
                }
            }
        }

        private static void ApplyPendingSave()
        {
            if (!SessionState.GetBool(PendingKey, false))
            {
                return;
            }

            var batch = LoadBatch();
            CancelSave();
            if (batch.snapshots == null || batch.snapshots.Count == 0)
            {
                return;
            }

            var total = 0;
            foreach (var snapshot in batch.snapshots)
            {
                total += ApplySnapshot(snapshot);
            }

            if (total > 0)
            {
                Debug.Log(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Format("已写回 {0} 项 Play Mode 参数。", total));
            }
        }

        private static int ApplySnapshot(SavedSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            Scene scene;
            if (!TryResolveScene(snapshot.scenePath, out scene))
            {
                Debug.LogWarning(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Format(
                        "找不到原场景，已跳过写回：{0}",
                        snapshot.scenePath));
                return 0;
            }

            var target = ResolveTransformPath(
                scene,
                snapshot.objectPath,
                snapshot.objectNamePath);
            if (target == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Format(
                        "找不到要写回参数的 PhysBone：{0}",
                        snapshot.objectPath));
                return 0;
            }

            var physBoneType = PhysBoneReflection.PhysBoneType;
            if (physBoneType == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Tr("未检测到 VRCPhysBone，无法写回参数。"));
                return 0;
            }

            Component physBone = null;
            var components = target.GetComponents(physBoneType);
            if (snapshot.componentIndex >= 0
                && snapshot.componentIndex < components.Length)
            {
                physBone = components[snapshot.componentIndex];
            }
            else if (snapshot.componentIndex < 0 && components.Length > 0)
            {
                // Compatibility with snapshots captured before componentIndex existed.
                physBone = components[0];
            }

            var created = false;
            if (physBone == null)
            {
                // A PhysBone added while playing does not exist in the edit-mode
                // scene. Recreate it on the same object before writing its fields.
                physBone = Undo.AddComponent(target.gameObject, physBoneType);
                created = physBone != null;

                if (created)
                {
                    Debug.Log(
                        "[PhysBone Motion] "
                        + PhysBoneLocalization.Format(
                            "已重建 Play Mode 中新建的 PhysBone：{0}",
                            snapshot.objectPath));
                }
            }

            if (physBone == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Format(
                        "找不到或无法重建要写回参数的 PhysBone：{0}",
                        snapshot.objectPath));
                return 0;
            }

            Undo.RecordObject(physBone, "Write Back PhysBone Parameters");

            var serializedObject = new SerializedObject(physBone);
            serializedObject.Update();

            var applied = 0;
            if (snapshot.properties != null)
            {
                foreach (var saved in snapshot.properties)
                {
                    var property = serializedObject.FindProperty(saved.path);
                    if (property == null)
                    {
                        continue;
                    }

                    if (TryApplyValue(property, (SerializedPropertyType)saved.type, saved.value))
                    {
                        applied++;
                    }
                }
            }

            if (!string.IsNullOrEmpty(snapshot.rootTransformPath))
            {
                var rootProperty = serializedObject.FindProperty("rootTransform");
                var rootTransform = ResolveTransformPath(
                    scene,
                    snapshot.rootTransformPath,
                    snapshot.rootTransformNamePath);
                if (rootProperty != null && rootTransform != null)
                {
                    rootProperty.objectReferenceValue = rootTransform;
                    applied++;
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(physBone);
            EditorSceneManager.MarkSceneDirty(scene);
            return applied;
        }

        private static bool TryApplyValue(
            SerializedProperty property,
            SerializedPropertyType type,
            string value)
        {
            try
            {
                switch (type)
                {
                    case SerializedPropertyType.Float:
                        property.floatValue = float.Parse(value, CultureInfo.InvariantCulture);
                        return true;

                    case SerializedPropertyType.Integer:
                        property.intValue = int.Parse(value, CultureInfo.InvariantCulture);
                        return true;

                    case SerializedPropertyType.Boolean:
                        property.boolValue = value == "1";
                        return true;

                    case SerializedPropertyType.String:
                        property.stringValue = value;
                        return true;

                    case SerializedPropertyType.Enum:
                        property.intValue = int.Parse(value, CultureInfo.InvariantCulture);
                        return true;

                    case SerializedPropertyType.Vector2:
                        var v2 = Split(value, 2);
                        property.vector2Value = new Vector2(v2[0], v2[1]);
                        return true;

                    case SerializedPropertyType.Vector3:
                        var v3 = Split(value, 3);
                        property.vector3Value = new Vector3(v3[0], v3[1], v3[2]);
                        return true;

                    case SerializedPropertyType.Color:
                        var color = Split(value, 4);
                        property.colorValue = new Color(color[0], color[1], color[2], color[3]);
                        return true;

                    case SerializedPropertyType.AnimationCurve:
                        var curve = DeserializeCurve(value);
                        if (curve == null)
                        {
                            return false;
                        }
                        property.animationCurveValue = curve;
                        return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Format(
                        "写回参数失败：{0}",
                        property.propertyPath + " / " + exception.Message));
            }

            return false;
        }

        private static float[] Split(string value, int count)
        {
            var parts = value.Split('|');
            var result = new float[count];
            for (var i = 0; i < count && i < parts.Length; i++)
            {
                result[i] = float.Parse(parts[i], CultureInfo.InvariantCulture);
            }
            return result;
        }

        private static string BuildSiblingPath(Transform transform)
        {
            var indices = new List<int>();
            var current = transform;
            while (current != null)
            {
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            indices.Reverse();
            return string.Join("/", indices);
        }

        private static Transform ResolveSiblingPath(Scene scene, string path)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(path))
            {
                return null;
            }

            var parts = path.Split('/');
            var roots = scene.GetRootGameObjects();
            int index;
            if (!int.TryParse(parts[0], out index) || index < 0 || index >= roots.Length)
            {
                return null;
            }

            var current = roots[index].transform;
            for (var i = 1; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out index) || index < 0 || index >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(index);
            }

            return current;
        }

        private static bool TryResolveScene(string scenePath, out Scene scene)
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                scene = SceneManager.GetActiveScene();
                return scene.IsValid() && scene.isLoaded;
            }

            // Never fall back to a different scene for a saved scene path:
            // sibling indices in another scene could point at an unrelated bone.
            scene = SceneManager.GetSceneByPath(scenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        private static Transform ResolveTransformPath(
            Scene scene,
            string siblingPath,
            string namePath)
        {
            var byIndex = ResolveSiblingPath(scene, siblingPath);
            if (byIndex != null
                && (string.IsNullOrEmpty(namePath) || BuildNamePath(byIndex) == namePath))
            {
                return byIndex;
            }

            if (string.IsNullOrEmpty(namePath))
            {
                return byIndex;
            }

            // The hierarchy may have shifted (for example a new sibling was
            // added in Play Mode). Fall back to a unique name path.
            Transform match = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (BuildNamePath(transform) != namePath)
                    {
                        continue;
                    }

                    if (match != null)
                    {
                        // Ambiguous; writing could hit the wrong bone.
                        return null;
                    }

                    match = transform;
                }
            }

            return match;
        }

        private static string BuildNamePath(Transform transform)
        {
            var names = new List<string>();
            var current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join(NamePathSeparator, names);
        }
    }
}
