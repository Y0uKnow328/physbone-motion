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

        [Serializable]
        private class SavedSnapshot
        {
            public string scenePath;
            public string objectPath;
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
            var snapshot = Capture(physBone);
            batch.snapshots.RemoveAll(item =>
                item.scenePath == snapshot.scenePath
                && item.objectPath == snapshot.objectPath);
            batch.snapshots.Add(snapshot);

            SessionState.SetString(SnapshotKey, JsonUtility.ToJson(batch));
            SessionState.SetBool(PendingKey, true);
            Debug.Log(
                "[PhysBone Motion] "
                + PhysBoneLocalization.Tr("已暂存 Play Mode 参数，退出 Play Mode 后写回场景。"));
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

        private static SavedSnapshot Capture(Component physBone)
        {
            var snapshot = new SavedSnapshot
            {
                scenePath = physBone.gameObject.scene.path,
                objectPath = BuildSiblingPath(physBone.transform)
            };

            var serializedObject = new SerializedObject(physBone);
            serializedObject.Update();

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
                    value = JsonUtility.ToJson(property.animationCurveValue);
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

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                ApplyPendingSave();
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

            var scene = string.IsNullOrEmpty(snapshot.scenePath)
                ? SceneManager.GetActiveScene()
                : SceneManager.GetSceneByPath(snapshot.scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = SceneManager.GetActiveScene();
            }

            var target = ResolveSiblingPath(scene, snapshot.objectPath);
            var physBone = target != null && PhysBoneReflection.PhysBoneType != null
                ? target.GetComponent(PhysBoneReflection.PhysBoneType)
                : null;
            if (physBone == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion] "
                    + PhysBoneLocalization.Format(
                        "找不到要写回参数的 PhysBone：{0}",
                        snapshot.objectPath));
                return 0;
            }

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
                        var curve = JsonUtility.FromJson<AnimationCurve>(value);
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
    }
}
