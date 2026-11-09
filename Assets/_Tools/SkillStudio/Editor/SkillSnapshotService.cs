using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Skills;
using UnityEditor;
using UnityEngine;

namespace MS2026.SkillStudio
{
    /// <summary>どの範囲を読み込むか。</summary>
    public enum SnapshotScope
    {
        All,
        Level,
        Look
    }

    /// <summary>
    /// 設定の保存・読み込み（<see cref="SkillSnapshotStore"/>）。読み込みは元に戻す（Ctrl+Z）ことができる。
    /// 「レベルデザインだけ」「見た目だけ」のときは、その欄の設定のまとまりだけを書き戻す（もう片方の欄は今のまま）。
    /// </summary>
    public sealed class SkillSnapshotService
    {
        private SkillSnapshotStore _store;

        /// <summary>最後に読み込んだ印（A/B の切り替え用）。スキルごと。</summary>
        private readonly Dictionary<CharacterSkill, string> _lastMark = new Dictionary<CharacterSkill, string>();

        public SkillSnapshotStore Store
        {
            get
            {
                if (_store == null)
                {
                    _store = AssetDatabase.LoadAssetAtPath<SkillSnapshotStore>(SkillSnapshotStore.AssetPath);
                }

                return _store;
            }
        }

        public IEnumerable<SkillSnapshotStore.Entry> EntriesOf(CharacterSkill skill)
        {
            var store = Store;
            return store == null || skill == null
                ? Enumerable.Empty<SkillSnapshotStore.Entry>()
                : store.entries.Where(e => e != null && e.skill == skill).Reverse();
        }

        public SkillSnapshotStore.Entry Marked(CharacterSkill skill, string mark)
        {
            return EntriesOf(skill).FirstOrDefault(e => e.mark == mark);
        }

        public string LastMark(CharacterSkill skill) => skill != null && _lastMark.TryGetValue(skill, out var mark) ? mark : null;

        /// <summary>今の値を保存する。<paramref name="mark"/> が A/B なら、同じスキルの同じ印の古い物から印を外す。</summary>
        public SkillSnapshotStore.Entry Save(CharacterSkill skill, string name, string mark = "")
        {
            if (skill == null)
            {
                return null;
            }

            var store = EnsureStore();
            Undo.RecordObject(store, "スキルの設定を保存");
            if (!string.IsNullOrEmpty(mark))
            {
                foreach (var old in store.entries.Where(e => e != null && e.skill == skill && e.mark == mark))
                {
                    old.mark = "";
                }
            }

            var entry = new SkillSnapshotStore.Entry
            {
                skill = skill,
                name = string.IsNullOrWhiteSpace(name) ? DefaultName(mark) : name.Trim(),
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                mark = mark ?? "",
                json = EditorJsonUtility.ToJson(skill)
            };
            store.entries.Add(entry);
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssetIfDirty(store);
            if (!string.IsNullOrEmpty(mark))
            {
                _lastMark[skill] = mark;
            }

            return entry;
        }

        public void Delete(SkillSnapshotStore.Entry entry)
        {
            var store = Store;
            if (store == null || entry == null)
            {
                return;
            }

            Undo.RecordObject(store, "保存した設定を消す");
            store.entries.Remove(entry);
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssetIfDirty(store);
        }

        public void SetMark(SkillSnapshotStore.Entry entry, string mark)
        {
            var store = Store;
            if (store == null || entry == null)
            {
                return;
            }

            Undo.RecordObject(store, "比べる印を付ける");
            foreach (var other in store.entries.Where(e => e != null && e.skill == entry.skill && e.mark == mark))
            {
                other.mark = "";
            }

            entry.mark = mark;
            EditorUtility.SetDirty(store);
            AssetDatabase.SaveAssetIfDirty(store);
        }

        /// <summary>保存した値をスキルの設定ファイルに書き戻す（元に戻せる）。</summary>
        public void Apply(SkillSnapshotStore.Entry entry, SnapshotScope scope)
        {
            var skill = entry?.skill;
            if (skill == null || string.IsNullOrEmpty(entry.json))
            {
                return;
            }

            if (scope == SnapshotScope.All)
            {
                Undo.RecordObject(skill, "保存した設定を読み込む");
                EditorJsonUtility.FromJsonOverwrite(entry.json, skill);
                EditorUtility.SetDirty(skill);
            }
            else
            {
                ApplyPartial(skill, entry.json, scope);
            }

            if (!string.IsNullOrEmpty(entry.mark))
            {
                _lastMark[skill] = entry.mark;
            }
        }

        /// <summary>A と B を切り替える（最後に読み込んだのが A なら B、それ以外なら A）。どちらかが無ければ false。</summary>
        public bool ToggleAB(CharacterSkill skill)
        {
            var next = LastMark(skill) == "A" ? "B" : "A";
            var entry = Marked(skill, next);
            if (entry == null)
            {
                next = next == "A" ? "B" : "A";
                entry = Marked(skill, next);
            }

            if (entry == null)
            {
                return false;
            }

            Apply(entry, SnapshotScope.All);
            return true;
        }

        private static void ApplyPartial(CharacterSkill skill, string json, SnapshotScope scope)
        {
            var temp = ScriptableObject.CreateInstance(skill.GetType());
            try
            {
                EditorJsonUtility.FromJsonOverwrite(json, temp);
                var source = new SerializedObject(temp);
                var target = new SerializedObject(skill);
                var tab = scope == SnapshotScope.Level ? SkillTab.Level : SkillTab.Look;
                var names = SkillTabs.Groups(skill.GetType(), tab).Select(f => f.Name).ToList();
                if (scope == SnapshotScope.Level)
                {
                    names.AddRange(SkillTabs.ChargeFields);
                    names.AddRange(SkillTabs.LaserFields);
                }

                foreach (var name in names)
                {
                    var property = source.FindProperty(name);
                    if (property != null)
                    {
                        target.CopyFromSerializedProperty(property);
                    }
                }

                target.ApplyModifiedProperties();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private SkillSnapshotStore EnsureStore()
        {
            var store = Store;
            if (store != null)
            {
                return store;
            }

            var folder = Path.GetDirectoryName(SkillSnapshotStore.AssetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            store = ScriptableObject.CreateInstance<SkillSnapshotStore>();
            AssetDatabase.CreateAsset(store, SkillSnapshotStore.AssetPath);
            _store = store;
            return store;
        }

        private static string DefaultName(string mark)
        {
            var time = DateTime.Now.ToString("MM/dd HH:mm");
            return string.IsNullOrEmpty(mark) ? time : $"{mark}（{time}）";
        }
    }
}
