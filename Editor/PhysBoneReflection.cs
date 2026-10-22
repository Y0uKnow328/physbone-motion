using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AvatarChamber.PhysBoneMotionPreview
{
    /// <summary>
    /// Reflection wrapper around VRCPhysBone so this package compiles in any project,
    /// even when the VRChat SDK is not installed.
    /// </summary>
    public static class PhysBoneReflection
    {
        public const string PhysBoneTypeName = "VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone";
        public const string AvatarDescriptorTypeName = "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor";

        private static Type _physBoneType;
        private static bool _typeSearched;
        private static Type _avatarDescriptorType;
        private static bool _avatarTypeSearched;

        public static Type PhysBoneType
        {
            get
            {
                if (_typeSearched)
                {
                    return _physBoneType;
                }

                _typeSearched = true;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(PhysBoneTypeName, false);
                    if (type == null)
                    {
                        continue;
                    }

                    _physBoneType = type;
                    break;
                }

                return _physBoneType;
            }
        }

        public static Type AvatarDescriptorType
        {
            get
            {
                if (_avatarTypeSearched)
                {
                    return _avatarDescriptorType;
                }

                _avatarTypeSearched = true;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(AvatarDescriptorTypeName, false);
                    if (type == null)
                    {
                        continue;
                    }

                    _avatarDescriptorType = type;
                    break;
                }

                return _avatarDescriptorType;
            }
        }

        public static bool IsAvailable
        {
            get { return PhysBoneType != null; }
        }

        /// <summary>
        /// Finds the avatar root (the object with VRCAvatarDescriptor) above the PhysBone.
        /// Falls back to the topmost parent when the descriptor is missing.
        /// </summary>
        public static Transform FindAvatarRoot(Component physBone)
        {
            return physBone != null ? FindAvatarRoot(physBone.transform) : null;
        }

        public static Transform FindAvatarRoot(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            var descriptorType = AvatarDescriptorType;
            if (descriptorType != null)
            {
                var current = transform;
                while (current != null)
                {
                    if (current.GetComponent(descriptorType) != null)
                    {
                        return current;
                    }

                    current = current.parent;
                }
            }

            var root = transform;
            while (root.parent != null)
            {
                root = root.parent;
            }

            return root;
        }

        /// <summary>
        /// Returns every VRCPhysBone under the avatar root, sorted by name.
        /// </summary>
        public static List<Component> CollectPhysBones(Transform avatarRoot)
        {
            var result = new List<Component>();
            var type = PhysBoneType;
            if (type == null || avatarRoot == null)
            {
                return result;
            }

            foreach (var component in avatarRoot.GetComponentsInChildren(type, true))
            {
                result.Add(component);
            }

            result.Sort((a, b) => string.Compare(
                a.gameObject.name,
                b.gameObject.name,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static Component GetPhysBone(GameObject gameObject)
        {
            var type = PhysBoneType;
            if (type == null || gameObject == null)
            {
                return null;
            }

            var components = gameObject.GetComponents(type);
            return components.Length > 0 ? components[0] : null;
        }

        public static Component FindPhysBoneInParents(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            var current = gameObject.transform;
            while (current != null)
            {
                var physBone = GetPhysBone(current.gameObject);
                if (physBone != null)
                {
                    return physBone;
                }

                current = current.parent;
            }

            return null;
        }

        public static Transform GetRootTransform(Component physBone)
        {
            if (physBone == null)
            {
                return null;
            }

            var root = GetFieldValue(physBone, "rootTransform") as Transform;
            return root != null ? root : physBone.transform;
        }

        public static float GetFloat(Component component, string fieldName, float fallback = 0f)
        {
            var value = GetFieldValue(component, fieldName);
            return value is float floatValue ? floatValue : fallback;
        }

        public static string GetString(Component component, string fieldName, string fallback = "")
        {
            var value = GetFieldValue(component, fieldName);
            return value as string ?? fallback;
        }

        public static bool SetBool(Component component, string fieldName, bool value)
        {
            if (component == null)
            {
                return false;
            }

            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null && field.FieldType == typeof(bool) && !field.IsInitOnly)
                {
                    field.SetValue(component, value);
                    return true;
                }
            }

            return false;
        }

        public static bool SetFieldValue(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return false;
            }

            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null && !field.IsInitOnly)
                {
                    field.SetValue(target, value);
                    return true;
                }
            }

            return false;
        }

        public static string GetEnumName(Component component, string fieldName)
        {
            var value = GetFieldValue(component, fieldName);
            return value != null ? value.ToString() : string.Empty;
        }

        public static bool SetEnumByName(Component component, string fieldName, string enumName)
        {
            if (component == null)
            {
                return false;
            }

            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null && field.FieldType.IsEnum)
                {
                    field.SetValue(component, Enum.Parse(field.FieldType, enumName));
                    return true;
                }
            }

            return false;
        }

        public static object GetFieldValue(object target, string fieldName)
        {
            if (target == null)
            {
                return null;
            }

            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field.GetValue(target);
                }
            }

            return null;
        }

        public static int GetCollectionCount(Component component, string fieldName)
        {
            var value = GetFieldValue(component, fieldName) as System.Collections.ICollection;
            return value != null ? value.Count : 0;
        }

        /// <summary>
        /// Collects every AnimationClip used by the avatar's Animators and
        /// VRC avatar descriptor playable layers.
        /// </summary>
        public static List<AnimationClip> CollectAnimationClips(Component physBone)
        {
            return CollectAnimationClips(FindAvatarRoot(physBone));
        }

        public static List<AnimationClip> CollectAnimationClips(Transform avatarRoot)
        {
            var clips = new List<AnimationClip>();
            if (avatarRoot == null)
            {
                return clips;
            }

            var controllers = new HashSet<RuntimeAnimatorController>();

            foreach (var animator in avatarRoot.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController != null)
                {
                    controllers.Add(animator.runtimeAnimatorController);
                }
            }

            var descriptorType = AvatarDescriptorType;
            if (descriptorType != null)
            {
                var descriptor = avatarRoot.GetComponent(descriptorType);
                if (descriptor != null)
                {
                    foreach (var fieldName in new[] { "baseAnimationLayers", "specialAnimationLayers" })
                    {
                        var field = descriptorType.GetField(
                            fieldName,
                            BindingFlags.Public | BindingFlags.Instance);
                        if (field == null)
                        {
                            continue;
                        }

                        var layers = field.GetValue(descriptor) as System.Collections.IEnumerable;
                        if (layers == null)
                        {
                            continue;
                        }

                        foreach (var layer in layers)
                        {
                            if (layer == null)
                            {
                                continue;
                            }

                            var controllerField = layer.GetType().GetField(
                                "animatorController",
                                BindingFlags.Public | BindingFlags.Instance);
                            var controller = controllerField != null
                                ? controllerField.GetValue(layer) as RuntimeAnimatorController
                                : null;
                            if (controller != null)
                            {
                                controllers.Add(controller);
                            }
                        }
                    }
                }
            }

            var seen = new HashSet<AnimationClip>();
            foreach (var controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                foreach (var clip in controller.animationClips)
                {
                    if (clip != null && seen.Add(clip))
                    {
                        clips.Add(clip);
                    }
                }
            }

            clips.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return clips;
        }

        /// <summary>
        /// Collects every clip available for the avatar:
        /// Animator / Avatar descriptor clips are merged with clips referenced by
        /// avatar components (VRCFury, GoGo Loco, etc.). If the avatar has none,
        /// humanoid clips shipped with installed packages are used.
        /// </summary>
        public static List<AnimationClip> CollectAllAnimationClipsWithFallback(
            Transform avatarRoot,
            out string source,
            out bool usedFallback)
        {
            var clips = new List<AnimationClip>();
            var seen = new HashSet<AnimationClip>();

            var animatorClips = CollectAnimationClips(avatarRoot);
            foreach (var clip in animatorClips)
            {
                if (clip != null && seen.Add(clip))
                {
                    clips.Add(clip);
                }
            }

            var referenceClips = CollectReferencedAnimationClips(avatarRoot);
            var addedReferences = 0;
            foreach (var clip in referenceClips)
            {
                if (clip != null && seen.Add(clip))
                {
                    clips.Add(clip);
                    addedReferences++;
                }
            }

            if (clips.Count > 0)
            {
                clips.Sort(CompareFallbackClips);

                if (animatorClips.Count > 0 && addedReferences > 0)
                {
                    source = "Animator / Avatar 描述符 + 角色组件引用（VRCFury 等）";
                    usedFallback = false;
                }
                else if (animatorClips.Count > 0)
                {
                    source = "Animator / Avatar 描述符";
                    usedFallback = false;
                }
                else
                {
                    source = "角色组件引用（VRCFury / GoGo Loco 等）";
                    usedFallback = true;
                }

                return clips;
            }

            clips = CollectPluginDefaultAnimationClips();
            source = clips.Count > 0
                ? "插件默认动画（VRChat SDK / Gesture Manager 等）"
                : "未找到";
            usedFallback = true;
            return clips;
        }

        /// <summary>
        /// Default "读取动作" scan: only walk, crouch-walk and crawl clips.
        /// If the avatar has no such clips, plugin default locomotion clips are used.
        /// </summary>
        public static List<AnimationClip> CollectLocomotionClipsWithFallback(
            Transform avatarRoot,
            out string source,
            out bool usedFallback)
        {
            var all = CollectAllAnimationClipsWithFallback(
                avatarRoot,
                out source,
                out usedFallback);

            var locomotion = AnimationClipClassifier.CollectDefaultLocomotionClips(all);
            if (locomotion.Count > 0)
            {
                return locomotion;
            }

            var defaults = AnimationClipClassifier.CollectDefaultLocomotionClips(
                CollectPluginDefaultAnimationClips());
            if (defaults.Count > 0)
            {
                source = "插件默认动画（VRChat SDK / Gesture Manager 等）";
                usedFallback = true;
                return defaults;
            }

            source = "未找到";
            usedFallback = true;
            return locomotion;
        }

        /// <summary>
        /// Scans serialized object references on every component under the avatar.
        /// This catches animations that only exist inside VRCFury / GoGo Loco data
        /// and are not assigned to the Animator yet.
        /// </summary>
        public static List<AnimationClip> CollectReferencedAnimationClips(Transform avatarRoot)
        {
            var clips = new HashSet<AnimationClip>();
            if (avatarRoot == null)
            {
                return new List<AnimationClip>();
            }

            var controllers = new HashSet<RuntimeAnimatorController>();
            foreach (var component in avatarRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                try
                {
                    var serializedObject = new SerializedObject(component);
                    var iterator = serializedObject.GetIterator();
                    var enterChildren = true;
                    while (iterator.NextVisible(enterChildren))
                    {
                        enterChildren = true;
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                        {
                            continue;
                        }

                        var value = iterator.objectReferenceValue;
                        var clip = value as AnimationClip;
                        if (clip != null)
                        {
                            clips.Add(clip);
                            continue;
                        }

                        var controller = value as RuntimeAnimatorController;
                        if (controller != null)
                        {
                            controllers.Add(controller);
                        }
                    }
                }
                catch (Exception)
                {
                    // Some third-party components can fail serialization while importing.
                    // Skip them instead of aborting the whole scan.
                }
            }

            foreach (var controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                foreach (var clip in controller.animationClips)
                {
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }
            }

            var result = new List<AnimationClip>(clips);
            result.Sort(CompareFallbackClips);
            return result;
        }

        private static List<AnimationClip> _cachedPluginDefaultClips;

        /// <summary>
        /// Humanoid clips shipped with installed packages. VRChat ships the proxy
        /// animations, Gesture Manager ships its default emote clips, and other
        /// plugins may add more. Non-humanoid clips are excluded here.
        /// </summary>
        public static List<AnimationClip> CollectPluginDefaultAnimationClips()
        {
            if (_cachedPluginDefaultClips != null)
            {
                return new List<AnimationClip>(_cachedPluginDefaultClips);
            }

            var clips = new List<AnimationClip>();
            var seen = new HashSet<AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Packages" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || !clip.isHumanMotion || !seen.Add(clip))
                {
                    continue;
                }

                clips.Add(clip);
            }

            clips.Sort(CompareFallbackClips);
            _cachedPluginDefaultClips = clips;
            return new List<AnimationClip>(clips);
        }

        private static int CompareFallbackClips(AnimationClip a, AnimationClip b)
        {
            if (a == null)
            {
                return b == null ? 0 : 1;
            }

            if (b == null)
            {
                return -1;
            }

            var humanoid = b.isHumanMotion.CompareTo(a.isHumanMotion);
            if (humanoid != 0)
            {
                return humanoid;
            }

            var moving = (b.length > 0.05f).CompareTo(a.length > 0.05f);
            if (moving != 0)
            {
                return moving;
            }

            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Estimates the bone chain length in edit mode. The bones list is only populated at runtime.
        /// </summary>
        public static int EstimateChainLength(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            var depth = 1;
            var current = root;
            while (current != null && current.childCount > 0 && depth < 256)
            {
                var next = current.GetChild(0);
                if (GetPhysBone(next.gameObject) != null)
                {
                    break;
                }

                current = next;
                depth++;
            }

            return depth;
        }
    }
}
