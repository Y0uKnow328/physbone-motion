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
        private static Vector2 _lastSceneMousePosition;
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

        static PhysBonePreviewOverlay()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public static void Recenter(Component physBone)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            var root = physBone != null
                ? PhysBoneReflection.GetRootTransform(physBone)
                : null;
            if (root != null)
            {
                sceneView.LookAt(root.position, sceneView.rotation, 1.5f);
                sceneView.Repaint();
            }
            else
            {
                sceneView.FrameSelected();
            }
        }

        public static void PlaceAtMouse()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(_lastSceneMousePosition);
            Place(sceneView, ray);
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            _lastSceneMousePosition = Event.current.mousePosition;

            var simulating = PhysBoneMotionDriver.IsSimulating;
            if (simulating)
            {
                HandleSimulationInput(sceneView);
            }
            else
            {
                ClearSimulationInput();
            }

            if (!ShowBones)
            {
                return;
            }

            if (!simulating
                && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.X
                && !Event.current.control
                && !Event.current.alt
                && !Event.current.shift)
            {
                var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                Place(sceneView, ray);
                Event.current.Use();
            }

            DrawBones();
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

        private static void Place(SceneView sceneView, Ray ray)
        {
            if (sceneView == null || sceneView.camera == null)
            {
                return;
            }

            var plane = new Plane(-sceneView.camera.transform.forward, sceneView.pivot);
            float distance;
            if (plane.Raycast(ray, out distance))
            {
                sceneView.pivot = ray.GetPoint(distance);
                sceneView.Repaint();
            }
        }

        private static void DrawBones()
        {
            var active = Selection.activeGameObject;
            if (active == null)
            {
                return;
            }

            var avatarRoot = PhysBoneReflection.FindAvatarRoot(active.transform);
            if (avatarRoot == null)
            {
                return;
            }

            var physBones = PhysBoneReflection.CollectPhysBones(avatarRoot);
            if (physBones.Count == 0)
            {
                return;
            }

            var selected = PhysBoneReflection.FindPhysBoneInParents(active);

            foreach (var physBone in physBones)
            {
                var root = PhysBoneReflection.GetRootTransform(physBone);
                if (root == null)
                {
                    continue;
                }

                var points = new List<Vector3> { root.position };
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
                    points.Add(current.position);
                }

                Handles.color = physBone == selected
                    ? new Color(1f, 0.85f, 0.2f, 0.95f)
                    : new Color(0.2f, 0.85f, 1f, 0.7f);
                Handles.DrawAAPolyLine(3f, points.ToArray());

                foreach (var point in points)
                {
                    Handles.SphereHandleCap(
                        0,
                        point,
                        Quaternion.identity,
                        HandleUtility.GetHandleSize(point) * 0.03f,
                        EventType.Repaint);
                }
            }
        }
    }
}
