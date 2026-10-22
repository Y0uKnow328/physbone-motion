using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarChamber.PhysBoneMotion
{
    public class PhysBoneMotionPreviewWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Avatar Chamber/PhysBone 动态";

        private static string T(string text)
        {
            return PhysBoneLocalization.Tr(text);
        }

        private static GUIContent C(string text, string tooltip = null)
        {
            return PhysBoneLocalization.Content(text, tooltip);
        }

        private static string F(string format, params object[] args)
        {
            return PhysBoneLocalization.Format(format, args);
        }

        private static string[] TA(string[] values)
        {
            return PhysBoneLocalization.TrArray(values);
        }

        private struct PresetButton
        {
            public readonly MotionPreset Preset;
            public readonly string Label;

            public PresetButton(MotionPreset preset, string label)
            {
                Preset = preset;
                Label = label;
            }
        }

        private static readonly PresetButton[] PresetButtons =
        {
            new PresetButton(MotionPreset.Bob, "上下摆动"),
            new PresetButton(MotionPreset.Sway, "左右摇摆"),
            new PresetButton(MotionPreset.Surge, "前后晃动"),
            new PresetButton(MotionPreset.Circle, "画圈"),
            new PresetButton(MotionPreset.Jitter, "高频抖动"),
            new PresetButton(MotionPreset.Wind, "风吹"),
            new PresetButton(MotionPreset.Pulse, "脉冲回弹")
        };

        private static readonly string[] DriverOptions =
        {
            "整个模型（Avatar 根节点）",
            "动骨根节点（直接驱动，可能不自然）",
            "父级骨骼",
            "自定义 Transform"
        };

        private static readonly string[] MotionModeLabels = { "位移（平移）", "旋转" };

        private Vector2 _scrollPosition;
        [SerializeField] private Component _physBone;
        [SerializeField] private Transform _avatarRoot;
        private readonly List<Component> _avatarPhysBones = new List<Component>();
        private int _driverChoice;
        private Transform _customDriver;
        private MotionAxisMode _motionMode = MotionAxisMode.Translation;
        private float _translationAmplitude = 0.05f;
        private float _rotationAmplitude = 15f;
        private float _frequency = 1.5f;
        private float _duration = 5f;
        private double _nextRepaintTime;
        [SerializeField] private bool _showBuildBlocking = true;
        [SerializeField] private bool _showTargetInfo = true;
        [SerializeField] private bool _showDriverSettings = true;
        [SerializeField] private int _currentTab;

        private readonly List<AnimationClip> _locomotionClips = new List<AnimationClip>();
        private readonly List<AnimationClip> _detailedAnimationClips = new List<AnimationClip>();
        private readonly List<AnimationClipGroup> _animationClipGroups = new List<AnimationClipGroup>();
        private readonly List<AnimationClip> _playlist = new List<AnimationClip>();
        private Vector2 _locomotionScroll;
        private bool _locomotionScanDone;
        private bool _detailedScanDone;
        private bool _locomotionUsedFallback;
        private bool _detailedUsedFallback;
        private string _locomotionSource = string.Empty;
        private string _detailedSource = string.Empty;
        [SerializeField] private bool _showAnimationRead = true;
        [SerializeField] private bool _showDetailedScan = true;
        [SerializeField] private bool _showAutoCategories = true;
        private List<bool> _categoryFoldouts = new List<bool>();

        [MenuItem(MenuPath)]
        public static void OpenWindow()
        {
            var window = GetWindow<PhysBoneMotionPreviewWindow>(T("PhysBone 动态"));
            window.minSize = new Vector2(360f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            PhysBoneMotionDriver.StateChanged += Repaint;
            PhysBoneLocalization.LanguageChanged += OnLanguageChanged;
            titleContent = new GUIContent(T("PhysBone 动态"));
            RefreshSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            PhysBoneMotionDriver.StateChanged -= Repaint;
            PhysBoneLocalization.LanguageChanged -= OnLanguageChanged;
        }

        private void OnSelectionChanged()
        {
            RefreshSelection();
            ResetAnimationClipState();
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            RefreshSelection();
            Repaint();
        }

        private void OnLanguageChanged()
        {
            titleContent = new GUIContent(T("PhysBone 动态"));
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!PhysBoneMotionDriver.IsPlayingMotion
                && !PhysBoneMotionDriver.IsPlayingClip)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextRepaintTime)
            {
                return;
            }

            _nextRepaintTime = EditorApplication.timeSinceStartup + 0.1;
            Repaint();
        }

        private void RefreshSelection()
        {
            var active = Selection.activeGameObject;

            // Keep the last target when the Hierarchy selection is cleared or a domain reload
            // happens while nothing is selected. _physBone is serialized for this reason.
            if (active == null && _physBone != null)
            {
                if (_avatarRoot == null)
                {
                    _avatarRoot = PhysBoneReflection.FindAvatarRoot(_physBone);
                }

                if (_avatarRoot != null && _avatarPhysBones.Count == 0)
                {
                    _avatarPhysBones.AddRange(PhysBoneReflection.CollectPhysBones(_avatarRoot));
                }

                return;
            }

            _avatarRoot = active != null
                ? PhysBoneReflection.FindAvatarRoot(active.transform)
                : null;

            _avatarPhysBones.Clear();
            if (_avatarRoot != null)
            {
                _avatarPhysBones.AddRange(PhysBoneReflection.CollectPhysBones(_avatarRoot));
            }

            var selectedPhysBone = active != null
                ? PhysBoneReflection.FindPhysBoneInParents(active)
                : null;

            if (selectedPhysBone != null
                && _avatarRoot != null
                && selectedPhysBone.transform.IsChildOf(_avatarRoot))
            {
                _physBone = selectedPhysBone;
            }
            else if (_physBone != null
                     && _avatarRoot != null
                     && _physBone.transform.IsChildOf(_avatarRoot))
            {
                // Selecting a mesh or another part of the same avatar keeps the current PhysBone.
            }
            else
            {
                _physBone = _avatarPhysBones.Count > 0 ? _avatarPhysBones[0] : null;
            }
        }

        private void DrawPhysBoneSelector()
        {
            if (_avatarPhysBones.Count == 0)
            {
                EditorGUILayout.HelpBox(T("当前角色上没有 PhysBone。"), MessageType.Info);
                return;
            }

            var names = new string[_avatarPhysBones.Count];
            var currentIndex = 0;
            for (var i = 0; i < _avatarPhysBones.Count; i++)
            {
                var component = _avatarPhysBones[i];
                names[i] = component != null ? component.gameObject.name : "(null)";
                if (component == _physBone)
                {
                    currentIndex = i;
                }
            }

            var newIndex = EditorGUILayout.Popup(T("目标动骨"), currentIndex, names);
            if (newIndex != currentIndex)
            {
                _physBone = _avatarPhysBones[newIndex];
            }
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            _currentTab = GUILayout.Toolbar(
                _currentTab,
                new[] { T("预览"), T("动骨"), T("设置") },
                GUILayout.Height(24f));
            EditorGUILayout.Space(6f);

            switch (_currentTab)
            {
                case 0:
                    DrawPreviewTab();
                    break;

                case 1:
                    DrawBoneTab();
                    break;

                default:
                    DrawSettingsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBoneTab()
        {
            if (!PhysBoneReflection.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    T("未检测到 VRChat SDK（VRCPhysBone）。请先在此工程安装 VRChat Avatars SDK。"),
                    MessageType.Error);
                return;
            }

            if (_physBone == null)
            {
                EditorGUILayout.HelpBox(
                    T("当前角色没有 PhysBone，或选择不属于角色。"),
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                F("当前动骨：{0}", _physBone.gameObject.name),
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            PhysBoneSettingsGUI.DrawCompleteSettings(_physBone);
        }

        private void DrawPhysicsPreviewHeader()
        {
            EditorGUILayout.LabelField(
                C(
                    "物理预览",
                    "预览只修改 Play Mode 中的 Transform，退出 Play Mode 后自动复原，不会写入场景或预制体。"),
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                T(EditorApplication.isPlaying
                    ? "物理预览已激活"
                    : "物理预览未激活（点击动作进入运行模式）"),
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                T(EditorApplication.isPlaying ? "状态：可随时保存更改" : "状态：编辑模式"),
                EditorStyles.miniLabel);

            var showBones = EditorGUILayout.Toggle(T("显示骨骼"), PhysBonePreviewOverlay.ShowBones);
            if (showBones != PhysBonePreviewOverlay.ShowBones)
            {
                PhysBonePreviewOverlay.ShowBones = showBones;
            }

            if (GUILayout.Button(T("重新居中 Gizmo"), GUILayout.Height(22f)))
            {
                PhysBonePreviewOverlay.Recenter(_physBone);
            }

            if (GUILayout.Button(T("在鼠标下方放置 Gizmo"), GUILayout.Height(22f)))
            {
                PhysBonePreviewOverlay.PlaceAtMouse();
            }

            if (GUILayout.Button(
                C(
                    "设置放置快捷键...",
                    "默认 X：在 Scene 视图把中心移到鼠标下方。"),
                GUILayout.Height(22f)))
            {
                EditorApplication.ExecuteMenuItem("Edit/Shortcuts");
            }

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || _physBone == null))
            {
                var previousColor = GUI.backgroundColor;

                GUI.backgroundColor = new Color(0.35f, 0.7f, 0.35f);
                var saveClicked = GUILayout.Button(T("保存更改"), GUILayout.Height(24f));

                GUI.backgroundColor = new Color(0.35f, 0.65f, 0.35f);
                var saveAndExitClicked = GUILayout.Button(T("保存并退出"), GUILayout.Height(24f));

                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                var exitClicked = GUILayout.Button(T("退出预览"), GUILayout.Height(24f));

                GUI.backgroundColor = previousColor;

                if (saveClicked)
                {
                    PhysBonePlayModeSave.QueueSave(_physBone);
                }

                if (saveAndExitClicked)
                {
                    PhysBonePlayModeSave.SaveAndExit(_physBone);
                }

                if (exitClicked)
                {
                    PhysBonePlayModeSave.ExitWithoutSaving();
                }
            }

            if (PhysBonePlayModeSave.HasPendingSave)
            {
                EditorGUILayout.HelpBox(
                    T("已暂存参数，退出 Play Mode 后会写回场景。"),
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(_physBone == null))
            {
                if (GUILayout.Button(
                    C(
                        "在 Inspector 中选中当前 PhysBone",
                        "使用 Inspector 修改物理组件。"),
                    GUILayout.Height(22f)))
                {
                    Selection.activeObject = _physBone;
                    EditorGUIUtility.PingObject(_physBone);
                }
            }
        }

        private void DrawPreviewTab()
        {
            DrawPhysicsPreviewHeader();
            EditorGUILayout.Space(6f);

            if (!PhysBoneReflection.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    T("未检测到 VRChat SDK（VRCPhysBone）。请先在此工程安装 VRChat Avatars SDK。"),
                    MessageType.Error);
                return;
            }

            if (_avatarRoot == null)
            {
                EditorGUILayout.HelpBox(
                    T("请选择角色上的任意物体（骨骼、网格、Avatar 根节点都可以）。"),
                    MessageType.Info);
                return;
            }

            DrawPhysBoneSelector();

            if (_physBone != null)
            {
                EditorGUILayout.LabelField(T("动骨参数"), EditorStyles.boldLabel);
                PhysBoneSettingsGUI.DrawCompactForces(_physBone);

                var pull = PhysBoneReflection.GetFloat(_physBone, "pull");
                var spring = PhysBoneReflection.GetFloat(_physBone, "spring");
                var stiffness = PhysBoneReflection.GetFloat(_physBone, "stiffness");
                if (Mathf.Approximately(pull, 0f)
                    && Mathf.Approximately(spring, 0f)
                    && Mathf.Approximately(stiffness, 0f))
                {
                    EditorGUILayout.HelpBox(
                        T("pull / spring / stiffness 都为 0，动骨可能不会摆动。"),
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    T("该角色没有 PhysBone，预设动作不可用；仍可调整动作参数。"),
                    MessageType.Info);
            }

            EditorGUILayout.Space(6f);
            DrawMotionSettings();
            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(_physBone == null))
            {
                DrawPresetButtons();
            }

            EditorGUILayout.Space(8f);
            DrawPlaybackStatus();
        }

        private void DrawAnimationTab()
        {
            if (!PhysBoneReflection.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    "未检测到 VRChat SDK（VRCPhysBone）。请先在此工程安装 VRChat Avatars SDK。",
                    MessageType.Error);
                return;
            }

            if (_avatarRoot == null)
            {
                EditorGUILayout.HelpBox(
                    "请选择角色上的任意物体（骨骼、网格、Avatar 根节点都可以）。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                new GUIContent("动画库", "详细扫描全部动画并按类型分类。勾选多个 Humanoid 循环动画可拼接播放。"),
                EditorStyles.boldLabel);

            DrawReadAnimationSection();
            EditorGUILayout.Space(4f);
            DrawDetailedScanSection();
            EditorGUILayout.Space(4f);
            DrawAutoCategoriesSection();
            EditorGUILayout.Space(6f);
            DrawPlaybackStatus();
        }

        private void DrawReadAnimationSection()
        {
            _showAnimationRead = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showAnimationRead,
                new GUIContent(
                    "读取动作",
                    "只读取可循环的行走 / 蹲走 / 爬行动画。"
                    + "每行右侧 ▶ 立即循环播放；左侧勾选可拼接播放。"
                    + "\n注意：VRChat SDK 的 proxy walk / crouch / crawl 可能是 0 秒静止姿势，"
                    + "不会产生连续行走；GoLoco 的 crawl / prone 有短动作。"));
            if (!_showAnimationRead)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;
            DrawLocomotionToolbar();

            if (!_locomotionScanDone)
            {
                EditorGUILayout.LabelField(
                    new GUIContent("点击「读取动作」扫描。", "默认只显示行走 / 蹲走 / 爬行循环动作。"),
                    EditorStyles.miniLabel);
            }
            else if (_locomotionClips.Count == 0)
            {
                EditorGUILayout.LabelField(
                    new GUIContent("没有找到循环动作。", "可以到「动画」标签使用详细扫描查看全部动画。"),
                    EditorStyles.miniLabel);
            }
            else
            {
                DrawClipSourceCompact(_locomotionSource, _locomotionUsedFallback);
                DrawClipList(_locomotionClips, ref _locomotionScroll, 220f, true);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawLocomotionToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(
                new GUIContent("读取动作", "重新扫描循环的行走 / 蹲走 / 爬行动画。"),
                EditorStyles.miniButton,
                GUILayout.Width(74f),
                GUILayout.Height(18f)))
            {
                ScanLocomotionClips();
            }

            DrawPlaylistButtons();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDetailedScanSection()
        {
            _showDetailedScan = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showDetailedScan,
                new GUIContent(
                    "详细扫描",
                    "读取全部动画并按类型自动分类。"
                    + "来源：Animator / Avatar 描述符、VRCFury / GoGo Loco 等组件引用、插件默认动画。"));
            if (!_showDetailedScan)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;
            DrawDetailedToolbar();

            if (!_detailedScanDone)
            {
                EditorGUILayout.LabelField(
                    new GUIContent("点击「详细扫描」读取全部动画。", "结果会自动分类到下方「自动分类」。"),
                    EditorStyles.miniLabel);
            }
            else if (_detailedAnimationClips.Count == 0)
            {
                EditorGUILayout.LabelField(
                    new GUIContent("没有找到任何动画。", "请确认角色有 Animator、VRCFury 组件引用或已安装插件默认动画。"),
                    EditorStyles.miniLabel);
            }
            else
            {
                DrawClipSourceCompact(_detailedSource, _detailedUsedFallback);
                EditorGUILayout.LabelField(
                    new GUIContent("共 " + _detailedAnimationClips.Count + " 个动画", "已自动分类到下方。"),
                    EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawDetailedToolbar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(
                new GUIContent("详细扫描", "读取全部动画：Animator / Avatar 描述符、VRCFury 等组件引用、插件默认动画。"),
                EditorStyles.miniButton,
                GUILayout.Width(74f),
                GUILayout.Height(18f)))
            {
                ScanDetailedClips();
            }

            DrawPlaylistButtons();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlaylistButtons()
        {
            var count = CountPlayablePlaylistClips();
            using (new EditorGUI.DisabledScope(count == 0))
            {
                if (GUILayout.Button(
                    new GUIContent(
                        "播放勾选 (" + count + ")",
                        "按勾选顺序循环拼接播放。只允许 Humanoid 且不含缩放曲线的循环动画。"),
                    EditorStyles.miniButton,
                    GUILayout.Height(18f)))
                {
                    PlayCheckedClips();
                }
            }

            if (GUILayout.Button(
                new GUIContent("清空", "清空勾选列表。"),
                EditorStyles.miniButton,
                GUILayout.Width(44f),
                GUILayout.Height(18f)))
            {
                _playlist.Clear();
            }
        }

        private void DrawAutoCategoriesSection()
        {
            _showAutoCategories = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showAutoCategories,
                new GUIContent("自动分类", "按动画名称自动归类。点击分类标题展开或收起。"));
            if (!_showAutoCategories)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;
            if (!_detailedScanDone)
            {
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "点击「详细扫描」后显示分类。",
                        "分类包括行走 / 蹲走 / 爬行 / 跳跃 / 飞行 / 坐姿 / 待机 / 表情 / 手势 / 其他。"),
                    EditorStyles.miniLabel);
            }
            else if (_animationClipGroups.Count == 0)
            {
                EditorGUILayout.LabelField(
                    new GUIContent("没有可分类的动画。", "详细扫描没有找到动画。"),
                    EditorStyles.miniLabel);
            }
            else
            {
                EnsureCategoryFoldoutSize();
                foreach (var group in _animationClipGroups)
                {
                    var index = (int)group.Category;
                    if (index < 0 || index >= _categoryFoldouts.Count)
                    {
                        continue;
                    }

                    var label = new GUIContent(
                        group.Name + " (" + group.Clips.Count + ")",
                        GetCategoryTooltip(group.Category));
                    _categoryFoldouts[index] = EditorGUILayout.Foldout(
                        _categoryFoldouts[index],
                        label,
                        true);
                    if (!_categoryFoldouts[index])
                    {
                        continue;
                    }

                    EditorGUI.indentLevel++;
                    foreach (var clip in group.Clips)
                    {
                        DrawClipRow(clip, false);
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static string GetCategoryTooltip(AnimationClipCategory category)
        {
            switch (category)
            {
                case AnimationClipCategory.Locomotion:
                    return "行走 / 移动：walk、run、sprint、strafe、shuffle、dash 等。";
                case AnimationClipCategory.CrouchWalk:
                    return "蹲走：蹲伏移动。";
                case AnimationClipCategory.Crawl:
                    return "爬行：crawl、low crawl、prone 等。";
                case AnimationClipCategory.Jump:
                    return "跳跃、下落、落地。";
                case AnimationClipCategory.Fly:
                    return "飞行、悬浮、游泳。";
                case AnimationClipCategory.Sit:
                    return "坐下、跪姿、躺卧、睡觉。";
                case AnimationClipCategory.Idle:
                    return "待机、站立、AFK、T-Pose / IK Pose。";
                case AnimationClipCategory.Emote:
                    return "表情、舞蹈、挥手、鼓掌等。";
                case AnimationClipCategory.Gesture:
                    return "手势、拳头、张开手、ASL 等。";
                default:
                    return "校准、平滑、空动画、开关等系统动画。";
            }
        }

        private static void DrawClipSourceCompact(string source, bool usedFallback)
        {
            if (string.IsNullOrEmpty(source))
            {
                return;
            }

            var shortSource = source;
            if (source.Contains("插件默认"))
            {
                shortSource = "插件默认动画";
            }
            else if (source.Contains("角色组件引用") && source.Contains("Animator"))
            {
                shortSource = "Animator + 组件引用";
            }
            else if (source.Contains("角色组件引用"))
            {
                shortSource = "角色组件引用";
            }
            else if (source.Contains("Animator"))
            {
                shortSource = "Animator / Avatar 描述符";
            }

            var tooltip = usedFallback
                ? "Animator / Avatar 描述符里没有对应动画，已自动回退到该来源。"
                : "动画来源。";
            EditorGUILayout.LabelField(
                new GUIContent("来源：" + shortSource, tooltip),
                EditorStyles.miniLabel);
        }

        private void DrawClipList(
            IList<AnimationClip> clips,
            ref Vector2 scroll,
            float maxHeight,
            bool forceLoop)
        {
            if (clips == null || clips.Count == 0)
            {
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(maxHeight));
            for (var i = 0; i < clips.Count; i++)
            {
                DrawClipRow(clips[i], forceLoop);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawClipRow(AnimationClip clip, bool forceLoop)
        {
            if (clip == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();

            string reason;
            var canAdd = AnimationClipClassifier.CanUseInPlaylist(clip, out reason);
            var isChecked = _playlist.Contains(clip);
            using (new EditorGUI.DisabledScope(!canAdd))
            {
                var newChecked = GUILayout.Toggle(
                    isChecked,
                    new GUIContent(string.Empty, canAdd ? "勾选后加入拼接播放" : reason),
                    GUILayout.Width(18f));
                if (newChecked != isChecked)
                {
                    if (newChecked)
                    {
                        if (!_playlist.Contains(clip))
                        {
                            _playlist.Add(clip);
                        }
                    }
                    else
                    {
                        _playlist.Remove(clip);
                    }
                }
            }

            string playReason;
            var canPlay = AnimationClipClassifier.IsSafeStandalone(clip, out playReason);
            var displayName = clip.name;
            if (clip.length <= 0.01f)
            {
                displayName += "  · 静止";
            }

            if (!canPlay)
            {
                displayName += "  · 受保护";
            }

            GUILayout.Label(
                new GUIContent(displayName, AnimationClipClassifier.GetClipTooltip(clip)),
                EditorStyles.miniLabel,
                GUILayout.ExpandWidth(true));

            using (new EditorGUI.DisabledScope(!canPlay))
            {
                if (GUILayout.Button(
                    new GUIContent(
                        "▶",
                        canPlay
                            ? (forceLoop
                                ? "立即循环播放这个动画"
                                : "立即播放这个动画（可循环动画会循环）")
                            : playReason),
                    EditorStyles.miniButton,
                    GUILayout.Width(24f),
                    GUILayout.Height(18f)))
                {
                    PlaySingleClip(clip, forceLoop);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void PlaySingleClip(AnimationClip clip, bool forceLoop)
        {
            if (clip == null || _avatarRoot == null)
            {
                return;
            }

            string reason;
            if (!AnimationClipClassifier.IsSafeStandalone(clip, out reason))
            {
                ShowNotification(new GUIContent("该动画受保护：" + reason));
                return;
            }

            PhysBoneMotionDriver.BeginClipPreview(
                _avatarRoot,
                clip,
                forceLoop || AnimationClipClassifier.IsLoopCapable(clip));
        }

        private int CountPlayablePlaylistClips()
        {
            var count = 0;
            foreach (var clip in _playlist)
            {
                string reason;
                if (AnimationClipClassifier.CanUseInPlaylist(clip, out reason))
                {
                    count++;
                }
            }

            return count;
        }

        private void PlayCheckedClips()
        {
            if (_avatarRoot == null)
            {
                return;
            }

            var clips = new List<AnimationClip>();
            foreach (var clip in _playlist)
            {
                string reason;
                if (AnimationClipClassifier.CanUseInPlaylist(clip, out reason))
                {
                    clips.Add(clip);
                }
            }

            if (clips.Count == 0)
            {
                ShowNotification(new GUIContent("请先勾选 Humanoid 循环动画。"));
                return;
            }

            PhysBoneMotionDriver.BeginClipPlaylistPreview(_avatarRoot, clips, true);
        }

        private void ScanLocomotionClips()
        {
            _locomotionClips.Clear();
            _locomotionClips.AddRange(PhysBoneReflection.CollectLocomotionClipsWithFallback(
                _avatarRoot,
                out _locomotionSource,
                out _locomotionUsedFallback));
            _locomotionScanDone = true;
            Repaint();
        }

        private void ScanDetailedClips()
        {
            _detailedAnimationClips.Clear();
            _detailedAnimationClips.AddRange(PhysBoneReflection.CollectAllAnimationClipsWithFallback(
                _avatarRoot,
                out _detailedSource,
                out _detailedUsedFallback));
            _detailedScanDone = true;

            _animationClipGroups.Clear();
            _animationClipGroups.AddRange(AnimationClipClassifier.Categorize(_detailedAnimationClips));

            _locomotionClips.Clear();
            _locomotionClips.AddRange(AnimationClipClassifier.CollectDefaultLocomotionClips(_detailedAnimationClips));
            _locomotionScanDone = true;
            _locomotionSource = _detailedSource;
            _locomotionUsedFallback = _detailedUsedFallback;

            EnsureCategoryFoldoutSize();
            for (var i = 0; i < _categoryFoldouts.Count; i++)
            {
                var category = (AnimationClipCategory)i;
                _categoryFoldouts[i] = category == AnimationClipCategory.Locomotion
                                       || category == AnimationClipCategory.CrouchWalk
                                       || category == AnimationClipCategory.Crawl;
            }

            Repaint();
        }

        private void EnsureCategoryFoldoutSize()
        {
            if (_categoryFoldouts == null)
            {
                _categoryFoldouts = new List<bool>();
            }

            var count = Enum.GetValues(typeof(AnimationClipCategory)).Length;
            while (_categoryFoldouts.Count < count)
            {
                _categoryFoldouts.Add(false);
            }

            while (_categoryFoldouts.Count > count)
            {
                _categoryFoldouts.RemoveAt(_categoryFoldouts.Count - 1);
            }
        }

        private void ResetAnimationClipState()
        {
            _locomotionClips.Clear();
            _detailedAnimationClips.Clear();
            _animationClipGroups.Clear();
            _playlist.Clear();
            _locomotionScanDone = false;
            _detailedScanDone = false;
            _locomotionUsedFallback = false;
            _detailedUsedFallback = false;
            _locomotionSource = string.Empty;
            _detailedSource = string.Empty;
        }

        private void DrawLanguageSettings()
        {
            EditorGUILayout.LabelField(T("语言 / Language"), EditorStyles.boldLabel);
            var current = (int)PhysBoneLocalization.Language;
            var selected = EditorGUILayout.Popup(
                C("界面语言", "语言设置会保存在本机，切换后立即生效。"),
                current,
                PhysBoneLocalization.LanguageNames);
            if (selected != current)
            {
                PhysBoneLocalization.Language = (PreviewLanguage)selected;
                titleContent = new GUIContent(T("PhysBone 动态"));
                Repaint();
            }
        }

        private void DrawSettingsTab()
        {
            DrawLanguageSettings();
            EditorGUILayout.Space(8f);
            DrawBuildBlockingSettings();
            EditorGUILayout.Space(8f);

            if (!PhysBoneReflection.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    T("未检测到 VRChat SDK（VRCPhysBone）。请先在此工程安装 VRChat Avatars SDK。"),
                    MessageType.Error);
                return;
            }

            if (_physBone == null)
            {
                EditorGUILayout.HelpBox(
                    T("当前角色没有 PhysBone，无法显示目标动骨和驱动对象设置。"),
                    MessageType.Info);
                return;
            }

            DrawTargetInfo();
            EditorGUILayout.Space(6f);
            DrawDriverSettings();
        }

        private void DrawTargetInfo()
        {
            _showTargetInfo = EditorGUILayout.BeginFoldoutHeaderGroup(_showTargetInfo, T("目标动骨"));
            if (_showTargetInfo)
            {
                EditorGUI.indentLevel++;

                var physBoneType = PhysBoneReflection.PhysBoneType;
                var selected = (Component)EditorGUILayout.ObjectField(
                    C("PhysBone", "当前预览的 VRCPhysBone，可直接拖入或从对象选择器切换。"),
                    _physBone,
                    physBoneType ?? typeof(Component),
                    true);
                if (selected != null && selected != _physBone)
                {
                    if ((physBoneType != null && !physBoneType.IsInstanceOfType(selected))
                        || !selected.gameObject.scene.IsValid()
                        || PhysBoneReflection.FindAvatarRoot(selected) == null)
                    {
                        ShowNotification(C("请选择场景中、属于角色的 VRCPhysBone。"));
                    }
                    else
                    {
                        SetTargetPhysBone(selected);
                    }
                }

                var path = GetHierarchyPath(_physBone.transform);
                EditorGUILayout.LabelField(
                    C("层级路径", "根据当前 PhysBone 自动生成，不能直接编辑；可以选中复制。"),
                    EditorStyles.miniLabel);
                var pathStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                var pathHeight = Mathf.Max(
                    EditorGUIUtility.singleLineHeight,
                    pathStyle.CalcHeight(
                        new GUIContent(path),
                        Mathf.Max(80f, EditorGUIUtility.currentViewWidth - 140f)));
                EditorGUILayout.SelectableLabel(path, pathStyle, GUILayout.Height(pathHeight));

                var root = PhysBoneReflection.GetRootTransform(_physBone);
                var newRoot = (Transform)EditorGUILayout.ObjectField(
                    C("根骨骼", "这条 PhysBone 链条的起点；留空时默认使用组件所在节点。修改后会改变链长和受控骨骼。"),
                    root,
                    typeof(Transform),
                    true);
                if (newRoot != root)
                {
                    var avatarRoot = PhysBoneReflection.FindAvatarRoot(_physBone);
                    var isValidRoot = newRoot == null
                        || (newRoot.gameObject.scene.IsValid()
                            && avatarRoot != null
                            && (newRoot == avatarRoot || newRoot.IsChildOf(avatarRoot)));
                    if (isValidRoot)
                    {
                        SetRootTransform(newRoot);
                    }
                    else
                    {
                        ShowNotification(C("根骨骼必须是当前角色层级内的场景对象。"));
                    }
                }

                var chainRoot = PhysBoneReflection.GetRootTransform(_physBone);
                var chainLength = PhysBoneReflection.EstimateChainLength(chainRoot);
                var nextPhysBone = FindNextPhysBoneInChain(chainRoot);
                var chainText = F("{0} 节", chainLength);
                if (nextPhysBone != null)
                {
                    chainText += F("（到 {0} 为止，它有自己的 PhysBone）", nextPhysBone.gameObject.name);
                }

                DrawStat(T("链长（估算）"), chainText);

                var behaviour = _physBone as Behaviour;
                if (behaviour != null && !behaviour.enabled)
                {
                    EditorGUILayout.HelpBox(
                        T("该 PhysBone 组件处于禁用状态，预览不会产生效果。"),
                        MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void SetTargetPhysBone(Component physBone)
        {
            if (physBone == null || physBone == _physBone)
            {
                return;
            }

            _physBone = physBone;
            _avatarRoot = PhysBoneReflection.FindAvatarRoot(physBone);
            _avatarPhysBones.Clear();
            if (_avatarRoot != null)
            {
                _avatarPhysBones.AddRange(PhysBoneReflection.CollectPhysBones(_avatarRoot));
            }

            if (_customDriver != null
                && (_avatarRoot == null
                    || (_customDriver != _avatarRoot && !_customDriver.IsChildOf(_avatarRoot))))
            {
                _customDriver = null;
            }

            ResetAnimationClipState();
            Repaint();
        }

        private void SetRootTransform(Transform newRoot)
        {
            if (_physBone == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(_physBone);
            serializedObject.Update();
            var property = serializedObject.FindProperty("rootTransform");
            if (property == null)
            {
                return;
            }

            Undo.RecordObject(_physBone, T("修改 PhysBone 根骨骼"));
            property.objectReferenceValue = newRoot;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_physBone);
            Repaint();
        }

        private static Component FindNextPhysBoneInChain(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var current = root;
            while (current != null && current.childCount > 0)
            {
                var next = current.GetChild(0);
                var physBone = PhysBoneReflection.GetPhysBone(next.gameObject);
                if (physBone != null)
                {
                    return physBone;
                }

                current = next;
            }

            return null;
        }

        private void DrawDriverSettings()
        {
            _showDriverSettings = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showDriverSettings,
                C(
                    "驱动对象",
                    "选择预览时移动的对象。推荐整个模型；直接驱动动骨根节点可能不自然。"));
            if (_showDriverSettings)
            {
                EditorGUI.indentLevel++;

                var driverTooltip = _driverChoice == 0
                    ? "推荐：移动整个 Avatar，PhysBone 会像手动移动主物体一样自然解算，限位也会生效。"
                    : _driverChoice == 1
                        ? "直接驱动动骨根节点可能产生不自然的抽搐，限位表现也与整体移动不同。"
                        : _driverChoice == 2
                            ? "驱动动骨根节点的父级骨骼，适合头发、耳朵这类挂在头部的链条。"
                            : "驱动一个自定义 Transform。";
                _driverChoice = EditorGUILayout.Popup(
                    C("驱动", driverTooltip),
                    _driverChoice,
                    TA(DriverOptions));

                if (_driverChoice == 3)
                {
                    _customDriver = (Transform)EditorGUILayout.ObjectField(
                        T("自定义"),
                        _customDriver,
                        typeof(Transform),
                        true);
                }

                var driver = GetDriver();
                if (driver != null)
                {
                    EditorGUILayout.HelpBox(F("当前驱动：{0}", GetHierarchyPath(driver)), MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox(T("当前没有可用的驱动对象。"), MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMotionSettings()
        {
            EditorGUILayout.LabelField(T("动作参数"), EditorStyles.boldLabel);

            var previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 110f;

            _motionMode = (MotionAxisMode)EditorGUILayout.Popup(
                C("运动方式", "位移：整体平移；旋转：整体转动"),
                (int)_motionMode,
                TA(MotionModeLabels));

            if (_motionMode == MotionAxisMode.Translation)
            {
                _translationAmplitude = EditorGUILayout.Slider(
                    C("幅度（米）", "整体移动的距离"),
                    _translationAmplitude,
                    0.001f,
                    0.5f);
            }
            else
            {
                _rotationAmplitude = EditorGUILayout.Slider(
                    C("幅度（角度）", "整体旋转的角度"),
                    _rotationAmplitude,
                    1f,
                    90f);
            }

            _frequency = EditorGUILayout.Slider(
                C("频率（Hz）", "每秒摆动次数"),
                _frequency,
                0.1f,
                10f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(
                C("时长（秒）", "0 表示循环播放，直到手动停止"),
                GUILayout.Width(110f));
            _duration = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(_duration, GUILayout.Width(60f)));
            GUILayout.Label(T("0 = 循环播放"), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private void DrawBuildBlockingSettings()
        {
            _showBuildBlocking = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showBuildBlocking,
                C(
                    "构建阻止",
                    "勾选后阻止对应工具在 Play Mode 处理 Avatar；"
                    + "设置立即生效并保持，取消勾选即恢复。"));
            if (_showBuildBlocking)
            {
                EditorGUI.indentLevel++;

                var vrcfuryAvailable = PhysBoneMotionDriver.VrcfuryAvailable;
                var ndmfAvailable = PhysBoneMotionDriver.NdmfAvailable;

                using (new EditorGUI.DisabledScope(!vrcfuryAvailable))
                {
                    var block = EditorGUILayout.Toggle(
                        C(
                            "阻止 VRCFury 构建",
                            "阻止 VRCFury 在 Play Mode 处理 Avatar。"),
                        PhysBoneMotionDriver.BlockVrcfury);
                    if (block != PhysBoneMotionDriver.BlockVrcfury)
                    {
                        PhysBoneMotionDriver.BlockVrcfury = block;
                    }
                }

                if (!vrcfuryAvailable)
                {
                    EditorGUILayout.HelpBox(T("未检测到 VRCFury。"), MessageType.None);
                }

                using (new EditorGUI.DisabledScope(!ndmfAvailable))
                {
                    var block = EditorGUILayout.Toggle(
                        C(
                            "阻止 MA / NDMF 构建",
                            "阻止 Modular Avatar / NDMF 在 Play Mode 处理 Avatar。"),
                        PhysBoneMotionDriver.BlockNdmf);
                    if (block != PhysBoneMotionDriver.BlockNdmf)
                    {
                        PhysBoneMotionDriver.BlockNdmf = block;
                    }
                }

                if (!ndmfAvailable)
                {
                    EditorGUILayout.HelpBox(T("未检测到 NDMF / Modular Avatar。"), MessageType.None);
                }

                if (EditorApplication.isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        T("当前已在 Play Mode，修改将在下次进入 Play Mode 时生效。"),
                        MessageType.None);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPresetButtons()
        {
            EditorGUILayout.LabelField(T("预设动作"), EditorStyles.boldLabel);

            const int columns = 3;
            for (var i = 0; i < PresetButtons.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (var j = 0; j < columns; j++)
                {
                    var index = i + j;
                    if (index < PresetButtons.Length)
                    {
                        DrawPresetButton(PresetButtons[index]);
                    }
                    else
                    {
                        GUILayout.FlexibleSpace();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawPresetButton(PresetButton button)
        {
            if (GUILayout.Button(T(button.Label), GUILayout.Height(24f)))
            {
                StartPreset(button.Preset);
            }
        }

        private void StartPreset(MotionPreset preset)
        {
            var driver = GetDriver();
            if (_physBone == null || driver == null)
            {
                Debug.LogWarning("[PhysBone Motion] " + T("没有可预览的动骨或驱动对象。"));
                return;
            }

            var settings = new MotionSettings
            {
                Preset = preset,
                Mode = _motionMode,
                Amplitude = _motionMode == MotionAxisMode.Translation
                    ? _translationAmplitude
                    : _rotationAmplitude,
                Frequency = _frequency,
                Duration = _duration
            };

            PhysBoneMotionDriver.Begin(_physBone, driver, settings);
        }

        private Transform GetDriver()
        {
            var root = PhysBoneReflection.GetRootTransform(_physBone);
            if (root == null)
            {
                return null;
            }

            switch (_driverChoice)
            {
                case 1:
                    return root;

                case 2:
                    return root.parent != null ? root.parent : root;

                case 3:
                    return _customDriver;

                default:
                    return PhysBoneReflection.FindAvatarRoot(_physBone);
            }
        }

        private void DrawPlaybackStatus()
        {
            EditorGUILayout.LabelField(T("播放状态"), EditorStyles.boldLabel);

            if (PhysBoneMotionDriver.IsPlayingClip)
            {
                var text = PhysBoneMotionDriver.IsPlayingPlaylist
                    ? F("播放序列 {0}/{1}：{2}",
                        PhysBoneMotionDriver.ActiveClipIndex + 1,
                        PhysBoneMotionDriver.ActiveClipCount,
                        PhysBoneMotionDriver.ActiveClipName)
                    : F("播放动画：{0}", PhysBoneMotionDriver.ActiveClipName);
                EditorGUILayout.HelpBox(text, MessageType.Info);
                if (GUILayout.Button(T("停止并复原"), GUILayout.Height(28f)))
                {
                    PhysBoneMotionDriver.Stop();
                }
            }
            else if (PhysBoneMotionDriver.IsPlayingMotion)
            {
                var text = F("播放中：{0}", GetPresetLabel(PhysBoneMotionDriver.ActivePreset));
                var remaining = PhysBoneMotionDriver.RemainingSeconds;
                if (remaining >= 0f)
                {
                    text += F("（剩余 {0} 秒）", remaining.ToString("0.0"));
                }

                EditorGUILayout.HelpBox(text, MessageType.Info);
                if (GUILayout.Button(T("停止并复原"), GUILayout.Height(28f)))
                {
                    PhysBoneMotionDriver.Stop();
                }
            }
            else if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    T("当前在 Play Mode 中，选择一个预设动作开始预览。"),
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    T("当前是编辑模式；点击任意预设动作会自动进入 Play Mode。"),
                    MessageType.None);
            }
        }

        private static void DrawStat(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(110f));
            GUILayout.Label(value, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static string GetPresetLabel(MotionPreset preset)
        {
            foreach (var button in PresetButtons)
            {
                if (button.Preset == preset)
                {
                    return T(button.Label);
                }
            }

            return preset.ToString();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            var current = transform;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
