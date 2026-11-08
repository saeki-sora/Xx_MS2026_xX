using MS2026.Fortress.Cameras;
using MS2026.ObjectToon.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 編集中の Game ビューを「Play 中と同じ見え方」にする（見るだけ。シーンには残さない）。
    /// ・本物のカメラを、見え方プリセットの その席（全体 / 1P〜4P）の位置・向き・視野角へ動かす。
    /// ・カメラの仕上げ（後処理）を ON にし、Play 中と同じ光のにじみ（Bloom: Resources/ToonGlowProfile）をかける。
    /// ・ステージの物を Play 中と同じトゥーンにする（3Dオブジェクトのトゥーンのプレビューを使う）。
    /// ・歯のコアを、Play 中と同じく その席の画面の回転だけ回す（どの席からも 1P と同じ見え方）。
    /// 保存の直前・Play を始める直前・スクリプトの読み直しの前には、カメラを元に戻す（保存のあとはまた当てる）。
    /// 靄・環境の粒など、Play 中にだけ作られる演出は出ない。
    /// </summary>
    [InitializeOnLoad]
    public static class StageGameViewPreview
    {
        /// <summary>見ていないとき。</summary>
        public const int Off = -2;

        /// <summary>全体（観戦）の視点。</summary>
        public const int Overview = -1;

        private const string BloomProfilePath = "ToonGlowProfile";
        private const string BloomEnabledPrefKey = "MS2026.PostFx.ToonGlow.Enabled";
        private const string VolumeName = "[Preview] Game Look Bloom";

        private static int _viewer = Off;
        private static Camera _camera;
        private static Pose _savedPose;
        private static bool _savedOrthographic;
        private static float _savedFieldOfView;
        private static float _savedOrthographicSize;
        private static float _savedNear;
        private static float _savedFar;
        private static bool _savedPostProcessing;
        private static bool _savedToon;
        private static GameObject _volumeObject;
        private static bool _suspendedForSave;

        static StageGameViewPreview()
        {
            EditorSceneManager.sceneSaving += (scene, path) => SuspendForSave(scene);
            EditorSceneManager.sceneSaved += _ => ResumeAfterSave();
            EditorSceneManager.sceneClosing += (scene, removing) =>
            {
                if (_camera != null && _camera.gameObject.scene == scene)
                {
                    Stop();
                }
            };
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode)
                {
                    Stop();
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }

        /// <summary>今見ている席（<see cref="Off"/> なら見ていない）。</summary>
        public static int Viewer => _viewer;

        public static bool IsOn => _viewer != Off;

        /// <summary>その席の見え方にする（<see cref="Off"/> なら元に戻す）。できなかったら false。</summary>
        public static bool Show(int viewer)
        {
            if (viewer == Off)
            {
                Stop();
                return true;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }

            var preset = StageGameCamera.Preset;
            var camera = FindCamera();
            if (preset == null || camera == null)
            {
                return false;
            }

            if (_viewer == Off || _camera != camera)
            {
                Stop();
                Save(camera);
                StartExtras();
            }

            _viewer = viewer;
            Apply(preset);
            return true;
        }

        /// <summary>カメラ・仕上げ・トゥーンを全部元に戻す。</summary>
        public static void Stop()
        {
            if (_viewer == Off)
            {
                return;
            }

            RestoreCamera();
            if (_camera != null)
            {
                var data = _camera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = _savedPostProcessing;
                }
            }

            if (_volumeObject != null)
            {
                Object.DestroyImmediate(_volumeObject);
            }

            _volumeObject = null;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                MS2026.Fortress.LightRaysLook.EditorPreviewCamera = null;
                MS2026.Fortress.EdgeMistLook.EditorPreviewCamera = null;
            }

            SetToothRoll(float.NaN);
            if (ObjectToonEditPreview.IsOn != _savedToon && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ObjectToonEditPreview.IsOn = _savedToon;
            }

            _viewer = Off;
            _camera = null;
            RepaintGameView();
        }

        private static Camera FindCamera()
        {
            var rig = StageGameCamera.Rig;
            return rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
        }

        private static void Save(Camera camera)
        {
            _camera = camera;
            var transform = camera.transform;
            // 親から見た値をそのまま覚えて戻す（計算し直すと 0 が −0 になるなど、保存のたびにファイルが変わるのを防ぐ）。
            _savedPose = new Pose(transform.localPosition, transform.localRotation);
            _savedOrthographic = camera.orthographic;
            _savedFieldOfView = camera.fieldOfView;
            _savedOrthographicSize = camera.orthographicSize;
            _savedNear = camera.nearClipPlane;
            _savedFar = camera.farClipPlane;
            var data = camera.GetUniversalAdditionalCameraData();
            _savedPostProcessing = data != null && data.renderPostProcessing;
            _savedToon = ObjectToonEditPreview.IsOn;
        }

        private static void StartExtras()
        {
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
            }

            var profile = Resources.Load<VolumeProfile>(BloomProfilePath);
            if (profile != null && EditorPrefs.GetBool(BloomEnabledPrefKey, true))
            {
                // Play 中の ToonGlow と同じ設定（優先度 5）。シーンには保存しない隠しオブジェクト。
                _volumeObject = new GameObject(VolumeName) { hideFlags = HideFlags.HideAndDontSave };
                var volume = _volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 5f;
                volume.sharedProfile = profile;
            }

            ObjectToonEditPreview.IsOn = true;

            // Play 中だけの画面の演出のうち、光の筋と画面のふちの靄も描く（ステージの見た目の設定のまま）。
            MS2026.Fortress.LightRaysLook.EditorPreviewCamera = _camera;
            MS2026.Fortress.EdgeMistLook.EditorPreviewCamera = _camera;
        }

        private static void Apply(CameraViewPreset preset)
        {
            if (_camera == null)
            {
                return;
            }

            var view = preset.GetView(_viewer).Sanitized();
            CameraPoseApplier.Apply(_camera.transform, _camera, view, preset.nearClip, preset.farClip);

            // 歯のコアは、どの席からも 1P と同じ見え方になるように、その席の画面の回転だけ回す（Play 中と同じ）。
            SetToothRoll(view.rollDegrees);
            RepaintGameView();
        }

        private static void RestoreCamera()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.transform.SetLocalPositionAndRotation(_savedPose.position, _savedPose.rotation);
            _camera.orthographic = _savedOrthographic;
            _camera.fieldOfView = _savedFieldOfView;
            _camera.orthographicSize = _savedOrthographicSize;
            _camera.nearClipPlane = _savedNear;
            _camera.farClipPlane = _savedFar;
        }

        // 保存するファイルに、動かしたカメラの位置を書き込まない。
        private static void SuspendForSave(Scene scene)
        {
            if (_viewer == Off || _camera == null || _camera.gameObject.scene != scene)
            {
                return;
            }

            RestoreCamera();
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = _savedPostProcessing;
            }

            // 回した歯の向きもファイルに書き込まない。
            SetToothRoll(float.NaN);
            _suspendedForSave = true;
        }

        private static void ResumeAfterSave()
        {
            if (!_suspendedForSave)
            {
                return;
            }

            _suspendedForSave = false;
            var preset = StageGameCamera.Preset;
            if (_viewer == Off || _camera == null || preset == null)
            {
                return;
            }

            var data = _camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
            }

            Apply(preset);
        }

        private static void SetToothRoll(float roll)
        {
            MS2026.Fortress.CoreToothLook.EditorPreviewRoll = roll;
            MS2026.Fortress.CoreToothLook.RefreshFacingAll();
        }

        private static void RepaintGameView()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
