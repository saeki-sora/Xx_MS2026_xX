using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// シーンビューを「ゲームのカメラと同じ見え方」に固定する（位置・向き・遠近・視野角まで合わせる）。
    /// 固定中もシーンビューの中で背景をそのまま動かせるので、ゲーム画面の見た目で配置を決められる。
    /// シーンビューを回す・ずらす・拡大縮小すると固定は外れる。固定中は Game ビューの縦横比の枠を描く（枠の外は暗くする）。
    /// Unity のシーンビューは視野角と距離を自分の式で決めるので、映った結果を見て少しずつ補正して合わせる。
    /// </summary>
    [InitializeOnLoad]
    public static class StageSceneViewSync
    {
        private static SceneView _view;
        private static int _viewer;
        private static Vector3 _appliedPivot;
        private static Quaternion _appliedRotation;
        private static float _appliedSize;
        private static GUIStyle _banner;

        static StageSceneViewSync()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        public static bool IsLocked => _view != null;

        public static int LockedViewer => _viewer;

        /// <summary>シーンビューをその視点のゲームカメラに合わせて固定する。</summary>
        public static bool Lock(int viewer, SceneView view = null)
        {
            view ??= SceneView.lastActiveSceneView;
            if (view == null || !StageGameCamera.TryGetView(viewer, out var settings))
            {
                return false;
            }

            _view = view;
            _viewer = viewer;
            view.in2DMode = false;
            view.orthographic = settings.IsOrthographic;
            if (!settings.IsOrthographic)
            {
                var cameraSettings = view.cameraSettings;
                cameraSettings.fieldOfView = settings.fieldOfView;
                view.cameraSettings = cameraSettings;
            }

            CameraViewMath.GetPose(settings, out _, out var rotation);
            var size = settings.IsOrthographic ? settings.orthographicSize : settings.distance * Mathf.Sin(settings.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Apply(new Vector3(settings.center.x, settings.center.y, 0f), rotation, size);
            return true;
        }

        public static void Unlock()
        {
            _view = null;
            SceneView.RepaintAll();
        }

        /// <summary>固定中だけ、Game ビューの縦横比の枠と「固定中」の帯を描く（StageSceneOverlay から Repaint のときに呼ぶ）。</summary>
        public static void DrawGameFrame(SceneView view)
        {
            if (view != _view || view == null || view.camera == null)
            {
                return;
            }

            var ppp = EditorGUIUtility.pixelsPerPoint;
            var width = view.camera.pixelWidth / ppp;
            var height = view.camera.pixelHeight / ppp;
            var aspect = StageGameCamera.Aspect;
            var frameWidth = Mathf.Min(width, height * aspect);
            var frameHeight = frameWidth / aspect;
            var frame = new Rect((width - frameWidth) * 0.5f, (height - frameHeight) * 0.5f, frameWidth, frameHeight);

            Handles.BeginGUI();
            var shade = new Color(0f, 0f, 0f, 0.45f);
            EditorGUI.DrawRect(new Rect(0, 0, frame.xMin, height), shade);
            EditorGUI.DrawRect(new Rect(frame.xMax, 0, width - frame.xMax, height), shade);
            EditorGUI.DrawRect(new Rect(frame.xMin, 0, frame.width, frame.yMin), shade);
            EditorGUI.DrawRect(new Rect(frame.xMin, frame.yMax, frame.width, height - frame.yMax), shade);

            var color = ViewerIndex.Color(_viewer);
            var line = 2f;
            EditorGUI.DrawRect(new Rect(frame.xMin, frame.yMin, frame.width, line), color);
            EditorGUI.DrawRect(new Rect(frame.xMin, frame.yMax - line, frame.width, line), color);
            EditorGUI.DrawRect(new Rect(frame.xMin, frame.yMin, line, frame.height), color);
            EditorGUI.DrawRect(new Rect(frame.xMax - line, frame.yMin, line, frame.height), color);

            _banner ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            var bannerRect = new Rect(frame.center.x - 170f, frame.yMin + 6f, 340f, 20f);
            EditorGUI.DrawRect(bannerRect, new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(bannerRect, $"{ViewerIndex.LongLabel(_viewer)}のゲーム目線（回す・ずらす・拡大で解除）", _banner);
            Handles.EndGUI();
        }

        private static void OnSceneGui(SceneView view)
        {
            if (view != _view || Event.current.type != EventType.Layout)
            {
                return;
            }

            // シーンビューを自分で回した・ずらした・拡大縮小したら、固定を外す。
            if ((view.pivot - _appliedPivot).sqrMagnitude > 1e-6f ||
                Quaternion.Angle(view.rotation, _appliedRotation) > 0.01f ||
                Mathf.Abs(view.size - _appliedSize) > _appliedSize * 1e-4f + 1e-5f)
            {
                Unlock();
                return;
            }

            if (!StageGameCamera.TryGetView(_viewer, out var settings))
            {
                Unlock();
                return;
            }

            Correct(view, settings);
        }

        // 実際に映ったシーンビューのカメラを見て、視野角・距離（正投影なら映る広さ）・向き・中心を目標に寄せる。
        // プリセットが書き換えられたときもここで追いかける。
        private static void Correct(SceneView view, in CameraViewSettings settings)
        {
            var camera = view.camera;
            CameraViewMath.GetPose(settings, out _, out var rotation);
            var pivot = new Vector3(settings.center.x, settings.center.y, 0f);
            var size = _appliedSize;

            if (view.orthographic != settings.IsOrthographic)
            {
                view.orthographic = settings.IsOrthographic;
            }

            if (settings.IsOrthographic)
            {
                if (camera.orthographicSize > 1e-4f && Mathf.Abs(camera.orthographicSize - settings.orthographicSize) > 1e-3f)
                {
                    size *= settings.orthographicSize / camera.orthographicSize;
                }
            }
            else
            {
                var error = settings.fieldOfView - camera.fieldOfView;
                if (Mathf.Abs(error) > 0.02f)
                {
                    var cameraSettings = view.cameraSettings;
                    cameraSettings.fieldOfView = Mathf.Clamp(cameraSettings.fieldOfView + error, 1f, 179f);
                    view.cameraSettings = cameraSettings;
                }

                if (view.cameraDistance > 1e-4f && Mathf.Abs(view.cameraDistance - settings.distance) > 1e-3f)
                {
                    size *= settings.distance / view.cameraDistance;
                }
            }

            if ((pivot - _appliedPivot).sqrMagnitude > 1e-8f || Quaternion.Angle(rotation, _appliedRotation) > 1e-3f || !Mathf.Approximately(size, _appliedSize))
            {
                Apply(pivot, rotation, size);
            }
        }

        private static void Apply(Vector3 pivot, Quaternion rotation, float size)
        {
            _view.LookAtDirect(pivot, rotation, size);
            _appliedPivot = _view.pivot;
            _appliedRotation = _view.rotation;
            _appliedSize = _view.size;
            _view.Repaint();
        }
    }
}
