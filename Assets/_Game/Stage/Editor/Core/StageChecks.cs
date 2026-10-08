using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// ステージの点検。プランナーが気づきにくい「遊べなくなる・見た目が崩れる」原因を探し、直し方を添える。
    /// 重い計算（経路）は編集中だけ行い、結果は StageStudioContext が少し間隔をあけて取り直す。
    /// </summary>
    public static class StageChecks
    {
        public static IReadOnlyList<StudioIssue> Run(StageStudioContext context)
        {
            var issues = new List<StudioIssue>();
            var root = context.Root;
            if (root == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Error, "シーンにステージの置き場所がありません",
                        "背景はシーンの「[Stage]」（StageRoot）の下に置きます。ボタンで作れます。")
                    .WithFix("置き場所を作る", () => StageSceneService.CreateRoot()));
                return issues;
            }

            if (root.current == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Warning, "ステージが選ばれていません",
                    "上の帯でステージを選ぶか、「＋ 新しいステージ」で作ってください。", root));
                return issues;
            }

            if (root.instance == null)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Warning, $"「{root.current.Label}」がシーンに出ていません",
                        "ステージのPrefabがシーンに置かれていません。", root)
                    .WithFix("シーンに出す", () => StageSceneService.PlaceStage(root, root.current)));
                return issues;
            }

            var props = context.Props;
            if (props.Count == 0)
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Info, "まだ背景オブジェクトがありません",
                    "「取り込み」ページで Maya のモデル（FBX）を入れてください。", root));
            }

            CheckProps(props, issues);
            CheckOverlapsWithGameObjects(props, issues);
            CheckGaps(props, issues);
            if (!Application.isPlaying)
            {
                CheckReachability(issues);
            }

            if (StageSceneService.HasUnsavedChanges(root))
            {
                issues.Add(new StudioIssue(StudioIssueSeverity.Info, "ステージに保存していない変更があります",
                        "シーンで直した内容は、保存するまでステージ（Prefab）には入りません。シーンを保存しても入りません。", root.instance)
                    .WithFix("ステージに保存", () => StageSceneService.SaveToPrefab(root)));
            }

            issues.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            return issues;
        }

        private static void CheckProps(IReadOnlyList<StageProp> props, List<StudioIssue> issues)
        {
            foreach (var prop in props)
            {
                if (prop == null)
                {
                    continue;
                }

                var name = prop.name;
                if (prop.visualRoot == null || prop.footprint == null || prop.GetComponent<StagePropRenderer>() == null)
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"{name}: 部品が足りません", "背景オブジェクトに必要な子や部品が欠けています。", prop)
                        .WithFix("足りない部品を足す", () => StagePropComposer.EnsureStructure(prop)));
                    continue;
                }

                var euler = prop.transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(euler.y, 0f)) > 0.01f)
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"{name}: 根元が傾いています",
                            "根元（一番上の物）は床の上で回す（Z軸だけ）ようにしてください。傾けると敵の通れない範囲がずれます。見た目を傾けたいときは Visual の下のモデルを回します。", prop)
                        .WithFix("根元をまっすぐにする", () => StraightenRoot(prop)));
                }

                if (prop.role.NeedsFootprint())
                {
                    if (!prop.footprint.HasShape)
                    {
                        issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"{name}: 通れない範囲がありません",
                                "役割が「" + prop.role.DisplayName() + "」なのに形がありません。床から「壁とみなす高さ」までに形が無いと空になります。", prop)
                            .WithFix("作り直す", () => StagePropComposer.RebuildFootprint(prop)));
                    }
                    else if (!Application.isPlaying && prop.footprint.IsStale(prop.modelRoot != null ? prop.modelRoot : prop.visualRoot))
                    {
                        issues.Add(new StudioIssue(StudioIssueSeverity.Warning, $"{name}: 通れない範囲が古いままです",
                                "モデルの形・置き方・設定が変わりました。作り直すと今の形に合います。", prop)
                            .WithFix("作り直す", () => StagePropComposer.RebuildFootprint(prop)));
                    }
                }

                var unconverted = StageMaterialConverter.CountUnconverted(prop);
                if (unconverted > 0)
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Info, $"{name}: 専用シェーダーでないマテリアルが {unconverted} 個",
                            "このままでも表示はされますが、透け・焦げ・光り・壊れて溶ける・敵の影が出ません。", prop)
                        .WithFix("切り替える", () => ConvertMaterials(prop)));
                }
            }
        }

        /// <summary>砲台・コア・湧き位置が、壁の中に埋まっていないか。</summary>
        private static void CheckOverlapsWithGameObjects(IReadOnlyList<StageProp> props, List<StudioIssue> issues)
        {
            var points = new List<(string label, Vector2 position, Object target)>();
            foreach (var turret in Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                points.Add(($"P{turret.playerIndex + 1}の砲台", turret.transform.position, turret));
            }

            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                points.Add(("コア", core.transform.position, core));
            }

            foreach (var spawn in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                points.Add(($"湧き位置 {spawn.ResolvedId}", spawn.transform.position, spawn));
            }

            foreach (var prop in props)
            {
                if (prop == null || !StageGapFinder.BlocksEnemies(prop) && prop.role != StagePropRole.SlowZone)
                {
                    continue;
                }

                foreach (var path in StageGapFinder.WorldPaths(prop))
                {
                    foreach (var point in points)
                    {
                        if (FootprintGeometry.Contains(path, point.position))
                        {
                            issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"{prop.name} が{point.label}にかぶっています",
                                $"{point.label}が「{prop.name}」の通れない範囲の中にあります。背景を動かすか、{point.label}を動かしてください。", prop));
                        }
                    }
                }
            }
        }

        private static void CheckGaps(IReadOnlyList<StageProp> props, List<StudioIssue> issues)
        {
            var diameter = StageGapFinder.LargestEnemyDiameter();
            var field = Object.FindFirstObjectByType<NavigationField>();
            foreach (var gap in StageGapFinder.Find(props))
            {
                if (!gap.PassableOnGrid)
                {
                    var issue = new StudioIssue(StudioIssueSeverity.Warning,
                        $"{gap.A.name} と {gap.B.name} のすき間を敵は通りません（幅 {gap.Width:0.00}）",
                        $"すき間が経路のマス目（{(field != null ? field.cellSize : 0f):0.##}）より狭いため、経路の上では塞がっています。通したいなら背景を少し離すか、経路のマス目を細かくします（細かいほど計算は重くなります）。",
                        gap.A);
                    if (field != null && field.cellSize > 0.26f)
                    {
                        issue.WithFix("マス目を0.25にする", () =>
                        {
                            Undo.RecordObject(field, "経路のマス目を細かくする");
                            field.cellSize = 0.25f;
                            field.MarkDirty();
                            EditorUtility.SetDirty(field);
                        });
                    }

                    issues.Add(issue);
                }
                else if (gap.Width < diameter)
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Warning,
                        $"{gap.A.name} と {gap.B.name} のすき間が敵より狭いです（幅 {gap.Width:0.00} / 敵 {diameter:0.00}）",
                        "群衆はここに押し寄せて詰まります。わざと詰まらせるなら問題ありません。", gap.A));
                }
            }
        }

        private static void CheckReachability(List<StudioIssue> issues)
        {
            var field = Object.FindFirstObjectByType<NavigationField>();
            if (field == null)
            {
                return;
            }

            field.RebuildIfChanged();
            var result = field.GetResult(null);
            foreach (var spawn in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                if (!result.IsReachable(spawn.transform.position))
                {
                    issues.Add(new StudioIssue(StudioIssueSeverity.Error, $"湧き位置 {spawn.ResolvedId} からコアへ行けません",
                        "背景で道が完全に塞がっています。背景を動かすか、すき間を作ってください（要塞デザイナーの「経路・障害物」で道を確認できます）。", spawn));
                }
            }
        }

        private static void ConvertMaterials(StageProp prop)
        {
            var root = StageSceneService.FindRoot();
            StageMaterialConverter.Convert(prop, StageAssetFactory.FolderOf(root != null ? root.current : null));
        }

        /// <summary>根元の X/Y の傾きをなくし、子の見た目の位置・向きはそのまま保つ。</summary>
        private static void StraightenRoot(StageProp prop)
        {
            var t = prop.transform;
            var children = new List<(Transform child, Vector3 position, Quaternion rotation)>();
            foreach (Transform child in t)
            {
                Undo.RecordObject(child, "根元をまっすぐにする");
                children.Add((child, child.position, child.rotation));
            }

            Undo.RecordObject(t, "根元をまっすぐにする");
            t.rotation = Quaternion.Euler(0f, 0f, t.eulerAngles.z);
            foreach (var (child, position, rotation) in children)
            {
                child.SetPositionAndRotation(position, rotation);
            }

            if (prop.footprint != null)
            {
                prop.footprint.transform.localRotation = Quaternion.identity;
            }

            StagePropComposer.RebuildFootprint(prop);
        }
    }
}
