using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// Play 中の変更を、Play を止めたあとに Prefab ファイル／シーンへ書き戻す。
    /// ・Prefab: 中身を読み込んで書き、保存する（その Prefab を編集画面で開いていれば、そこに書く）
    /// ・シーン: 開いていればそこに書き（Ctrl+Z で戻せる。保存で確定）、開いていなければ一時的に開いて書いて保存して閉じる
    /// 書き戻せた変更は一覧から消え、書き戻せなかった物は理由を付けて一覧に残る。
    /// </summary>
    public static class UiPlayChangeApplier
    {
        public sealed class Report
        {
            public int Applied;
            public int Failed;
            public readonly List<string> Destinations = new List<string>();
            public readonly List<string> Problems = new List<string>();

            public string Summary =>
                Applied == 0 && Failed == 0 ? "書き戻す変更がありませんでした。"
                : $"{Applied} か所を書き戻しました" + (Destinations.Count > 0 ? $"（{string.Join("、", Destinations)}）" : "") +
                  (Failed > 0 ? $"。{Failed} か所は書き戻せませんでした（理由は一覧に出ています）。" : "。");

            public void Log()
            {
                if (Applied > 0)
                {
                    Debug.Log($"[UIスタジオ] Play 中の変更: {Summary}");
                }

                if (Failed > 0)
                {
                    Debug.LogWarning($"[UIスタジオ] Play 中の変更を {Failed} か所 書き戻せませんでした:\n{string.Join("\n", Problems)}");
                }
            }
        }

        private const string UndoName = "Play中の変更を残す";

        /// <summary>一覧でチェックが付いている（残せる）変更を書き戻す。</summary>
        public static Report ApplyKept()
        {
            var selected = new List<UiPlayChange>();
            foreach (var change in UiPlayChangeStore.Items)
            {
                if (change.keep && change.CanApply)
                {
                    selected.Add(change);
                }
            }

            return Apply(selected);
        }

        public static Report Apply(IReadOnlyList<UiPlayChange> changes)
        {
            var report = new Report();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Problems.Add("Play 中は書き戻せません。Play を止めてから押してください。");
                return report;
            }

            var applied = new HashSet<UiPlayChange>();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            var undoGroup = Undo.GetCurrentGroup();
            foreach (var batch in UiPlayChangeList.GroupByDestination(changes))
            {
                try
                {
                    if (batch[0].target.destination == UiPlayDestination.Prefab)
                    {
                        ApplyToPrefab(batch, applied, report);
                    }
                    else
                    {
                        ApplyToScene(batch, applied, report);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    foreach (var change in batch)
                    {
                        if (!applied.Contains(change))
                        {
                            Fail(change, $"書き戻し中にエラーが出ました（{e.Message}）。", report);
                        }
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            report.Applied = applied.Count;
            UiPlayChangeStore.RemoveAll(applied.Contains);
            return report;
        }

        private static void ApplyToPrefab(List<UiPlayChange> batch, HashSet<UiPlayChange> applied, Report report)
        {
            var path = batch[0].target.assetPath;
            var name = Path.GetFileName(path);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                if (ApplyAll(stage.prefabContentsRoot.transform, batch, true, applied, report) > 0)
                {
                    EditorSceneManager.MarkSceneDirty(stage.scene);
                    report.Destinations.Add($"{name}（開いている編集画面。保存で確定）");
                }

                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                foreach (var change in batch)
                {
                    Fail(change, $"Prefab ファイル（{path}）が見つかりません。", report);
                }

                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ApplyAll(root.transform, batch, false, applied, report) > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.Destinations.Add(name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyToScene(List<UiPlayChange> batch, HashSet<UiPlayChange> applied, Report report)
        {
            var path = batch[0].target.assetPath;
            var name = Path.GetFileNameWithoutExtension(path);
            var scene = SceneManager.GetSceneByPath(path);
            var opened = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    foreach (var change in batch)
                    {
                        Fail(change, $"シーン（{path}）が見つかりません。", report);
                    }

                    return;
                }

                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                var count = 0;
                var byAnchor = new Dictionary<string, List<UiPlayChange>>();
                var order = new List<string>();
                foreach (var change in batch)
                {
                    if (!byAnchor.TryGetValue(change.target.anchorId, out var list))
                    {
                        byAnchor[change.target.anchorId] = list = new List<UiPlayChange>();
                        order.Add(change.target.anchorId);
                    }

                    list.Add(change);
                }

                foreach (var anchorId in order)
                {
                    var anchor = FindSceneAnchor(scene, anchorId);
                    if (anchor == null)
                    {
                        var reason = string.IsNullOrEmpty(anchorId)
                            ? $"シーン「{name}」に [UI] が見つかりません。"
                            : $"シーン「{name}」に画面「{anchorId}」が見つかりません。";
                        foreach (var change in byAnchor[anchorId])
                        {
                            Fail(change, reason, report);
                        }

                        continue;
                    }

                    count += ApplyAll(anchor, byAnchor[anchorId], !opened, applied, report);
                }

                if (count > 0)
                {
                    if (opened)
                    {
                        EditorSceneManager.SaveScene(scene);
                        report.Destinations.Add($"{name}.unity（開いて保存）");
                    }
                    else
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        report.Destinations.Add($"{name}.unity（保存で確定）");
                    }
                }
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static Transform FindSceneAnchor(Scene scene, string anchorId)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (string.IsNullOrEmpty(anchorId))
                {
                    var root = go.GetComponentInChildren<UiRoot>(true);
                    if (root != null)
                    {
                        return root.transform;
                    }

                    continue;
                }

                foreach (var screen in go.GetComponentsInChildren<UiScreen>(true))
                {
                    if (screen.screenId == anchorId)
                    {
                        return screen.transform;
                    }
                }
            }

            return null;
        }

        /// <summary>目印の下に、変更をまとめて書く。書けた数を返す。</summary>
        private static int ApplyAll(Transform anchor, List<UiPlayChange> changes, bool undo, HashSet<UiPlayChange> applied, Report report)
        {
            var count = 0;
            var serialized = new Dictionary<Object, SerializedObject>();
            foreach (var change in UiPlayChangeList.ApplyOrder(changes))
            {
                var go = UiPlayLocator.FindGameObject(anchor, change.target);
                if (go == null)
                {
                    Fail(change, "部品が見つかりません（名前を変えたか、Play 中に作られた部品です）。", report);
                    continue;
                }

                var target = UiPlayLocator.FindTarget(go, change.target, true, undo, out var problem);
                if (target == null)
                {
                    Fail(change, problem, report);
                    continue;
                }

                if (!serialized.TryGetValue(target, out var so))
                {
                    serialized[target] = so = new SerializedObject(target);
                }

                so.Update();
                var property = so.FindProperty(change.propertyPath);
                if (property == null)
                {
                    Fail(change, "この欄が見つかりません（部品の作りが変わったか、配列の数が合いません）。", report);
                    continue;
                }

                var reference = change.objectValue;
                if (change.referencesLocal)
                {
                    var referenceGo = UiPlayLocator.FindGameObject(anchor, change.reference);
                    reference = referenceGo != null ? UiPlayLocator.FindTarget(referenceGo, change.reference, false, false, out _) : null;
                    if (reference == null)
                    {
                        Fail(change, $"指している部品「{change.reference.where}」が見つかりません。", report);
                        continue;
                    }
                }

                if (!UiPlayValueIo.TryWrite(property, change.value, reference, out problem))
                {
                    Fail(change, problem, report);
                    continue;
                }

                // 配列の数を変えたら、続く要素の欄を探せるよう、その場で反映する。
                if (undo)
                {
                    so.ApplyModifiedProperties();
                }
                else
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                applied.Add(change);
                count++;
            }

            return count;
        }

        private static void Fail(UiPlayChange change, string reason, Report report)
        {
            change.problem = reason;
            report.Failed++;
            report.Problems.Add($"・{change.target.where} › {change.target.componentLabel} › {change.label}: {reason}");
        }
    }
}
