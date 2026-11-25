using System;
using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>
    /// Play 中にゲームで鳴った欄を見つけて、ツールの行とナビの場面を少し光らせる（鳴るはずなのに鳴らない欄が分かる）。エディタだけ。
    /// 設定ファイルの欄は鳴った FortressEffect がそのまま同じ物なので、それで引く。シーンの欄は Play 中は別の物なので、
    /// Play に入ったときとシーンを読んだときに、部品の場所（GlobalObjectId）と欄の道筋で対応表を作り直す。
    /// </summary>
    public sealed class PlayedSlotIndex : IDisposable
    {
        private const double RebuildCooldown = 2;

        private readonly SoundStudioContext _context;
        private readonly StudioShell _shell;
        private readonly Dictionary<FortressEffect, SoundSlot> _map = new Dictionary<FortressEffect, SoundSlot>();
        private readonly Queue<(FortressEffect effect, double time)> _played = new Queue<(FortressEffect, double)>();
        private readonly Dictionary<SoundPageId, double> _navFlashUntil = new Dictionary<SoundPageId, double>();
        private int _mapVersion = -1;
        private double _nextRebuild;
        private bool _dirty = true;

        public PlayedSlotIndex(SoundStudioContext context, StudioShell shell)
        {
            _context = context;
            _shell = shell;
            FortressEffectPlayer.Played += OnPlayed;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public void Dispose()
        {
            FortressEffectPlayer.Played -= OnPlayed;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EditorApplication.playModeStateChanged -= OnPlayMode;
        }

        /// <summary>窓が定期的に呼ぶ。たまった「鳴った」を行に伝える。</summary>
        public void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                _played.Clear();
                return;
            }

            if (_dirty || _mapVersion != _context.Version)
            {
                Rebuild();
            }

            var missed = false;
            while (_played.Count > 0)
            {
                var (effect, time) = _played.Dequeue();
                if (_map.TryGetValue(effect, out var slot))
                {
                    Light(slot);
                    _context.Stats.Record(slot.Key, time);
                }
                else
                {
                    missed = true;
                }
            }

            // 後から出てきた物（Play 中に作った砲台など）の欄は、見つからなければ時々作り直して探す。
            if (missed && EditorApplication.timeSinceStartup >= _nextRebuild)
            {
                _dirty = true;
            }

            UpdateNav();
        }

        private void OnPlayed(FortressEffect effect, int playerIndex)
        {
            // 鳴った時刻はここで覚える（行に伝えるのは 0.25 秒ごとなので、そのときの時刻だと1秒の回数がずれる）。
            if (effect != null && _played.Count < 4096)
            {
                _played.Enqueue((effect, EditorApplication.timeSinceStartup));
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _dirty = true;

        private void OnPlayMode(PlayModeStateChange change) => _dirty = true;

        private void Rebuild()
        {
            _dirty = false;
            _mapVersion = _context.Version;
            _nextRebuild = EditorApplication.timeSinceStartup + RebuildCooldown;
            _map.Clear();

            var byPlace = new Dictionary<string, SoundSlot>();
            var byField = new Dictionary<string, SoundSlot>();
            foreach (var slot in _context.Slots)
            {
                if (slot.Kind != SoundSlotKind.Effect)
                {
                    continue;
                }

                if (slot.Place == SoundSlotPlace.Asset && slot.Owner != null)
                {
                    foreach (var found in SlotPathFinder.FindSlots(slot.Owner))
                    {
                        if (found.Path == slot.PropertyPath && found.Value is FortressEffect effect)
                        {
                            _map[effect] = slot;
                        }
                    }
                }

                if (slot.GlobalObjectId != null)
                {
                    byPlace[slot.GlobalObjectId + "|" + slot.PropertyPath] = slot;
                }

                var fieldKey = slot.DeclaringType?.FullName + "|" + slot.PropertyPath;
                if (!byField.ContainsKey(fieldKey))
                {
                    byField[fieldKey] = slot;
                }
            }

            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || !SlotPathFinder.MayHoldSlots(behaviour.GetType()))
                {
                    continue;
                }

                var id = GlobalObjectId.GetGlobalObjectIdSlow(behaviour).ToString();
                foreach (var found in SlotPathFinder.FindSlots(behaviour))
                {
                    if (!(found.Value is FortressEffect effect) || _map.ContainsKey(effect))
                    {
                        continue;
                    }

                    // シーンに置いてある物は場所で、Play 中に作った物（Prefab から出した砲台など）は欄の種類で引く。
                    if (byPlace.TryGetValue(id + "|" + found.Path, out var slot)
                        || byField.TryGetValue(found.DeclaringType.FullName + "|" + found.Path, out slot))
                    {
                        _map[effect] = slot;
                    }
                }
            }
        }

        private void Light(SoundSlot slot)
        {
            _navFlashUntil[slot.Page] = EditorApplication.timeSinceStartup + 0.9;
            if (_shell.Current is SoundScenePage page)
            {
                page.Flash(slot);
            }
        }

        private void UpdateNav()
        {
            var items = _shell.Query(className: "sk-nav-item").ToList();
            var now = EditorApplication.timeSinceStartup;
            var index = 0;
            foreach (SoundPageId id in Enum.GetValues(typeof(SoundPageId)))
            {
                if (index >= items.Count)
                {
                    break;
                }

                items[index].EnableInClassList("ss-nav--flash", _navFlashUntil.TryGetValue(id, out var until) && until > now);
                index++;
            }
        }
    }
}
