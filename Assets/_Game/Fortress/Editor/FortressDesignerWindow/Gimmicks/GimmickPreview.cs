using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 仕掛け（歯車・動く障害物）の編集中のプレビュー（2026-10-29）。
    /// ・<see cref="SetTime"/> で、シーンの仕掛けをその秒数の形にする（経路の予想ルートも、その形で計算し直される）
    /// ・<see cref="Stop"/> で 0 秒の形（＝シーンに保存される形）に戻す。保存するときは一時的に 0 秒の形に戻してから書く
    /// ・プレビューしていない間は、動く障害物を手で動かすと出発点（homePosition）が付いてくる
    /// Play を始めるときは自動でやめる。シーンには何も足さない。
    /// </summary>
    [InitializeOnLoad]
    public static class GimmickPreview
    {
        private const double SyncInterval = 0.2;

        private static double _lastSync;
        private static double _lastPlayTick;

        /// <summary>プレビュー中か。</summary>
        public static bool Active => GimmickClock.PreviewTime >= 0f;

        /// <summary>プレビューの秒数（していなければ 0）。</summary>
        public static float Time => Mathf.Max(0f, GimmickClock.PreviewTime);

        /// <summary>プレビューを時間で進めている（▶）か。</summary>
        public static bool Playing { get; private set; }

        /// <summary>▶ の速さ。</summary>
        public static float PlaySpeed { get; set; } = 1f;

        /// <summary>▶ でここまで来たら 0 に戻る。</summary>
        public static float Horizon { get; set; } = 60f;

        static GimmickPreview()
        {
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        /// <summary>シーンの仕掛けを t 秒の形にする（プレビュー開始）。</summary>
        public static void SetTime(float t)
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (!Active)
            {
                // 始める直前に手で動かした分も、出発点に入れておく。
                SyncHomes();
            }

            GimmickClock.PreviewTime = Mathf.Max(0f, t);
            ApplyToScenes(GimmickClock.PreviewTime, null);
        }

        /// <summary>プレビューをやめて、0 秒の形（保存される形）に戻す。</summary>
        public static void Stop()
        {
            Playing = false;
            if (!Active)
            {
                return;
            }

            GimmickClock.PreviewTime = -1f;
            if (!Application.isPlaying)
            {
                ApplyToScenes(0f, null);
            }
        }

        public static void TogglePlay()
        {
            if (Application.isPlaying)
            {
                return;
            }

            Playing = !Playing;
            _lastPlayTick = EditorApplication.timeSinceStartup;
            if (Playing && !Active)
            {
                SetTime(0f);
            }
        }

        /// <summary>シーン（scene が空なら開いている全部）の仕掛けを t 秒の形にし、経路の再計算を頼む。</summary>
        public static void ApplyToScenes(float t, Scene? scene)
        {
            var any = false;
            foreach (var gimmick in GimmickClock.FindAllInScenes())
            {
                if (scene.HasValue && gimmick.gameObject.scene != scene.Value)
                {
                    continue;
                }

                gimmick.ApplyAt(t);
                any = true;
            }

            if (!any)
            {
                return;
            }

            Physics2D.SyncTransforms();
            NavigationObstacle.NotifyChanged();
            SceneView.RepaintAll();
        }

        private static void OnUpdate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (Playing)
            {
                var dt = (float)(now - _lastPlayTick);
                _lastPlayTick = now;
                var next = Time + Mathf.Min(dt, 0.1f) * PlaySpeed;
                SetTime(next > Horizon ? 0f : next);
            }

            if (Active || now - _lastSync < SyncInterval)
            {
                return;
            }

            _lastSync = now;
            SyncHomes();
        }

        /// <summary>プレビューしていない間に、動く障害物を手で動かしたら、出発点をそこにする。</summary>
        private static void SyncHomes()
        {
            foreach (var gimmick in GimmickClock.FindAllInScenes())
            {
                if (!(gimmick is GimmickMover mover))
                {
                    continue;
                }

                mover.ReadPose(out var position, out var angle);
                if (mover.homeCaptured &&
                    (position - mover.homePosition).sqrMagnitude < 1e-8f &&
                    Mathf.Abs(Mathf.DeltaAngle(angle, mover.homeAngle)) < 1e-3f)
                {
                    continue;
                }

                var so = new SerializedObject(mover);
                so.FindProperty(nameof(GimmickMover.homePosition)).vector2Value = position;
                so.FindProperty(nameof(GimmickMover.homeAngle)).floatValue = angle;
                so.FindProperty(nameof(GimmickMover.homeCaptured)).boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                mover.MarkChanged();
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                Stop();
            }
        }

        private static readonly List<Scene> SavingScenes = new List<Scene>();

        private static void OnSceneSaving(Scene scene, string path)
        {
            if (!Active || Application.isPlaying)
            {
                return;
            }

            // 保存されるのは 0 秒の形（動く障害物は出発点、歯車は回転 0）。
            SavingScenes.Add(scene);
            ApplyToScenes(0f, scene);
        }

        private static void OnSceneSaved(Scene scene)
        {
            if (!SavingScenes.Remove(scene) || !Active)
            {
                return;
            }

            ApplyToScenes(Time, scene);
        }
    }
}
