using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 配置ページの「複製」「消す」。Unity の Ctrl+D / Delete と同じ仕組み（Edit メニューと同じ）を使うので、
    /// モデルの Prefab のつながり・通れない範囲・役割の設定もそのまま写り、Ctrl+Z で戻せる。
    /// </summary>
    public static class StagePropDuplicateOps
    {
        /// <summary>複製したときにずらす量（m）。元の物と重ならず、すぐ見分けられる程度。</summary>
        public const float Offset = 0.5f;

        /// <summary>選んでいる背景を複製して、少しずらして選ぶ。作った物を返す。</summary>
        public static List<StageProp> Duplicate(IReadOnlyList<StageProp> props)
        {
            var sources = props.Where(p => p != null).Select(p => p.gameObject).ToArray();
            if (sources.Length == 0)
            {
                return new List<StageProp>();
            }

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Selection.objects = sources;
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var copies = Selection.gameObjects
                .Select(go => go.GetComponent<StageProp>())
                .Where(p => p != null && !sources.Contains(p.gameObject))
                .ToList();
            foreach (var copy in copies)
            {
                Undo.RecordObject(copy.transform, "背景を複製");
                StagePropTransformOps.Move(copy, new Vector2(Offset, -Offset));
            }

            Undo.SetCurrentGroupName("背景を複製");
            Undo.CollapseUndoOperations(group);
            StagePropTransformOps.Commit();
            StageGameCamera.MarkDirty();
            return copies;
        }

        /// <summary>選んでいる背景を消す（確認つき。Ctrl+Z で戻せる）。消したら true。</summary>
        public static bool Delete(IReadOnlyList<StageProp> props)
        {
            var targets = props.Where(p => p != null).Select(p => p.gameObject).ToArray();
            if (targets.Length == 0)
            {
                return false;
            }

            var names = targets.Length == 1 ? $"「{targets[0].name}」" : $"「{targets[0].name}」ほか{targets.Length - 1}個";
            if (!EditorUtility.DisplayDialog("背景を消す", $"{names}をシーンから消します。\n元に戻すは Ctrl+Z。ステージに残すには、あとで「ステージに保存」を押します。", "消す", "やめる"))
            {
                return false;
            }

            Selection.objects = targets;
            Unsupported.DeleteGameObjectSelection();
            StagePropTransformOps.Commit();
            StageGameCamera.MarkDirty();
            return true;
        }
    }
}
