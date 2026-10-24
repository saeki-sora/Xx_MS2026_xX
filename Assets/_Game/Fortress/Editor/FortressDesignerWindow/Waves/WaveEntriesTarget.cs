using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 表で編集する「湧きの行の並び」と、その持ち主（時間割のアセット、またはウェーブの計画の中の1ウェーブ）。
    /// 表・タイムライン・便利ボタンは、この形だけを相手にする（どのアセットの行かを気にしない）。
    /// </summary>
    public sealed class WaveEntriesTarget
    {
        public WaveEntriesTarget(Object owner, SerializedObject serialized, string propertyPath, List<EnemySpawnEntry> list, string label)
        {
            Owner = owner;
            Serialized = serialized;
            PropertyPath = propertyPath;
            List = list ?? new List<EnemySpawnEntry>();
            Label = label;
        }

        /// <summary>Undo・保存の印を付けるアセット。</summary>
        public Object Owner { get; }

        public SerializedObject Serialized { get; }

        /// <summary>行の並びのプロパティの場所（例: "spawnEntries"、"waves.Array.data[2].spawnEntries"）。</summary>
        public string PropertyPath { get; }

        /// <summary>行の並びそのもの（直接書き換えるとき用。書き換えは <see cref="Modify"/> の中で）。</summary>
        public List<EnemySpawnEntry> List { get; }

        /// <summary>「Wave 2」などの名前（確認の文などに使う）。</summary>
        public string Label { get; }

        public SerializedProperty Property => Serialized.FindProperty(PropertyPath);

        /// <summary>
        /// 直接書き換える（Ctrl+Z で戻せる）。表の欄で変えた分を先に書き込んでから書き換え、表を読み直す。
        /// </summary>
        public void Modify(string undoName, Action change)
        {
            Serialized.ApplyModifiedProperties();
            Undo.RecordObject(Owner, undoName);
            change();
            EditorUtility.SetDirty(Owner);
            Serialized.Update();
            GUI.FocusControl(null);
        }

        /// <summary>同じ持ち主・同じ場所か（表を作り直すかどうかの判定）。</summary>
        public bool SameAs(WaveEntriesTarget other) =>
            other != null && other.Owner == Owner && other.PropertyPath == PropertyPath && ReferenceEquals(other.Serialized, Serialized);
    }
}
