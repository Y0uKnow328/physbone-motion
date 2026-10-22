using System;
using UnityEditor;
using UnityEngine;

namespace AvatarChamber.PhysBoneMotion
{
    /// <summary>
    /// Complete Chinese editor for VRCPhysBone, plus a compact force strip for the preview tab.
    /// </summary>
    public static class PhysBoneSettingsGUI
    {
        private static readonly string[] AdvancedBoolLabels = { "关闭", "开启", "其他（高级）" };
        private static readonly string[] AdvancedBoolValues = { "False", "True", "Other" };
        private static readonly string[] LimitTypeLabels = { "无", "角度", "铰链", "极坐标" };
        private static readonly string[] LimitTypeValues = { "None", "Angle", "Hinge", "Polar" };
        private static readonly string[] ImmobileTypeLabels = { "所有运动", "世界坐标" };
        private static readonly string[] ImmobileTypeValues = { "AllMotion", "World" };
        private static readonly string[] MultiChildTypeLabels = { "忽略", "第一个", "平均" };
        private static readonly string[] MultiChildTypeValues = { "Ignore", "First", "Average" };
        private static readonly string[] IntegrationTypeLabels = { "简化", "高级" };
        private static readonly string[] IntegrationTypeValues = { "Simplified", "Advanced" };

        private static bool _showTransforms = true;
        private static bool _showForces = true;
        private static bool _showCollision;
        private static bool _showStretch;
        private static bool _showLimits = true;
        private static bool _showGrab;
        private static bool _showOptions;

        /// <summary>
        /// Full VRCPhysBone editor. Uses SerializedProperty so lists, curves, references and Undo all work.
        /// </summary>
        public static void DrawCompleteSettings(Component physBone)
        {
            if (physBone == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(physBone);
            serializedObject.Update();

            var version = serializedObject.FindProperty("version");
                if (version != null)
                {
                    EditorGUILayout.LabelField(
                        PhysBoneLocalization.Content("版本（Version）", "PhysBone 配置版本"),
                        new GUIContent(version.enumNames[version.enumValueIndex]));
                }

                DrawTransforms(serializedObject);
                DrawForces(serializedObject);
                DrawCollision(serializedObject);
                DrawStretch(serializedObject);
                DrawLimits(serializedObject);
                DrawGrab(serializedObject);
            DrawOptions(serializedObject);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawTransforms(SerializedObject so)
        {
            _showTransforms = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showTransforms,
                PhysBoneLocalization.Tr("骨骼与变换 / Transforms"));
            if (_showTransforms)
            {
                EditorGUI.indentLevel++;
                DrawProp(so, "rootTransform", "根骨骼（Root Transform）", "动骨的根骨骼，默认是组件所在节点。");
                DrawProp(so, "ignoreTransforms", "忽略的骨骼（Ignore Transforms）", "这些骨骼及其子级不会被 PhysBone 控制。");
                DrawProp(so, "endpointPosition", "末端位置（Endpoint Position）", "在链末端额外延伸的虚拟端点位置。");
                DrawEnumProp(
                    so,
                    "multiChildType",
                    "多子骨骼模式（Multi Child Type）",
                    "根骨骼有多个子级时使用哪一个。",
                    MultiChildTypeLabels,
                    MultiChildTypeValues);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawForces(SerializedObject so)
        {
            _showForces = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showForces,
                PhysBoneLocalization.Tr("受力 / Forces"));
            if (_showForces)
            {
                EditorGUI.indentLevel++;
                DrawEnumProp(
                    so,
                    "integrationType",
                    "集成方式（Integration Type）",
                    "简化：普通骨骼链；高级：允许更复杂的末端和子级处理。",
                    IntegrationTypeLabels,
                    IntegrationTypeValues);
                DrawFloatWithCurve(so, "pull", "pull 回拉力（Pull）", "越大，骨骼越快回到静止姿态。", "pullCurve");
                DrawFloatWithCurve(so, "spring", "spring 弹性（Spring）", "越大，摆动越有弹性，回弹越明显。", "springCurve");
                DrawFloatWithCurve(so, "stiffness", "stiffness 硬度（Stiffness）", "越大，骨骼越硬，越不容易弯曲。", "stiffnessCurve");
                DrawFloatWithCurve(so, "gravity", "gravity 重力（Gravity）", "越大，骨骼越容易被向下拉。", "gravityCurve");
                DrawFloatWithCurve(so, "gravityFalloff", "重力衰减（Gravity Falloff）", "沿骨骼链向下减弱重力的程度。", "gravityFalloffCurve");
                DrawFloatWithCurve(so, "immobile", "immobile 固定度（Immobile）", "越大，骨骼越不容易跟随整体移动。", "immobileCurve");
                DrawEnumProp(
                    so,
                    "immobileType",
                    "固定方式（Immobile Type）",
                    "固定度作用在哪些运动上。",
                    ImmobileTypeLabels,
                    ImmobileTypeValues);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawCollision(SerializedObject so)
        {
            _showCollision = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showCollision,
                PhysBoneLocalization.Tr("碰撞 / Collision"));
            if (_showCollision)
            {
                EditorGUI.indentLevel++;
                DrawEnumProp(
                    so,
                    "allowCollision",
                    "允许碰撞（Allow Collision）",
                    "是否允许动骨与碰撞体发生碰撞。",
                    AdvancedBoolLabels,
                    AdvancedBoolValues);
                DrawPermissionFilter(
                    so.FindProperty("collisionFilter"),
                    "碰撞权限（Collision Filter）",
                    "设置谁能触发碰撞，以及哪些内容类型参与碰撞。");
                DrawFloatWithCurve(so, "radius", "碰撞半径（Radius）", "动骨碰撞体的粗细。", "radiusCurve");
                DrawProp(so, "colliders", "碰撞体列表（Colliders）", "参与碰撞的 VRCPhysBoneCollider 列表。");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawStretch(SerializedObject so)
        {
            _showStretch = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showStretch,
                PhysBoneLocalization.Tr("拉伸与压缩 / Stretch & Squish"));
            if (_showStretch)
            {
                EditorGUI.indentLevel++;
                DrawFloatWithCurve(so, "maxStretch", "最大拉伸（Max Stretch）", "允许骨骼链被拉长的最大比例。", "maxStretchCurve");
                DrawFloatWithCurve(so, "maxSquish", "最大压缩（Max Squish）", "允许骨骼链被压缩的最大比例。", "maxSquishCurve");
                DrawFloatWithCurve(so, "stretchMotion", "拉伸运动（Stretch Motion）", "拉伸时产生的额外运动强度。", "stretchMotionCurve");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawLimits(SerializedObject so)
        {
            _showLimits = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showLimits,
                PhysBoneLocalization.Tr("限位 / Limits"));
            if (_showLimits)
            {
                EditorGUI.indentLevel++;
                DrawEnumProp(
                    so,
                    "limitType",
                    "限位方式（Limit Type）",
                    "无：不限制；角度：限制 X/Z 摆动；铰链/极坐标：高级限位。",
                    LimitTypeLabels,
                    LimitTypeValues);
                DrawFloatWithCurve(so, "maxAngleX", "最大 X 角度（Max Angle X）", "绕 X 轴允许的最大摆动角度。", "maxAngleXCurve");
                DrawFloatWithCurve(so, "maxAngleZ", "最大 Z 角度（Max Angle Z）", "绕 Z 轴允许的最大摆动角度。", "maxAngleZCurve");
                DrawProp(so, "limitRotation", "旋转限位（Limit Rotation）", "旋转限位的中心角度。", false);
                DrawProp(so, "limitRotationXCurve", "旋转限位 X 曲线", "绕 X 轴旋转限位的曲线。", false);
                DrawProp(so, "limitRotationYCurve", "旋转限位 Y 曲线", "绕 Y 轴旋转限位的曲线。", false);
                DrawProp(so, "limitRotationZCurve", "旋转限位 Z 曲线", "绕 Z 轴旋转限位的曲线。", false);
                DrawProp(so, "staticFreezeAxis", "静止冻结轴（Static Freeze Axis）", "冻结指定轴向的静止运动。", false);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawGrab(SerializedObject so)
        {
            _showGrab = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showGrab,
                PhysBoneLocalization.Tr("抓取与姿势 / Grab & Pose"));
            if (_showGrab)
            {
                EditorGUI.indentLevel++;
                DrawEnumProp(
                    so,
                    "allowGrabbing",
                    "允许抓取（Allow Grabbing）",
                    "是否允许玩家抓取这条动骨。",
                    AdvancedBoolLabels,
                    AdvancedBoolValues);
                DrawPermissionFilter(
                    so.FindProperty("grabFilter"),
                    "抓取权限（Grab Filter）",
                    "设置谁能抓取，以及哪些内容类型可以抓取。");
                DrawEnumProp(
                    so,
                    "allowPosing",
                    "允许摆姿势（Allow Posing）",
                    "是否允许玩家在菜单里摆弄这条动骨。",
                    AdvancedBoolLabels,
                    AdvancedBoolValues);
                DrawPermissionFilter(
                    so.FindProperty("poseFilter"),
                    "摆姿势权限（Pose Filter）",
                    "设置谁能摆姿势，以及哪些内容类型可以摆姿势。");
                DrawProp(so, "snapToHand", "吸附到手（Snap To Hand）", "抓取时是否直接吸附到手的位置。", false);
                DrawProp(so, "grabMovement", "抓取移动（Grab Movement）", "抓取时骨骼跟随手部移动的比例。", false);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawOptions(SerializedObject so)
        {
            _showOptions = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showOptions,
                PhysBoneLocalization.Tr("选项 / Options"));
            if (_showOptions)
            {
                EditorGUI.indentLevel++;
                DrawProp(so, "isAnimated", "根骨骼被动画驱动（Is Animated）", "动画会驱动根骨骼时勾选；直接驱动根节点预览时插件会临时开启。", false);
                DrawProp(so, "resetWhenDisabled", "禁用时重置（Reset When Disabled）", "组件禁用时是否让动骨回到初始姿态。", false);
                DrawProp(so, "parameter", "控制参数名（Parameter）", "读取 Angle / Stretch / Squish 等参数时使用的名字。", false);
                DrawProp(so, "showGizmos", "显示 Gizmo（Show Gizmos）", "在 Scene 视图显示动骨辅助线。", false);
                DrawProp(so, "boneOpacity", "骨骼透明度（Bone Opacity）", "Gizmo 中骨骼辅助线的透明度。", false);
                DrawProp(so, "limitOpacity", "限位透明度（Limit Opacity）", "Gizmo 中限位范围的透明度。", false);
                DrawProp(so, "ignoreOtherPhysBones", "忽略其他 PhysBone（Ignore Other PhysBones）", "是否忽略其他 PhysBone 对本链条的影响。", false);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawFloatWithCurve(
            SerializedObject so,
            string valueField,
            string label,
            string tooltip,
            string curveField)
        {
            DrawProp(so, valueField, label, tooltip, false);
            DrawProp(so, curveField, "曲线（Curve）", "数值沿骨骼链的衰减曲线。", false);
        }

        private static void DrawProp(
            SerializedObject so,
            string fieldName,
            string label,
            string tooltip,
            bool includeChildren = true)
        {
            var property = so.FindProperty(fieldName);
            if (property == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(
                property,
                PhysBoneLocalization.Content(label, tooltip),
                includeChildren);
        }

        private static void DrawEnumProp(
            SerializedObject so,
            string fieldName,
            string label,
            string tooltip,
            string[] displayNames,
            string[] valueNames)
        {
            var property = so.FindProperty(fieldName);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
            {
                return;
            }

            var currentIndex = Array.IndexOf(valueNames, property.enumNames[property.enumValueIndex]);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var selectedIndex = EditorGUILayout.Popup(
                PhysBoneLocalization.Content(label, tooltip),
                currentIndex,
                PhysBoneLocalization.TrArray(displayNames));
            if (selectedIndex != currentIndex)
            {
                var targetIndex = Array.IndexOf(property.enumNames, valueNames[selectedIndex]);
                if (targetIndex >= 0)
                {
                    property.enumValueIndex = targetIndex;
                }
            }
        }

        private static void DrawPermissionFilter(
            SerializedProperty parent,
            string label,
            string tooltip)
        {
            if (parent == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                PhysBoneLocalization.Content(label, tooltip),
                EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var allowSelf = parent.FindPropertyRelative("allowSelf");
            var allowOthers = parent.FindPropertyRelative("allowOthers");
            var contentTypes = parent.FindPropertyRelative("contentTypes");

            if (allowSelf != null)
            {
                EditorGUILayout.PropertyField(
                    allowSelf,
                    new GUIContent(PhysBoneLocalization.Tr("允许自己（Allow Self）")));
            }

            if (allowOthers != null)
            {
                EditorGUILayout.PropertyField(
                    allowOthers,
                    new GUIContent(PhysBoneLocalization.Tr("允许他人（Allow Others）")));
            }

            if (contentTypes != null)
            {
                EditorGUILayout.PropertyField(
                    contentTypes,
                    new GUIContent(PhysBoneLocalization.Tr("内容类型（Content Types）")));
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Compact editable force sliders for the preview tab.
        /// Explanations are shown as hover tooltips instead of extra lines.
        /// </summary>
        public static void DrawCompactForces(Component physBone)
        {
            if (physBone == null)
            {
                return;
            }

            DrawCompactFloat(physBone, "pull", "pull 回拉力", "回正速度：越大，骨骼越快回到静止姿态。", 0f, 1f);
            DrawCompactFloat(physBone, "spring", "spring 弹性", "回弹强度：越大，摆动越有弹性，回弹越明显。", 0f, 1f);
            DrawCompactFloat(physBone, "stiffness", "stiffness 硬度", "抗弯曲：越大，骨骼越硬，越不容易弯曲。", 0f, 1f);
            DrawCompactFloat(physBone, "gravity", "gravity 重力", "下垂力：越大，骨骼越容易被向下拉。", 0f, 1f);
            DrawCompactFloat(physBone, "immobile", "immobile 固定度", "抗整体移动：越大，骨骼越不容易跟随整体移动。", 0f, 1f);
        }

        private static void DrawCompactFloat(
            Component component,
            string fieldName,
            string label,
            string tooltip,
            float min,
            float max)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                PhysBoneLocalization.Content(label, tooltip),
                GUILayout.Width(105f));
            var value = PhysBoneReflection.GetFloat(component, fieldName);
            var newValue = GUILayout.HorizontalSlider(
                value,
                min,
                max,
                GUILayout.ExpandWidth(true));
            GUILayout.Label(newValue.ToString("0.00"), GUILayout.Width(38f));
            EditorGUILayout.EndHorizontal();

            if (!Mathf.Approximately(value, newValue))
            {
                Undo.RecordObject(component, PhysBoneLocalization.Tr("修改动骨设置"));
                PhysBoneReflection.SetFieldValue(component, fieldName, newValue);
                EditorUtility.SetDirty(component);
            }
        }
    }
}
