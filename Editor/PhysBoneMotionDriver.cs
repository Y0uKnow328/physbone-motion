using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace AvatarChamber.PhysBoneMotionPreview
{
    /// <summary>
    /// Plays preset motions on a transform while in Play Mode, so VRCPhysBone can simulate.
    /// The pending request survives the domain reload caused by entering Play Mode via SessionState.
    /// </summary>
    [InitializeOnLoad]
    public static class PhysBoneMotionDriver
    {
        private const string Prefix = "AvatarChamber.PhysBoneMotionPreview.";
        private const string PendingKey = Prefix + "Pending";
        private const string PendingTimeKey = Prefix + "PendingTime";
        private const string SceneKey = Prefix + "Scene";
        private const string TargetKey = Prefix + "Target";
        private const string DriverSceneKey = Prefix + "DriverScene";
        private const string DriverKey = Prefix + "Driver";
        private const string PresetKey = Prefix + "Preset";
        private const string ModeKey = Prefix + "Mode";
        private const string AmplitudeKey = Prefix + "Amplitude";
        private const string FrequencyKey = Prefix + "Frequency";
        private const string DurationKey = Prefix + "Duration";

        private const string ClipPendingKey = Prefix + "ClipPending";
        private const string ClipPendingTimeKey = Prefix + "ClipPendingTime";
        private const string ClipSceneKey = Prefix + "ClipScene";
        private const string ClipAvatarKey = Prefix + "ClipAvatar";
        private const string ClipPathKey = Prefix + "ClipPath";
        private const string ClipNameKey = Prefix + "ClipName";
        private const string ClipLoopKey = Prefix + "ClipLoop";
        private const string ClipPlaylistKey = Prefix + "ClipPlaylist";
        private const string ClipPlaylistLoopKey = Prefix + "ClipPlaylistLoop";

        private const string SimPendingKey = Prefix + "SimPending";
        private const string SimPendingTimeKey = Prefix + "SimPendingTime";
        private const string SimSceneKey = Prefix + "SimScene";
        private const string SimAvatarKey = Prefix + "SimAvatar";
        private const string SimFollowKey = Prefix + "SimFollow";
        private const string SimSpeedKey = Prefix + "SimSpeed";

        private const string BlockFuryPrefKey = "AvatarChamber.PhysBoneMotionPreview.BlockVrcfury";
        private const string BlockNdmfPrefKey = "AvatarChamber.PhysBoneMotionPreview.BlockNdmf";
        private const string VrcfuryPlayModePref = "com.vrcfury.playMode";

        // Old preview-scoped restore markers, cleaned up on load.
        private const string OldFuryRestorePendingPref = "AvatarChamber.PhysBoneMotionPreview.FuryRestorePending";
        private const string OldFuryRestoreValuePref = "AvatarChamber.PhysBoneMotionPreview.FuryRestoreValue";

        private const float PendingTimeoutSeconds = 120f;

        private static bool _active;
        private static Component _physBone;
        private static bool _originalIsAnimated;
        private static bool _isAnimatedOverridden;
        private static bool _pendingReEnable;
        private static double _reEnableTime;
        private static int _disableFrame;
        private static Transform _driver;
        private static Vector3 _initialLocalPosition;
        private static Quaternion _initialLocalRotation;
        private static MotionPreset _preset;
        private static MotionAxisMode _mode;
        private static float _amplitude;
        private static float _frequency;
        private static float _duration;
        private static double _startTime;

        private static bool _playingClip;
        private static AnimationClip _clip;
        private static Transform _clipAvatarRoot;
        private static PlayableGraph _clipGraph;
        private static AnimationClipPlayable _clipPlayable;
        private static bool _clipLoop;
        private static double _clipStartTime;
        private static List<TransformPose> _clipPoseSnapshot;
        private static List<AnimationClip> _clipPlaylist;
        private static int _clipPlaylistIndex;
        private static bool _clipPlaylistLoop;
        private static AnimationPlayableOutput _clipOutput;
        private static float _clipPlaylistDuration;

        private static bool _simulating;
        private static Transform _simAvatarRoot;
        private static Animator _simAnimator;
        private static RuntimeAnimatorController _simOriginalController;
        private static Avatar _simOriginalAvatar;
        private static bool _simAvatarRebuilt;
        private static bool _simOriginalApplyRootMotion;
        private static Vector3 _simOriginalPosition;
        private static Quaternion _simOriginalRotation;
        private static Vector3 _simVelocity;
        private static Vector3 _simInitialForward;
        private static Vector3 _simInitialRight;
        private static bool _simFollowCamera;
        private static float _simSpeedMultiplier = 1f;
        private static bool _simCrouch;
        private static bool _simProne;
        private static int _simLocomotionLayer = -1;
        private static string _simCurrentState = string.Empty;
        private static HashSet<string> _simParameters;
        private static List<TransformPose> _simPoseSnapshot;
        private static Camera _simCamera;
        private static Vector3 _simOriginalCameraPosition;
        private static Quaternion _simOriginalCameraRotation;
        private static Vector3 _simOriginalScenePivot;
        private static bool _simScenePivotCaptured;
        private static float _simCurrentSpeed;
        private static bool _simExternalInput;
        private static float _simExternalX;
        private static float _simExternalZ;
        private static bool _simExternalRun;
        private static double _simExternalInputTime;

        // Procedural gait adds a looping limb swing on top of the static GoLoco
        // proxy poses. VRChat fills the same gap with client-side IK; the editor
        // needs this overlay to show a complete walk cycle.
        private static bool _simGaitActive;
        private static int _simGaitFrame = -1;
        private static float _simGaitPhase;
        private static Transform _simGaitLeftUpperLeg;
        private static Transform _simGaitRightUpperLeg;
        private static Transform _simGaitLeftLowerLeg;
        private static Transform _simGaitRightLowerLeg;
        private static Transform _simGaitLeftUpperArm;
        private static Transform _simGaitRightUpperArm;

        private struct TransformPose
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        public static event Action StateChanged;

        public static bool IsPlayingMotion
        {
            get { return _active && !_playingClip && !_simulating; }
        }

        public static bool IsPlayingClip
        {
            get { return _playingClip; }
        }

        public static string ActiveClipName
        {
            get { return _clip != null ? _clip.name : string.Empty; }
        }

        public static bool IsPlayingPlaylist
        {
            get { return _playingClip && _clipPlaylist != null && _clipPlaylist.Count > 1; }
        }

        public static int ActiveClipIndex
        {
            get { return _clipPlaylist != null ? _clipPlaylistIndex : 0; }
        }

        public static int ActiveClipCount
        {
            get { return _clipPlaylist != null ? _clipPlaylist.Count : 0; }
        }

        public static bool IsSimulating
        {
            get { return _simulating; }
        }

        public static string SimulationMode
        {
            get
            {
                if (!_simulating)
                {
                    return string.Empty;
                }

                if (_simProne)
                {
                    return "爬行";
                }

                if (_simCrouch)
                {
                    return "蹲走";
                }

                return _simVelocity.sqrMagnitude > 0.01f ? "行走" : "待机";
            }
        }

        public static float SimulationSpeed
        {
            get { return _simCurrentSpeed; }
        }

        public static string SimulationControllerName
        {
            get
            {
                return _simAnimator != null && _simAnimator.runtimeAnimatorController != null
                    ? _simAnimator.runtimeAnimatorController.name
                    : "无";
            }
        }

        /// <summary>
        /// Input bridge for the Scene view. Unity's Input class does not receive
        /// keyboard events while the Scene view is focused, so the Scene overlay
        /// forwards its held keys here. Game view input still uses Input directly.
        /// </summary>
        public static void SetSimulationExternalInput(float x, float z, bool run)
        {
            _simExternalInput = true;
            _simExternalX = Mathf.Clamp(x, -1f, 1f);
            _simExternalZ = Mathf.Clamp(z, -1f, 1f);
            _simExternalRun = run;
            _simExternalInputTime = EditorApplication.timeSinceStartup;
        }

        public static void ClearSimulationExternalInput()
        {
            _simExternalInput = false;
            _simExternalX = 0f;
            _simExternalZ = 0f;
            _simExternalRun = false;
            _simExternalInputTime = 0d;
        }

        public static void ToggleSimulationCrouch()
        {
            if (!_simulating)
            {
                return;
            }

            _simCrouch = !_simCrouch;
            if (_simCrouch)
            {
                _simProne = false;
            }

            ApplySimulationPostureParameters();
            PlaySimulationState(true);
        }

        public static void ToggleSimulationProne()
        {
            if (!_simulating)
            {
                return;
            }

            _simProne = !_simProne;
            if (_simProne)
            {
                _simCrouch = false;
            }

            ApplySimulationPostureParameters();
            PlaySimulationState(true);
        }

        public static MotionPreset ActivePreset
        {
            get { return _preset; }
        }

        public static float ElapsedSeconds
        {
            get { return _active ? (float)(EditorApplication.timeSinceStartup - _startTime) : 0f; }
        }

        public static float RemainingSeconds
        {
            get { return _active && _duration > 0f ? Mathf.Max(0f, _duration - ElapsedSeconds) : -1f; }
        }

        public static bool VrcfuryAvailable
        {
            get { return IsVrcfuryAvailable(); }
        }

        public static bool NdmfAvailable
        {
            get { return GetNdmfApplyOnPlayProperty() != null; }
        }

        /// <summary>
        /// Persistent preference: prevent VRCFury from processing the avatar on Play.
        /// Changing it applies immediately and stays in effect.
        /// </summary>
        public static bool BlockVrcfury
        {
            get { return EditorPrefs.GetBool(BlockFuryPrefKey, true); }
            set
            {
                EditorPrefs.SetBool(BlockFuryPrefKey, value);
                ApplyBuildBlocking();
            }
        }

        /// <summary>
        /// Persistent preference: prevent NDMF / Modular Avatar from processing the avatar on Play.
        /// Changing it applies immediately and stays in effect.
        /// </summary>
        public static bool BlockNdmf
        {
            get { return EditorPrefs.GetBool(BlockNdmfPrefKey, true); }
            set
            {
                EditorPrefs.SetBool(BlockNdmfPrefKey, value);
                ApplyBuildBlocking();
            }
        }

        static PhysBoneMotionDriver()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Keep the update hook registered for the whole editor session.
            // Adding it during the play-mode transition can be lost with the update invocation list.
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            // Remove markers left by the old preview-scoped design.
            EditorPrefs.DeleteKey(OldFuryRestorePendingPref);
            EditorPrefs.DeleteKey(OldFuryRestoreValuePref);
            SessionState.EraseBool(Prefix + "BlockFury");
            SessionState.EraseBool(Prefix + "BlockNdmf");
            SessionState.EraseBool(Prefix + "FuryChanged");
            SessionState.EraseBool(Prefix + "FuryOriginal");
            SessionState.EraseBool(Prefix + "NdmfChanged");
            SessionState.EraseBool(Prefix + "NdmfOriginal");

            // The simulation tab is temporarily removed. Clear any stale request so a
            // normal Play Mode entry can never start a simulation by accident.
            SessionState.EraseBool(SimPendingKey);
            SessionState.EraseFloat(SimPendingTimeKey);

            ApplyBuildBlocking();
        }

        public static void Begin(Component physBone, Transform driver, MotionSettings settings)
        {
            if (physBone == null || settings == null)
            {
                return;
            }

            var targetDriver = driver != null ? driver : PhysBoneReflection.GetRootTransform(physBone);
            if (targetDriver == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] " + PhysBoneLocalization.Tr("找不到驱动对象。"));
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                SavePending(physBone, targetDriver, settings);
                EditorApplication.isPlaying = true;
                StateChanged?.Invoke();
                return;
            }

            StartInternal(physBone, targetDriver, settings);
        }

        public static void Stop()
        {
            StopInternal(true);
        }

        /// <summary>
        /// Plays an AnimationClip on the avatar in Play Mode.
        /// The clip is resolved by asset path after the domain reload.
        /// </summary>
        public static void BeginClipPreview(Component physBone, AnimationClip clip, bool loop)
        {
            BeginClipPreview(PhysBoneReflection.FindAvatarRoot(physBone), clip, loop);
        }

        public static void BeginClipPreview(Transform avatarRoot, AnimationClip clip, bool loop)
        {
            if (avatarRoot == null || clip == null)
            {
                return;
            }

            string unsafeReason;
            if (!AnimationClipClassifier.IsSafeStandalone(clip, out unsafeReason))
            {
                Debug.LogWarning("[PhysBone Motion Preview] 已阻止可能畸形的动画："
                                 + clip.name + " / " + unsafeReason);
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                SaveClipPending(avatarRoot, clip, loop);
                EditorApplication.isPlaying = true;
                StateChanged?.Invoke();
                return;
            }

            StartClipPlayback(avatarRoot, clip, loop);
        }

        /// <summary>
        /// Plays several clips in sequence, looping the whole sequence.
        /// Only Humanoid, loop-capable clips without scale curves are accepted;
        /// this avoids the mesh stretching that a mixed playlist can cause.
        /// </summary>
        public static void BeginClipPlaylistPreview(
            Transform avatarRoot,
            IList<AnimationClip> clips,
            bool loop)
        {
            if (avatarRoot == null || clips == null || clips.Count == 0)
            {
                return;
            }

            var playlist = new List<AnimationClip>();
            foreach (var clip in clips)
            {
                string reason;
                if (AnimationClipClassifier.CanUseInPlaylist(clip, out reason))
                {
                    playlist.Add(clip);
                }
                else
                {
                    Debug.LogWarning("[PhysBone Motion Preview] 跳过不适合拼接播放的动画："
                                     + (clip != null ? clip.name : "null") + " / " + reason);
                }
            }

            if (playlist.Count == 0)
            {
                Debug.LogWarning("[PhysBone Motion Preview] 没有可拼接播放的 Humanoid 循环动画。");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                SaveClipPlaylistPending(avatarRoot, playlist, loop);
                EditorApplication.isPlaying = true;
                StateChanged?.Invoke();
                return;
            }

            StartClipPlaylistPlayback(avatarRoot, playlist, loop);
        }

        /// <summary>
        /// Starts an in-place locomotion simulation. The avatar is moved through the
        /// world to give VRCPhysBone real inertia, while the Scene/Game camera follows
        /// so the character appears to stay in place. WASD moves, Shift runs,
        /// Z toggles crouch and X toggles prone/crawl.
        /// </summary>
        public static void BeginSimulation(
            Transform avatarRoot,
            bool followCamera,
            float speedMultiplier)
        {
            if (avatarRoot == null)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                var scene = avatarRoot.gameObject.scene;
                SessionState.SetString(SimSceneKey, scene.path);
                SessionState.SetString(SimAvatarKey, BuildSiblingPath(avatarRoot));
                SessionState.SetBool(SimFollowKey, followCamera);
                SessionState.SetFloat(SimSpeedKey, speedMultiplier);
                SessionState.SetFloat(SimPendingTimeKey, (float)EditorApplication.timeSinceStartup);
                SessionState.SetBool(SimPendingKey, true);
                EditorApplication.isPlaying = true;
                StateChanged?.Invoke();
                return;
            }

            StartSimulation(avatarRoot, followCamera, speedMultiplier);
        }

        public static void StopSimulation()
        {
            if (_simulating)
            {
                StopInternal(true);
            }

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static void TryStartSimPending()
        {
            SessionState.SetBool(SimPendingKey, false);

            var pendingAge = (float)EditorApplication.timeSinceStartup
                             - SessionState.GetFloat(SimPendingTimeKey, 0f);
            if (pendingAge > PendingTimeoutSeconds)
            {
                return;
            }

            var scene = GetScene(SessionState.GetString(SimSceneKey, string.Empty));
            var avatarRoot = ResolveSiblingPath(
                scene,
                SessionState.GetString(SimAvatarKey, string.Empty));
            if (avatarRoot == null)
            {
                Debug.LogWarning("[PhysBone Motion Preview] 模拟启动时找不到角色根节点。");
                return;
            }

            StartSimulation(
                avatarRoot,
                SessionState.GetBool(SimFollowKey, true),
                SessionState.GetFloat(SimSpeedKey, 1f));
        }

        private static void StartSimulation(
            Transform avatarRoot,
            bool followCamera,
            float speedMultiplier)
        {
            if (avatarRoot == null)
            {
                return;
            }

            StopInternal(false);
            ClearSimulationExternalInput();

            _simAvatarRoot = avatarRoot;
            _simOriginalPosition = avatarRoot.position;
            _simOriginalRotation = avatarRoot.rotation;
            _simInitialForward = avatarRoot.forward;
            _simInitialRight = avatarRoot.right;
            _simVelocity = Vector3.zero;
            _simFollowCamera = followCamera;
            _simSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.25f, 3f);
            _simCrouch = false;
            _simProne = false;
            _simCurrentSpeed = 0f;
            _simCurrentState = string.Empty;
            _simPoseSnapshot = CapturePose(avatarRoot);

            _simAnimator = avatarRoot.GetComponent<Animator>();
            if (_simAnimator == null)
            {
                _simAnimator = avatarRoot.GetComponentInChildren<Animator>(true);
            }

            if (_simAnimator != null)
            {
                _simAnimator.enabled = true;
                _simOriginalApplyRootMotion = _simAnimator.applyRootMotion;
                _simAnimator.applyRootMotion = false;
                _simOriginalController = _simAnimator.runtimeAnimatorController;
                _simOriginalAvatar = _simAnimator.avatar;
                RebuildSimulationAvatar(_simAnimator);

                if (_simOriginalController == null)
                {
                    var goLoco = FindController("GoLocoBaseWD");
                    if (goLoco != null)
                    {
                        _simAnimator.runtimeAnimatorController = goLoco;
                        _simAnimator.Rebind();
                        _simAnimator.Update(0f);
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[PhysBone Motion Preview] 找不到 GoLocoBaseWD，"
                            + "模拟只会移动角色，不播放游戏动画。");
                    }
                }

                _simParameters = new HashSet<string>();
                foreach (var parameter in _simAnimator.parameters)
                {
                    _simParameters.Add(parameter.name);
                }

                _simLocomotionLayer = FindLayer(_simAnimator, "Locomotion");
                if (_simLocomotionLayer < 0 && _simAnimator.layerCount > 0)
                {
                    _simLocomotionLayer = 0;
                }

                SetSimBool("IsLocal", true);
                SetSimBool("Grounded", true);
                SetSimBool("Go/Stationary", true);
                SetSimFloat("Go/Weight", 1f);
                SetSimFloat("Go/StandType", 0f);
                SetSimFloat("Go/CrouchType", 0f);
                SetSimFloat("Go/ProneType", 0f);
                SetSimFloat("VelocityX", 0f);
                SetSimFloat("VelocityZ", 0f);
                SetSimFloat("VelocityMagnitude", 0f);
                ApplySimulationPostureParameters();

                PlaySimulationState(true);
                StartSimulationGait();
            }

            _simCamera = Camera.main;
            if (_simCamera != null)
            {
                if (_simCamera.transform.IsChildOf(avatarRoot))
                {
                    _simCamera = null;
                }
                else
                {
                    _simOriginalCameraPosition = _simCamera.transform.position;
                    _simOriginalCameraRotation = _simCamera.transform.rotation;
                }
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                _simOriginalScenePivot = sceneView.pivot;
                _simScenePivotCaptured = true;
            }

            _simulating = true;
            _active = true;
            StateChanged?.Invoke();
        }

        private static RuntimeAnimatorController FindController(string controllerName)
        {
            var guids = AssetDatabase.FindAssets(controllerName + " t:AnimatorController");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                if (controller != null && controller.name == controllerName)
                {
                    return controller;
                }
            }

            return null;
        }

        /// <summary>
        /// Some avatar assets store a humanoid rest pose that no longer matches the scene
        /// skeleton (typical after the model was rescaled). The Animator then retargets
        /// humanoid positions with a wrong humanScale and stretches the mesh as soon as a
        /// controller plays. Rebuild a temporary Avatar from the current rest pose so the
        /// simulation uses the same scale as the scene.
        /// </summary>
        private static void RebuildSimulationAvatar(Animator animator)
        {
            if (animator == null
                || animator.avatar == null
                || !animator.avatar.isHuman
                || !animator.avatar.isValid)
            {
                return;
            }

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
            {
                return;
            }

            var hadController = animator.runtimeAnimatorController != null;
            if (hadController)
            {
                animator.runtimeAnimatorController = null;
                animator.Update(0.001f);
            }

            var restLocal = hips.localPosition;

            if (hadController)
            {
                animator.runtimeAnimatorController = _simOriginalController;
                animator.Update(0.001f);
            }

            var description = animator.avatar.humanDescription;
            var hipsIndex = -1;
            for (var i = 0; i < description.skeleton.Length; i++)
            {
                if (description.skeleton[i].name == hips.name)
                {
                    hipsIndex = i;
                    break;
                }
            }

            if (hipsIndex < 0)
            {
                return;
            }

            var descriptionLocal = description.skeleton[hipsIndex].position;
            if (descriptionLocal.sqrMagnitude < 0.0000001f)
            {
                return;
            }

            var ratio = restLocal.magnitude / descriptionLocal.magnitude;
            if (ratio > 0.95f && ratio < 1.05f)
            {
                return;
            }

            for (var i = 0; i < description.skeleton.Length; i++)
            {
                var bone = description.skeleton[i];
                bone.position *= ratio;
                description.skeleton[i] = bone;
            }

            var rebuilt = AvatarBuilder.BuildHumanAvatar(animator.gameObject, description);
            if (rebuilt == null || !rebuilt.isValid || !rebuilt.isHuman)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] 角色 Avatar 与当前骨骼比例不一致，"
                    + "自动重建失败，播放动画时可能出现拉伸。");
                return;
            }

            animator.avatar = rebuilt;
            _simAvatarRebuilt = true;
            Debug.Log(
                "[PhysBone Motion Preview] 已按场景骨骼比例重建临时 Avatar（"
                + ratio.ToString("0.000") + "），避免模拟动画拉伸。");
        }

        private static int FindLayer(Animator animator, string layerName)
        {
            for (var i = 0; i < animator.layerCount; i++)
            {
                if (animator.GetLayerName(i) == layerName)
                {
                    return i;
                }
            }

            return -1;
        }

        private static void SetSimBool(string name, bool value)
        {
            if (_simAnimator != null
                && _simParameters != null
                && _simParameters.Contains(name))
            {
                _simAnimator.SetBool(name, value);
            }
        }

        private static void SetSimFloat(string name, float value)
        {
            if (_simAnimator != null
                && _simParameters != null
                && _simParameters.Contains(name))
            {
                _simAnimator.SetFloat(name, value);
            }
        }

        private static void SetSimInt(string name, int value)
        {
            if (_simAnimator == null
                || _simParameters == null
                || !_simParameters.Contains(name))
            {
                return;
            }

            foreach (var parameter in _simAnimator.parameters)
            {
                if (parameter.name == name
                    && parameter.type == AnimatorControllerParameterType.Int)
                {
                    _simAnimator.SetInteger(name, value);
                    return;
                }
            }
        }

        /// <summary>
        /// GoLoco decides stand / crouch / prone from Upright and Go/UprightMobile,
        /// not only from the Go/Crouch and Go/Prone booleans. UprightMobile defaults
        /// to true, which blocks the crouch and prone transitions until it is cleared.
        /// </summary>
        private static void ApplySimulationPostureParameters()
        {
            SetSimFloat("Upright", _simProne ? 0.30f : _simCrouch ? 0.55f : 1f);
            SetSimBool("Go/UprightMobile", false);
            SetSimBool("Go/Crouch", _simCrouch);
            SetSimBool("Go/Prone", _simProne);
            SetSimFloat("VRModeProxy", 0f);
            SetSimInt("VRMode", 0);
            SetSimInt("TrackingType", 0);
        }

        private static void StartSimulationGait()
        {
            StopSimulationGait();
            if (_simAnimator == null)
            {
                return;
            }

            _simGaitLeftUpperLeg = _simAnimator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _simGaitRightUpperLeg = _simAnimator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _simGaitLeftLowerLeg = _simAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _simGaitRightLowerLeg = _simAnimator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _simGaitLeftUpperArm = _simAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _simGaitRightUpperArm = _simAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);

            _simGaitActive = _simGaitLeftUpperLeg != null
                             && _simGaitRightUpperLeg != null
                             && _simGaitLeftLowerLeg != null
                             && _simGaitRightLowerLeg != null;
            if (!_simGaitActive)
            {
                return;
            }

            _simGaitPhase = 0f;
            _simGaitFrame = -1;
            Camera.onPreCull -= OnSimulationPreCull;
            Camera.onPreCull += OnSimulationPreCull;
        }

        private static void StopSimulationGait()
        {
            Camera.onPreCull -= OnSimulationPreCull;
            _simGaitActive = false;
            _simGaitFrame = -1;
            _simGaitPhase = 0f;
            _simGaitLeftUpperLeg = null;
            _simGaitRightUpperLeg = null;
            _simGaitLeftLowerLeg = null;
            _simGaitRightLowerLeg = null;
            _simGaitLeftUpperArm = null;
            _simGaitRightUpperArm = null;
        }

        private static void OnSimulationPreCull(Camera camera)
        {
            if (!_simulating || !_simGaitActive || _simCurrentSpeed < 0.05f)
            {
                return;
            }

            if (_simGaitFrame == Time.frameCount)
            {
                return;
            }

            _simGaitFrame = Time.frameCount;

            var amplitude = Mathf.Clamp01(_simCurrentSpeed / 1.5f);
            if (_simCrouch)
            {
                amplitude *= 0.65f;
            }
            if (_simProne)
            {
                amplitude *= 0.35f;
            }
            if (amplitude < 0.01f)
            {
                return;
            }

            var axis = _simInitialRight;
            var legSwing = Mathf.Sin(_simGaitPhase) * 24f * amplitude;
            var armSwing = -legSwing * 0.75f;
            var leftKnee = -Mathf.Max(0f, -Mathf.Sin(_simGaitPhase)) * 32f * amplitude;
            var rightKnee = -Mathf.Max(0f, Mathf.Sin(_simGaitPhase)) * 32f * amplitude;

            RotateSimulationBone(_simGaitLeftUpperLeg, axis, legSwing);
            RotateSimulationBone(_simGaitRightUpperLeg, axis, -legSwing);
            RotateSimulationBone(_simGaitLeftLowerLeg, axis, leftKnee);
            RotateSimulationBone(_simGaitRightLowerLeg, axis, rightKnee);
            RotateSimulationBone(_simGaitLeftUpperArm, axis, armSwing);
            RotateSimulationBone(_simGaitRightUpperArm, axis, -armSwing);
        }

        private static void RotateSimulationBone(Transform bone, Vector3 axis, float angle)
        {
            if (bone != null && Mathf.Abs(angle) > 0.001f)
            {
                bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
            }
        }

        private static void PlaySimulationState(bool force)
        {
            if (_simAnimator == null || _simLocomotionLayer < 0)
            {
                return;
            }

            var desired = _simProne ? "Proning Motion"
                : _simCrouch ? "Crouch Motion"
                : "Stand Motion";
            var hash = Animator.StringToHash(desired);

            if (!_simAnimator.HasState(_simLocomotionLayer, hash))
            {
                desired = "Stand Motion";
                hash = Animator.StringToHash(desired);
                if (!_simAnimator.HasState(_simLocomotionLayer, hash))
                {
                    return;
                }
            }

            var current = _simAnimator.GetCurrentAnimatorStateInfo(_simLocomotionLayer);
            if (force
                || (!current.IsName(desired)
                    && !_simAnimator.IsInTransition(_simLocomotionLayer)))
            {
                _simAnimator.Play(hash, _simLocomotionLayer, 0f);
                _simCurrentState = desired;
            }
        }

        private static void TickSimulation()
        {
            if (!EditorApplication.isPlaying || _simAvatarRoot == null)
            {
                StopInternal(true);
                return;
            }

            // Scene view focus is unreliable during EditorApplication.update, so use a
            // short lease instead. The overlay refreshes it on every Scene view repaint.
            if (_simExternalInput
                && EditorApplication.timeSinceStartup - _simExternalInputTime > 0.35d)
            {
                ClearSimulationExternalInput();
            }

            if (_simExternalInput && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Repaint();
            }

            float inputX;
            float inputZ;
            bool running;

            if (_simExternalInput)
            {
                inputX = _simExternalX;
                inputZ = _simExternalZ;
                running = _simExternalRun;
            }
            else
            {
                inputX = (Input.GetKey(KeyCode.D) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                inputZ = (Input.GetKey(KeyCode.W) ? 1f : 0f)
                         - (Input.GetKey(KeyCode.S) ? 1f : 0f);

                if (Input.GetKeyDown(KeyCode.Z))
                {
                    ToggleSimulationCrouch();
                }

                if (Input.GetKeyDown(KeyCode.X))
                {
                    ToggleSimulationProne();
                }

                running = Input.GetKey(KeyCode.LeftShift)
                          || Input.GetKey(KeyCode.RightShift);
            }

            ProcessSimulationInput(inputX, inputZ, running, Time.deltaTime);
        }

        private static void ProcessSimulationInput(
            float inputX,
            float inputZ,
            bool running,
            float deltaTime)
        {
            if (_simAvatarRoot == null)
            {
                return;
            }

            deltaTime = Mathf.Max(0.0001f, deltaTime);

            var direction = _simInitialRight * inputX + _simInitialForward * inputZ;
            var hasInput = direction.sqrMagnitude > 0.0001f;
            if (hasInput)
            {
                direction.Normalize();
            }

            var baseSpeed = _simProne ? 0.5f
                : _simCrouch ? 1.0f
                : running ? 4.0f
                : 1.5f;
            var targetSpeed = hasInput ? baseSpeed * _simSpeedMultiplier : 0f;
            var targetVelocity = direction * targetSpeed;
            var acceleration = hasInput ? 8f : 10f;
            _simVelocity = Vector3.MoveTowards(
                _simVelocity,
                targetVelocity,
                acceleration * deltaTime);

            if (_simVelocity.sqrMagnitude < 0.0001f)
            {
                _simVelocity = Vector3.zero;
            }

            var previousPosition = _simAvatarRoot.position;
            _simAvatarRoot.position += _simVelocity * deltaTime;
            FollowSimulationCamera(_simAvatarRoot.position - previousPosition);

            var velocityX = Vector3.Dot(_simVelocity, _simInitialRight);
            var velocityZ = Vector3.Dot(_simVelocity, _simInitialForward);
            var speed = _simVelocity.magnitude;
            _simCurrentSpeed = speed;

            SetSimFloat("VelocityX", velocityX);
            SetSimFloat("VelocityZ", velocityZ);
            SetSimFloat("VelocityMagnitude", speed);
            SetSimBool("Go/Stationary", speed < 0.05f);
            SetSimBool("Go/Crouch", _simCrouch);
            SetSimBool("Go/Prone", _simProne);

            if (_simGaitActive && speed > 0.05f)
            {
                var gaitRate = _simProne ? 2.4f : _simCrouch ? 3.4f : 4.2f;
                _simGaitPhase += deltaTime
                                 * gaitRate
                                 * Mathf.Clamp01(speed / 1.5f + 0.15f);
            }
        }

        private static void FollowSimulationCamera(Vector3 delta)
        {
            if (!_simFollowCamera || delta.sqrMagnitude < 0.0000001f)
            {
                return;
            }

            if (_simCamera != null)
            {
                _simCamera.transform.position += delta;
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.pivot += delta;
                sceneView.Repaint();
            }
        }

        private static void SaveClipPending(Transform avatarRoot, AnimationClip clip, bool loop)
        {
            var scene = avatarRoot.gameObject.scene;
            SessionState.SetString(ClipSceneKey, scene.path);
            SessionState.SetString(ClipAvatarKey, BuildSiblingPath(avatarRoot));
            SessionState.SetString(ClipPathKey, AssetDatabase.GetAssetPath(clip));
            SessionState.SetString(ClipNameKey, clip.name);
            SessionState.SetBool(ClipLoopKey, loop);
            SessionState.EraseString(ClipPlaylistKey);
            SessionState.SetFloat(ClipPendingTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(ClipPendingKey, true);
        }

        private static void SaveClipPlaylistPending(
            Transform avatarRoot,
            IList<AnimationClip> clips,
            bool loop)
        {
            var scene = avatarRoot.gameObject.scene;
            SessionState.SetString(ClipSceneKey, scene.path);
            SessionState.SetString(ClipAvatarKey, BuildSiblingPath(avatarRoot));
            SessionState.SetString(ClipPlaylistKey, BuildPlaylistString(clips));
            SessionState.SetBool(ClipPlaylistLoopKey, loop);
            SessionState.SetString(ClipPathKey, string.Empty);
            SessionState.SetString(ClipNameKey, string.Empty);
            SessionState.SetFloat(ClipPendingTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(ClipPendingKey, true);
        }

        private static string BuildPlaylistString(IList<AnimationClip> clips)
        {
            var builder = new StringBuilder();
            foreach (var clip in clips)
            {
                if (clip == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(AssetDatabase.GetAssetPath(clip)).Append('|').Append(clip.name);
            }

            return builder.ToString();
        }

        private static List<AnimationClip> ParsePlaylistString(string text)
        {
            var clips = new List<AnimationClip>();
            if (string.IsNullOrEmpty(text))
            {
                return clips;
            }

            foreach (var line in text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = line.IndexOf('|');
                var path = separator >= 0 ? line.Substring(0, separator) : line;
                var name = separator >= 0 ? line.Substring(separator + 1) : string.Empty;
                var clip = LoadClip(path, name);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        private static void TryStartClipPending()
        {
            SessionState.SetBool(ClipPendingKey, false);

            var pendingAge = (float)EditorApplication.timeSinceStartup
                             - SessionState.GetFloat(ClipPendingTimeKey, 0f);
            if (pendingAge > PendingTimeoutSeconds)
            {
                return;
            }

            var scene = GetScene(SessionState.GetString(ClipSceneKey, string.Empty));
            var avatarRoot = ResolveSiblingPath(scene, SessionState.GetString(ClipAvatarKey, string.Empty));
            if (avatarRoot == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] " + PhysBoneLocalization.Tr("在 Play Mode 中找不到角色根节点。"));
                return;
            }

            var playlistText = SessionState.GetString(ClipPlaylistKey, string.Empty);
            if (!string.IsNullOrEmpty(playlistText))
            {
                var playlist = ParsePlaylistString(playlistText);
                SessionState.EraseString(ClipPlaylistKey);
                if (playlist.Count > 0)
                {
                    StartClipPlaylistPlayback(
                        avatarRoot,
                        playlist,
                        SessionState.GetBool(ClipPlaylistLoopKey, true));
                    return;
                }

                Debug.LogWarning("[PhysBone Motion Preview] 拼接播放列表为空。");
                return;
            }

            var clip = LoadClip(
                SessionState.GetString(ClipPathKey, string.Empty),
                SessionState.GetString(ClipNameKey, string.Empty));
            if (clip == null)
            {
                Debug.LogWarning("[PhysBone Motion Preview] 找不到要播放的动画。");
                return;
            }

            StartClipPlayback(avatarRoot, clip, SessionState.GetBool(ClipLoopKey, true));
        }

        private static AnimationClip LoadClip(string assetPath, string clipName)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            var main = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (main != null && (string.IsNullOrEmpty(clipName) || main.name == clipName))
            {
                return main;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                var clip = asset as AnimationClip;
                if (clip != null && (string.IsNullOrEmpty(clipName) || clip.name == clipName))
                {
                    return clip;
                }
            }

            return null;
        }

        private static void StartClipPlayback(Transform avatarRoot, AnimationClip clip, bool loop)
        {
            if (avatarRoot == null || clip == null)
            {
                return;
            }

            string unsafeReason;
            if (!AnimationClipClassifier.IsSafeStandalone(clip, out unsafeReason))
            {
                Debug.LogWarning("[PhysBone Motion Preview] 已阻止可能畸形的动画："
                                 + clip.name + " / " + unsafeReason);
                return;
            }

            StopInternal(false);

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = avatarRoot.GetComponentInChildren<Animator>(true);
            }

            if (animator == null)
            {
                Debug.LogWarning("[PhysBone Motion Preview] 角色上没有 Animator，无法播放动画。");
                return;
            }

            _clip = clip;
            _clipAvatarRoot = avatarRoot;
            _clipLoop = loop;
            _clipPoseSnapshot = CapturePose(avatarRoot);

            _clipGraph = PlayableGraph.Create("PhysBone Motion Preview Clip");
            var output = AnimationPlayableOutput.Create(_clipGraph, "Clip", animator);
            _clipPlayable = AnimationClipPlayable.Create(_clipGraph, clip);
            _clipPlayable.SetApplyFootIK(false);
            _clipPlayable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(_clipPlayable);
            _clipGraph.Play();

            _clipStartTime = EditorApplication.timeSinceStartup;
            _playingClip = true;
            _active = true;
            StateChanged?.Invoke();
        }

        private static void StartClipPlaylistPlayback(
            Transform avatarRoot,
            IList<AnimationClip> clips,
            bool loop)
        {
            if (avatarRoot == null || clips == null || clips.Count == 0)
            {
                return;
            }

            StopInternal(false);

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = avatarRoot.GetComponentInChildren<Animator>(true);
            }

            if (animator == null)
            {
                Debug.LogWarning("[PhysBone Motion Preview] 角色上没有 Animator，无法播放动画。");
                return;
            }

            if (!animator.isHuman)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] 角色 Animator 不是 Humanoid，"
                    + "拼接播放可能导致骨骼拉伸，已取消。");
                return;
            }

            var playlist = new List<AnimationClip>();
            foreach (var clip in clips)
            {
                string reason;
                if (AnimationClipClassifier.CanUseInPlaylist(clip, out reason))
                {
                    playlist.Add(clip);
                }
                else
                {
                    Debug.LogWarning("[PhysBone Motion Preview] 跳过不适合拼接播放的动画："
                                     + (clip != null ? clip.name : "null") + " / " + reason);
                }
            }

            if (playlist.Count == 0)
            {
                Debug.LogWarning("[PhysBone Motion Preview] 没有可拼接播放的 Humanoid 循环动画。");
                return;
            }

            _clipAvatarRoot = avatarRoot;
            _clipPlaylist = playlist;
            _clipPlaylistIndex = 0;
            _clipPlaylistLoop = loop;
            _clipPoseSnapshot = CapturePose(avatarRoot);

            _clipGraph = PlayableGraph.Create("PhysBone Motion Preview Playlist");
            _clipOutput = AnimationPlayableOutput.Create(_clipGraph, "Clip", animator);
            _clipGraph.Play();

            _playingClip = true;
            _active = true;
            PlayClipPlaylistIndex(0);
            StateChanged?.Invoke();
        }

        private static void PlayClipPlaylistIndex(int index)
        {
            if (_clipPlaylist == null || index < 0 || index >= _clipPlaylist.Count)
            {
                return;
            }

            _clipPlaylistIndex = index;
            var next = _clipPlaylist[index];
            if (next == null)
            {
                return;
            }

            var previous = _clipPlayable;
            var playable = AnimationClipPlayable.Create(_clipGraph, next);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            _clipOutput.SetSourcePlayable(playable);
            _clipPlayable = playable;

            if (previous.IsValid())
            {
                _clipGraph.DestroyPlayable(previous);
            }

            _clip = next;
            _clipStartTime = EditorApplication.timeSinceStartup;
            _clipPlaylistDuration = Mathf.Max(next.length, 0.5f);
        }

        private static List<TransformPose> CapturePose(Transform root)
        {
            var result = new List<TransformPose>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                result.Add(new TransformPose
                {
                    Transform = transform,
                    LocalPosition = transform.localPosition,
                    LocalRotation = transform.localRotation,
                    LocalScale = transform.localScale
                });
            }

            return result;
        }

        private static void RestorePose(List<TransformPose> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (var pose in snapshot)
            {
                if (pose.Transform == null)
                {
                    continue;
                }

                pose.Transform.localPosition = pose.LocalPosition;
                pose.Transform.localRotation = pose.LocalRotation;
                pose.Transform.localScale = pose.LocalScale;
            }
        }

        /// <summary>
        /// Applies the persistent build-blocking preferences to VRCFury and NDMF.
        /// Checked = blocked (Play Mode processing disabled); unchecked = enabled.
        /// </summary>
        public static void ApplyBuildBlocking()
        {
            if (IsVrcfuryAvailable())
            {
                EditorPrefs.SetBool(VrcfuryPlayModePref, !BlockVrcfury);
            }

            var property = GetNdmfApplyOnPlayProperty();
            if (property != null)
            {
                property.SetValue(null, !BlockNdmf);
            }
        }

        private static bool IsVrcfuryAvailable()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (name == "VRCFury" || name == "VRCFury-Editor")
                {
                    return true;
                }
            }

            return false;
        }

        private static PropertyInfo GetNdmfApplyOnPlayProperty()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("nadena.dev.ndmf.config.Config", false);
                if (type == null)
                {
                    continue;
                }

                var property = type.GetProperty(
                    "ApplyOnPlay",
                    BindingFlags.Public | BindingFlags.Static);
                if (property != null
                    && property.PropertyType == typeof(bool)
                    && property.CanRead
                    && property.CanWrite)
                {
                    return property;
                }
            }

            return null;
        }

        private static void StartInternal(Component physBone, Transform driver, MotionSettings settings)
        {
            if (driver == null || settings == null)
            {
                return;
            }

            StopInternal(false);

            _physBone = physBone;
            _pendingReEnable = false;
            if (physBone != null)
            {
                var root = PhysBoneReflection.GetRootTransform(physBone);
                if (driver == root)
                {
                    var currentIsAnimated = PhysBoneReflection.GetFieldValue(physBone, "isAnimated");
                    _originalIsAnimated = currentIsAnimated is bool value && value;
                    _isAnimatedOverridden = !_originalIsAnimated;
                    if (_isAnimatedOverridden)
                    {
                        PhysBoneReflection.SetBool(physBone, "isAnimated", true);

                        // VRCPhysBone caches whether its root is animated at initialization.
                        // Toggle the component off for one frame so it reinitializes with isAnimated = true.
                        var behaviour = physBone as Behaviour;
                        if (behaviour != null && behaviour.enabled)
                        {
                            behaviour.enabled = false;
                            _pendingReEnable = true;
                            _disableFrame = Time.frameCount;
                            _reEnableTime = EditorApplication.timeSinceStartup + 0.1;
                        }
                    }
                }
            }

            _driver = driver;
            _initialLocalPosition = driver.localPosition;
            _initialLocalRotation = driver.localRotation;
            _preset = settings.Preset;
            _mode = settings.Mode;
            _amplitude = Mathf.Max(0f, settings.Amplitude);
            _frequency = Mathf.Max(0.01f, settings.Frequency);
            _duration = Mathf.Max(0f, settings.Duration);
            _startTime = EditorApplication.timeSinceStartup;
            _active = true;

            StateChanged?.Invoke();
        }

        private static void StopInternal(bool notify)
        {
            var wasActive = _active;

            if (_simulating)
            {
                StopSimulationGait();

                if (_simAvatarRoot != null)
                {
                    _simAvatarRoot.position = _simOriginalPosition;
                    _simAvatarRoot.rotation = _simOriginalRotation;
                }

                if (_simAnimator != null)
                {
                    _simAnimator.enabled = false;
                    _simAnimator.runtimeAnimatorController = _simOriginalController;
                    if (_simAvatarRebuilt)
                    {
                        _simAnimator.avatar = _simOriginalAvatar;
                    }
                    _simAnimator.applyRootMotion = _simOriginalApplyRootMotion;
                }

                RestorePose(_simPoseSnapshot);
                _simPoseSnapshot = null;

                if (_simCamera != null)
                {
                    _simCamera.transform.position = _simOriginalCameraPosition;
                    _simCamera.transform.rotation = _simOriginalCameraRotation;
                }

                if (_simScenePivotCaptured && SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.pivot = _simOriginalScenePivot;
                    SceneView.lastActiveSceneView.Repaint();
                }

                _simulating = false;
                _simAvatarRoot = null;
                _simAnimator = null;
                _simOriginalAvatar = null;
                _simAvatarRebuilt = false;
                _simCamera = null;
                _simVelocity = Vector3.zero;
                _simCurrentSpeed = 0f;
                _simCurrentState = string.Empty;
                _simParameters = null;
                _simScenePivotCaptured = false;
                _simExternalInput = false;
                _simExternalX = 0f;
                _simExternalZ = 0f;
                _simExternalRun = false;
            }

            if (_playingClip)
            {
                if (_clipGraph.IsValid())
                {
                    _clipGraph.Destroy();
                }

                _clipGraph = default(PlayableGraph);
                _clipPlayable = default(AnimationClipPlayable);
                _clipOutput = default(AnimationPlayableOutput);
                _clipPlaylist = null;
                _clipPlaylistIndex = 0;
                _clipPlaylistDuration = 0f;
                RestorePose(_clipPoseSnapshot);
                _clipPoseSnapshot = null;
                _playingClip = false;
                _clip = null;
                _clipAvatarRoot = null;
            }

            if (_driver != null)
            {
                _driver.localPosition = _initialLocalPosition;
                _driver.localRotation = _initialLocalRotation;
            }

            if (_pendingReEnable && _physBone != null)
            {
                var behaviour = _physBone as Behaviour;
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }

                _pendingReEnable = false;
            }

            if (_isAnimatedOverridden && _physBone != null)
            {
                PhysBoneReflection.SetBool(_physBone, "isAnimated", _originalIsAnimated);
            }

            _active = false;
            _physBone = null;
            _isAnimatedOverridden = false;
            _driver = null;

            if (notify && wasActive)
            {
                StateChanged?.Invoke();
            }
        }

        private static void Tick()
        {
            if (!_active)
            {
                return;
            }

            if (_simulating)
            {
                TickSimulation();
                return;
            }

            if (_playingClip)
            {
                if (!EditorApplication.isPlaying
                    || _clip == null
                    || _clipAvatarRoot == null
                    || !_clipGraph.IsValid()
                    || !_clipPlayable.IsValid())
                {
                    StopInternal(true);
                    return;
                }

                var clipElapsed = (float)(EditorApplication.timeSinceStartup - _clipStartTime);

                if (_clipPlaylist != null && _clipPlaylist.Count > 1)
                {
                    var duration = Mathf.Max(_clipPlaylistDuration, 0.01f);
                    _clipPlayable.SetTime(Mathf.Min(clipElapsed, duration));

                    if (clipElapsed >= duration)
                    {
                        var nextIndex = _clipPlaylistIndex + 1;
                        if (nextIndex < _clipPlaylist.Count)
                        {
                            PlayClipPlaylistIndex(nextIndex);
                        }
                        else if (_clipPlaylistLoop)
                        {
                            PlayClipPlaylistIndex(0);
                        }
                        else
                        {
                            StopInternal(true);
                        }
                    }

                    return;
                }

                if (_clipPlaylist != null && _clipPlaylist.Count == 1)
                {
                    var singleLength = Mathf.Max(0.01f, _clip.length);
                    var singleTime = _clipPlaylistLoop
                        ? Mathf.Repeat(clipElapsed, singleLength)
                        : Mathf.Min(clipElapsed, singleLength);
                    _clipPlayable.SetTime(singleTime);

                    if (!_clipPlaylistLoop && clipElapsed >= singleLength)
                    {
                        StopInternal(true);
                    }

                    return;
                }

                var length = Mathf.Max(0.01f, _clip.length);
                var clipTime = _clipLoop
                    ? Mathf.Repeat(clipElapsed, length)
                    : Mathf.Min(clipElapsed, length);
                _clipPlayable.SetTime(clipTime);

                // Do not call graph.Evaluate() here: the Animator must evaluate the graph
                // so humanoid retargeting and the PhysBone update order stay correct.
                return;
            }

            if (_driver == null || !EditorApplication.isPlaying)
            {
                StopInternal(true);
                return;
            }

            if (_pendingReEnable)
            {
                // Make sure the player loop actually advances while the component is disabled.
                EditorApplication.QueuePlayerLoopUpdate();

                if (EditorApplication.timeSinceStartup >= _reEnableTime && Time.frameCount > _disableFrame + 1)
                {
                    var behaviour = _physBone as Behaviour;
                    if (behaviour != null)
                    {
                        behaviour.enabled = true;
                    }

                    _pendingReEnable = false;
                }
            }

            var elapsed = (float)(EditorApplication.timeSinceStartup - _startTime);
            if (_duration > 0f && elapsed >= _duration)
            {
                StopInternal(true);
                return;
            }

            Vector3 translationOffset;
            Vector3 rotationEuler;
            EvaluateMotion(_preset, elapsed, _frequency, _amplitude, out translationOffset, out rotationEuler);

            if (_mode == MotionAxisMode.Translation)
            {
                _driver.localPosition = _initialLocalPosition + translationOffset;
            }
            else
            {
                _driver.localRotation = _initialLocalRotation * Quaternion.Euler(rotationEuler);
            }
        }

        private static void EvaluateMotion(
            MotionPreset preset,
            float time,
            float frequency,
            float amplitude,
            out Vector3 translation,
            out Vector3 rotation)
        {
            var phase = time * frequency * Mathf.PI * 2f;
            var sin = Mathf.Sin(phase);
            var cos = Mathf.Cos(phase);

            translation = Vector3.zero;
            rotation = Vector3.zero;

            switch (preset)
            {
                case MotionPreset.Bob:
                    translation = new Vector3(0f, sin * amplitude, 0f);
                    rotation = new Vector3(sin * amplitude, 0f, 0f);
                    break;

                case MotionPreset.Sway:
                    translation = new Vector3(sin * amplitude, 0f, 0f);
                    rotation = new Vector3(0f, sin * amplitude, 0f);
                    break;

                case MotionPreset.Surge:
                    translation = new Vector3(0f, 0f, sin * amplitude);
                    rotation = new Vector3(0f, 0f, sin * amplitude);
                    break;

                case MotionPreset.Circle:
                    translation = new Vector3(sin * amplitude, 0f, cos * amplitude);
                    rotation = new Vector3(sin * amplitude, 0f, cos * amplitude);
                    break;

                case MotionPreset.Jitter:
                {
                    var a = Mathf.Sin(phase * 3.1f) * 0.6f + Mathf.Sin(phase * 7.3f) * 0.4f;
                    var b = Mathf.Sin(phase * 4.7f + 1.3f) * 0.6f + Mathf.Sin(phase * 8.9f + 0.7f) * 0.4f;
                    translation = new Vector3(a, b, 0f) * (amplitude * 0.5f);
                    rotation = new Vector3(a, b, 0f) * (amplitude * 0.5f);
                    break;
                }

                case MotionPreset.Wind:
                {
                    var a = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 0.37f + 1.7f) * 0.3f;
                    var b = Mathf.Sin(phase * 1.31f + 0.9f) * 0.5f;
                    translation = new Vector3(a, 0f, b) * amplitude;
                    rotation = new Vector3(b, a * 0.5f, 0f) * amplitude;
                    break;
                }

                case MotionPreset.Pulse:
                {
                    var period = 1f / Mathf.Max(0.01f, frequency);
                    var u = Mathf.Repeat(time / period, 1f);
                    float envelope;
                    if (u < 0.12f)
                    {
                        envelope = u / 0.12f;
                    }
                    else if (u < 0.30f)
                    {
                        envelope = 1f;
                    }
                    else if (u < 0.80f)
                    {
                        envelope = 1f - (u - 0.30f) / 0.50f;
                    }
                    else
                    {
                        envelope = 0f;
                    }

                    translation = new Vector3(envelope * amplitude, 0f, 0f);
                    rotation = new Vector3(envelope * amplitude, 0f, 0f);
                    break;
                }
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    ApplyBuildBlocking();
                    if (SessionState.GetBool(SimPendingKey, false))
                    {
                        TryStartSimPending();
                    }
                    else if (SessionState.GetBool(ClipPendingKey, false))
                    {
                        TryStartClipPending();
                    }
                    else if (SessionState.GetBool(PendingKey, false))
                    {
                        TryStartPending();
                    }
                    break;

                case PlayModeStateChange.ExitingPlayMode:
                    StopInternal(true);
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    ApplyBuildBlocking();
                    break;
            }
        }

        private static void SavePending(Component physBone, Transform driver, MotionSettings settings)
        {
            var targetScene = physBone.gameObject.scene;
            var driverScene = driver.gameObject.scene;

            SessionState.SetString(SceneKey, targetScene.path);
            SessionState.SetString(TargetKey, BuildSiblingPath(physBone.transform));
            SessionState.SetString(DriverSceneKey, driverScene.path);
            SessionState.SetString(DriverKey, BuildSiblingPath(driver));
            SessionState.SetInt(PresetKey, (int)settings.Preset);
            SessionState.SetInt(ModeKey, (int)settings.Mode);
            SessionState.SetFloat(AmplitudeKey, settings.Amplitude);
            SessionState.SetFloat(FrequencyKey, settings.Frequency);
            SessionState.SetFloat(DurationKey, settings.Duration);
            SessionState.SetFloat(PendingTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(PendingKey, true);
        }

        private static void TryStartPending()
        {
            SessionState.SetBool(PendingKey, false);

            var pendingAge = (float)EditorApplication.timeSinceStartup
                             - SessionState.GetFloat(PendingTimeKey, 0f);
            if (pendingAge > PendingTimeoutSeconds)
            {
                return;
            }

            var targetScene = GetScene(SessionState.GetString(SceneKey, string.Empty));
            var targetTransform = ResolveSiblingPath(targetScene, SessionState.GetString(TargetKey, string.Empty));
            if (targetTransform == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] " + PhysBoneLocalization.Tr("在 Play Mode 中找不到目标对象。"));
                return;
            }

            var physBone = PhysBoneReflection.GetPhysBone(targetTransform.gameObject)
                           ?? PhysBoneReflection.FindPhysBoneInParents(targetTransform.gameObject);
            if (physBone == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] " + PhysBoneLocalization.Tr("目标对象上没有 VRCPhysBone。"));
                return;
            }

            var driverScene = GetScene(SessionState.GetString(DriverSceneKey, string.Empty));
            var driver = ResolveSiblingPath(driverScene, SessionState.GetString(DriverKey, string.Empty));
            if (driver == null)
            {
                driver = PhysBoneReflection.GetRootTransform(physBone);
            }

            if (driver == null)
            {
                Debug.LogWarning(
                    "[PhysBone Motion Preview] " + PhysBoneLocalization.Tr("找不到驱动对象。"));
                return;
            }

            var settings = new MotionSettings
            {
                Preset = (MotionPreset)SessionState.GetInt(PresetKey, (int)MotionPreset.Bob),
                Mode = (MotionAxisMode)SessionState.GetInt(ModeKey, (int)MotionAxisMode.Translation),
                Amplitude = SessionState.GetFloat(AmplitudeKey, 0.05f),
                Frequency = SessionState.GetFloat(FrequencyKey, 1.5f),
                Duration = SessionState.GetFloat(DurationKey, 5f)
            };

            StartInternal(physBone, driver, settings);
        }

        private static Scene GetScene(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                {
                    return scene;
                }
            }

            return SceneManager.GetActiveScene();
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
