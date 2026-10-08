using System.Collections.Generic;
using DDrive.Editor.Preview;
using DDrive.Foundation.Handle;
using DDrive.Runtime.Ui;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 編集中（Playしていないとき）に、D-Drive の UI の動きをその場で再生して見せる。
    /// 手元に D-Drive の動きの計算役（UiTweenManager）を作ってエディタの時間で進め、終わったら位置・大きさ・回転・濃さ・色を元に戻す。
    /// 画面の「出る／消える」は、子の部品の動き（遅れも含む）をまとめて再生する。
    /// </summary>
    public static class UiMotionPreview
    {
        private struct Snapshot
        {
            public Vector2 AnchoredPosition;
            public Vector3 Scale;
            public Vector3 Euler;
            public Vector2 SizeDelta;
            public bool HadGroup;
            public float Alpha;
            public Graphic Graphic;
            public Color Color;
        }

        private struct Scheduled
        {
            public UiMotion Motion;
            public RectTransform Target;
            public double At;
        }

        private const double RestoreDelay = 0.35;

        private static readonly Dictionary<RectTransform, Snapshot> Snapshots = new Dictionary<RectTransform, Snapshot>();
        private static readonly List<Scheduled> Pending = new List<Scheduled>();
        private static readonly List<Handle<UiTweenMarker>> Running = new List<Handle<UiTweenMarker>>();
        private static readonly TweenTrack[] Buffer = new TweenTrack[UiTweenManager.MaxTracksPerTween];
        private static UiTweenManager _manager;
        private static double _last;
        private static double _idleSince = -1;
        private static bool _hooked;

        public static bool IsPlaying => Pending.Count > 0 || Running.Count > 0;

        /// <summary>1つの動きを試す。</summary>
        public static void Play(UiMotion motion, RectTransform target)
        {
            if (Application.isPlaying)
            {
                UiMotionPlayers.Current.Play(motion, target);
                return;
            }

            Stop();
            Schedule(motion, target, 0f);
        }

        /// <summary>画面全体の「出る」（show=true）か「消える」を、子の動きも含めて試す。</summary>
        public static void PlayScreen(UiScreen screen, bool show)
        {
            if (screen == null)
            {
                return;
            }

            Stop();
            var root = (RectTransform)screen.transform;
            var rootMotion = show ? screen.showMotion : screen.hideMotion;
            if (!rootMotion.IsEmpty)
            {
                Schedule(rootMotion, root, 0f);
            }

            var trigger = show ? UiMotionTrigger.OnShow : UiMotionTrigger.OnHide;
            foreach (var element in screen.GetComponentsInChildren<UiElementMotion>(false))
            {
                foreach (var entry in element.entries)
                {
                    if (entry != null && entry.trigger == trigger && !entry.motion.IsEmpty)
                    {
                        Schedule(entry.motion, entry.target != null ? entry.target : (RectTransform)element.transform, 0f);
                    }
                }
            }
        }

        /// <summary>止めて、全部元に戻す。</summary>
        public static void Stop()
        {
            if (_manager != null)
            {
                foreach (var handle in Running)
                {
                    _manager.Stop(handle, false);
                }
            }

            Running.Clear();
            Pending.Clear();
            RestoreAll();
        }

        private static void Schedule(UiMotion motion, RectTransform target, float extraDelay)
        {
            if (target == null)
            {
                return;
            }

            Hook();
            Remember(target);
            Pending.Add(new Scheduled { Motion = motion, Target = target, At = EditorApplication.timeSinceStartup + motion.delay + extraDelay });
            _idleSince = -1;
        }

        private static void Hook()
        {
            _manager ??= new UiTweenManager(EditorAnchorRegistry.Build());
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            _last = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
        }

        private static void Tick()
        {
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _last);
            _last = now;

            for (var i = Pending.Count - 1; i >= 0; i--)
            {
                if (Pending[i].At <= now)
                {
                    var item = Pending[i];
                    Pending.RemoveAt(i);
                    Start(item.Motion, item.Target);
                }
            }

            if (_manager != null && dt > 0f && dt < 1f)
            {
                _manager.Tick(dt);
            }

            Running.RemoveAll(h => !_manager.IsPlaying(h));
            if (Snapshots.Count > 0)
            {
                SceneView.RepaintAll();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            if (!IsPlaying && Snapshots.Count > 0)
            {
                if (_idleSince < 0)
                {
                    _idleSince = now;
                }
                else if (now - _idleSince > RestoreDelay)
                {
                    RestoreAll();
                }
            }
        }

        private static void Start(UiMotion motion, RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            Handle<UiTweenMarker> handle;
            if (motion.source == UiMotionSource.Preset)
            {
                var count = UiPresetFactory.Build(motion.preset, target, Buffer);
                handle = count > 0 ? _manager.PlayTracks(Buffer, count, target) : Handle<UiTweenMarker>.Invalid;
            }
            else
            {
                var data = FindTween(motion.tween.Value);
                handle = data != null ? _manager.PlayData(data, target) : Handle<UiTweenMarker>.Invalid;
            }

            if (_manager.IsPlaying(handle))
            {
                Running.Add(handle);
            }
        }

        private static UiTweenData FindTween(ulong id)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(UiTweenData)))
            {
                var data = AssetDatabase.LoadAssetAtPath<UiTweenData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null && data.Id == id)
                {
                    return data;
                }
            }

            return null;
        }

        private static void Remember(RectTransform target)
        {
            if (Snapshots.ContainsKey(target))
            {
                return;
            }

            var group = target.GetComponent<CanvasGroup>();
            var graphic = target.GetComponent<Graphic>();
            Snapshots[target] = new Snapshot
            {
                AnchoredPosition = target.anchoredPosition,
                Scale = target.localScale,
                Euler = target.localEulerAngles,
                SizeDelta = target.sizeDelta,
                HadGroup = group != null,
                Alpha = group != null ? group.alpha : 1f,
                Graphic = graphic,
                Color = graphic != null ? graphic.color : Color.white
            };
        }

        private static void RestoreAll()
        {
            foreach (var pair in Snapshots)
            {
                var target = pair.Key;
                if (target == null)
                {
                    continue;
                }

                var snap = pair.Value;
                target.anchoredPosition = snap.AnchoredPosition;
                target.localScale = snap.Scale;
                target.localEulerAngles = snap.Euler;
                target.sizeDelta = snap.SizeDelta;
                var group = target.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    if (snap.HadGroup)
                    {
                        group.alpha = snap.Alpha;
                    }
                    else
                    {
                        Object.DestroyImmediate(group);
                    }
                }

                if (snap.Graphic != null)
                {
                    snap.Graphic.color = snap.Color;
                }
            }

            Snapshots.Clear();
            _idleSince = -1;
            SceneView.RepaintAll();
        }
    }
}
