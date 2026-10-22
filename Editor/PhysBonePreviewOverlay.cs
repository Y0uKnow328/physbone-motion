using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarChamber.PhysBoneMotion
{
    /// <summary>
    /// Scene view overlay for the preview: bone chain visualization and gizmo-like navigation.
    /// </summary>
    [InitializeOnLoad]
    public static class PhysBonePreviewOverlay
    {
        private const string ShowBonesKey = "AvatarChamber.PhysBoneMotion.ShowBones";
        private const string ShowAvatarGizmoKey = "AvatarChamber.PhysBoneMotion.ShowAvatarGizmo";
        private const string PlaceShortcutKey = "AvatarChamber.PhysBoneMotion.PlaceShortcutKey";
        private static Vector2 _lastSceneMousePosition;
        private static bool _hasSceneMousePosition;
        private static Component _targetPhysBone;
        private static Vector3 _gizmoAnchor;
        private static bool _gizmoAnchorValid;
        private static bool _simW;
        private static bool _simA;
        private static bool _simS;
        private static bool _simD;
        private static bool _simShift;
        private static bool _simZ;
        private static bool _simX;

        public static bool ShowBones
        {
            get { return EditorPrefs.GetBool(ShowBonesKey, false); }
            set
            {
                EditorPrefs.SetBool(ShowBonesKey, value);
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Draws an avatar-level manipulation handle at a free-floating anchor.
        /// Translation arrows and rotation rings are both shown, so the whole
        /// model can be moved without changing the current selection.
        /// </summary>
        public static bool ShowAvatarGizmo
        {
            get { return EditorPrefs.GetBool(ShowAvatarGizmoKey, true); }
            set
            {
                EditorPrefs.SetBool(ShowAvatarGizmoKey, value);
                SceneView.RepaintAll();
            }
        }

        static PhysBonePreviewOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static KeyCode PlaceShortcut
        {
            get
            {
                return (KeyCode)EditorPrefs.GetInt(
                    PlaceShortcutKey,
                    (int)KeyCode.X);
            }
            set
            {
                EditorPrefs.SetInt(PlaceShortcutKey, (int)value);
                SceneView.RepaintAll();
            }
        }

        public static string GetPlaceShortcutDisplay()
        {
            return PlaceShortcut.ToString();
        }

        public static bool SetPlaceShortcut(KeyCode key)
        {
            if (key == KeyCode.None)
            {
                return false;
            }

            PlaceShortcut = key;
            return true;
        }

        public static void SetTargetPhysBone(Component physBone)
        {
            var oldRoot = _targetPhysBone != null
                ? PhysBoneReflection.FindAvatarRoot(_targetPhysBone)
                : null;
            var newRoot = physBone != null
                ? PhysBoneReflection.FindAvatarRoot(physBone)
                : null;

            // Keep a manually placed anchor while switching between parts of
            // the same avatar; reset it only when the avatar changes.
            if (oldRoot != newRoot)
            {
                _gizmoAnchorValid = false;
            }

            _targetPhysBone = physBone;
            SceneView.RepaintAll();
        }

        private static Transform GetTargetAvatarRoot()
        {
            return _targetPhysBone != null
                ? PhysBoneReflection.FindAvatarRoot(_targetPhysBone)
                : null;
        }

        private static SceneView GetSceneView()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                return SceneView.lastActiveSceneView;
            }

            return SceneView.sceneViews.Count > 0 ? SceneView.sceneViews[0] as SceneView : null;
        }

        /// <summary>
        /// Moves the gizmo anchor back to the avatar's center. The Scene view
        /// camera is intentionally left untouched.
        /// </summary>
        public static void RecenterGizmo(Component physBone)
        {
            var root = physBone != null
                ? PhysBoneReflection.FindAvatarRoot(physBone)
                : GetTargetAvatarRoot();
            if (root == null)
            {
                return;
            }

            _gizmoAnchor = CalculateAvatarCenter(root);
            _gizmoAnchorValid = true;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Places the gizmo anchor at the world position under the mouse. The
        /// ray intersects a plane through the avatar center, and the Scene view
        /// camera is not moved.
        /// </summary>
        public static void PlaceGizmoAtMouse()
        {
            var sceneView = GetSceneView();
            var root = GetTargetAvatarRoot();
            if (sceneView == null || sceneView.camera == null || root == null)
            {
                return;
            }

            var mousePosition = GetSceneMousePosition(sceneView);
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            // Keep the placement plane at the avatar's depth so repeated drops
            // do not drift toward or away from the camera.
            var center = CalculateAvatarCenter(root);
            var plane = new Plane(sceneView.camera.transform.forward, center);

            float distance;
            if (!plane.Raycast(ray, out distance))
            {
                return;
            }

            _gizmoAnchor = ray.GetPoint(distance);
            _gizmoAnchorValid = true;
            SceneView.RepaintAll();
        }

        private static Vector2 GetSceneMousePosition(SceneView sceneView)
        {
            // The cached position can be stale (for example after the Scene view
            // was resized); fall back to the center when it is outside the view.
            var rect = sceneView.position;
            var mousePosition = _lastSceneMousePosition;
            if (!_hasSceneMousePosition
                || mousePosition.x < 0f || mousePosition.y < 0f
                || mousePosition.x > rect.width || mousePosition.y > rect.height)
            {
                mousePosition = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            }

            return mousePosition;
        }

        private static Vector3 CalculateAvatarCenter(Transform root)
        {
            if (root == null)
            {
                return Vector3.zero;
            }

            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.GetComponentInChildren<Animator>();
            }

            if (animator != null && animator.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                {
                    return hips.position;
                }
            }

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                }

                return bounds.center;
            }

            return root.position;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var current = Event.current;
            if (current != null)
            {
                _lastSceneMousePosition = current.mousePosition;
                _hasSceneMousePosition = true;
            }

            var simulating = PhysBoneMotionDriver.IsSimulating;
            if (simulating)
            {
                HandleSimulationInput(sceneView);
            }
            else
            {
                ClearSimulationInput();
            }

            // Placement shortcut. Handled here instead of Unity's Shortcut Manager
            // so it works even when the active shortcut profile is read-only.
            if (!simulating
                && current != null
                && sceneView != null
                && sceneView.hasFocus
                && current.type == EventType.KeyDown)
            {
                var shortcut = PlaceShortcut;
                if (shortcut != KeyCode.None
                    && current.keyCode == shortcut
                    && !current.control
                    && !current.alt
                    && !current.shift)
                {
                    PlaceGizmoAtMouse();
                    current.Use();
                }
            }

            if (ShowAvatarGizmo)
            {
                DrawAvatarGizmo();
            }

            if (ShowBones)
            {
                DrawBones();
            }
        }

        private static void DrawAvatarGizmo()
        {
            var avatarRoot = GetTargetAvatarRoot();
            if (avatarRoot == null)
            {
                return;
            }

            // Unity already draws transform handles for the selected object.
            // Hide ours only when that object is the avatar root itself, to
            // avoid stacking two identical handles at the same place.
            if (Selection.activeTransform == avatarRoot
                && (Tools.current == Tool.Move || Tools.current == Tool.Rotate))
            {
                return;
            }

            if (!_gizmoAnchorValid)
            {
                _gizmoAnchor = CalculateAvatarCenter(avatarRoot);
                _gizmoAnchorValid = true;
            }

            var anchor = _gizmoAnchor;
            var rotation = avatarRoot.rotation;

            // Both handle types stay visible at the same time: arrows for
            // translation and rings for rotation.
            var newAnchor = Handles.PositionHandle(anchor, rotation);
            var newRotation = Handles.RotationHandle(rotation, anchor);

            if (newAnchor == anchor && newRotation == rotation)
            {
                return;
            }

            // Manual manipulation takes over from a running preset or animation.
            if (PhysBoneMotionDriver.IsPlayingMotion || PhysBoneMotionDriver.IsPlayingClip)
            {
                PhysBoneMotionDriver.Stop();
            }

            var recordUndo = !EditorApplication.isPlaying;
            if (recordUndo)
            {
                Undo.RecordObject(avatarRoot, "Manipulate PhysBone Avatar");
            }

            _gizmoAnchor = ApplyGizmoTransform(
                avatarRoot,
                anchor,
                newAnchor,
                newRotation);

            if (recordUndo)
            {
                EditorUtility.SetDirty(avatarRoot);

                var scene = avatarRoot.gameObject.scene;
                if (scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            SceneView.RepaintAll();
        }

        private static Vector3 ApplyGizmoTransform(
            Transform avatarRoot,
            Vector3 anchor,
            Vector3 newAnchor,
            Quaternion newRotation)
        {
            if (avatarRoot == null)
            {
                return newAnchor;
            }

            var oldRotation = avatarRoot.rotation;
            var position = avatarRoot.position + (newAnchor - anchor);

            if (newRotation != oldRotation)
            {
                var delta = newRotation * Quaternion.Inverse(oldRotation);
                position = newAnchor + delta * (position - newAnchor);
            }

            avatarRoot.SetPositionAndRotation(position, newRotation);
            return newAnchor;
        }

        private static void HandleSimulationInput(SceneView sceneView)
        {
            if (sceneView == null || !sceneView.hasFocus)
            {
                ClearSimulationInput();
                return;
            }

            var current = Event.current;
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.KeyDown || current.type == EventType.KeyUp)
            {
                var down = current.type == EventType.KeyDown;
                switch (current.keyCode)
                {
                    case KeyCode.W:
                        _simW = down;
                        break;
                    case KeyCode.A:
                        _simA = down;
                        break;
                    case KeyCode.S:
                        _simS = down;
                        break;
                    case KeyCode.D:
                        _simD = down;
                        break;
                    case KeyCode.LeftShift:
                    case KeyCode.RightShift:
                        _simShift = down;
                        break;
                    case KeyCode.Z:
                        if (down && !_simZ)
                        {
                            PhysBoneMotionDriver.ToggleSimulationCrouch();
                        }
                        _simZ = down;
                        break;
                    case KeyCode.X:
                        if (down && !_simX)
                        {
                            PhysBoneMotionDriver.ToggleSimulationProne();
                        }
                        _simX = down;
                        break;
                    default:
                        return;
                }

                current.Use();
            }

            var x = (_simD ? 1f : 0f) - (_simA ? 1f : 0f);
            var z = (_simW ? 1f : 0f) - (_simS ? 1f : 0f);
            PhysBoneMotionDriver.SetSimulationExternalInput(x, z, _simShift);
        }

        private static void ClearSimulationInput()
        {
            _simW = false;
            _simA = false;
            _simS = false;
            _simD = false;
            _simShift = false;
            _simZ = false;
            _simX = false;
            PhysBoneMotionDriver.ClearSimulationExternalInput();
        }

        private static void DrawBones()
        {
            var selected = _targetPhysBone;
            if (selected == null && Selection.activeGameObject != null)
            {
                selected = PhysBoneReflection.FindPhysBoneInParents(Selection.activeGameObject);
            }

            if (selected == null)
            {
                return;
            }

            var avatarRoot = PhysBoneReflection.FindAvatarRoot(selected);
            if (avatarRoot == null)
            {
                return;
            }

            var physBones = PhysBoneReflection.CollectPhysBones(avatarRoot);
            if (physBones.Count == 0)
            {
                return;
            }

            var originalColor = Handles.color;
            foreach (var physBone in physBones)
            {
                var root = PhysBoneReflection.GetRootTransform(physBone);
                if (root == null)
                {
                    continue;
                }

                var chain = new List<Transform> { root };
                var current = root;
                var guard = 0;
                while (current != null && current.childCount > 0 && guard++ < 64)
                {
                    var next = current.GetChild(0);
                    if (PhysBoneReflection.GetPhysBone(next.gameObject) != null)
                    {
                        break;
                    }

                    current = next;
                    chain.Add(current);
                }

                var points = new Vector3[chain.Count];
                for (var i = 0; i < chain.Count; i++)
                {
                    points[i] = chain[i].position;
                }

                Handles.color = physBone == selected
                    ? new Color(1f, 0.85f, 0.2f, 1f)
                    : new Color(0.2f, 0.85f, 1f, 0.8f);

                // Make PhysBone chains visually heavier than ordinary scene lines.
                Handles.DrawAAPolyLine(physBone == selected ? 6f : 4f, points);

                // Every joint is a pick target. Clicking one selects that bone,
                // so the chain is much easier to select than a thin transform line.
                for (var i = 0; i < chain.Count; i++)
                {
                    var point = points[i];
                    var size = HandleUtility.GetHandleSize(point)
                        * (i == 0 ? 0.09f : 0.055f);

                    if (Handles.Button(
                        point,
                        Quaternion.identity,
                        size,
                        size,
                        Handles.SphereHandleCap))
                    {
                        Selection.activeGameObject = chain[i].gameObject;
                    }
                }
            }

            Handles.color = originalColor;
        }
    }
}
