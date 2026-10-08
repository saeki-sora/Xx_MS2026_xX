using System;
using System.Collections.Generic;
using MS2026.StudioKit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>取り込みの予定表（1行＝1つの物）。取り込むか・役割を行ごとに直せる。</summary>
    public sealed class ImportPlanTable : VisualElement
    {
        private static readonly List<StagePropRole> Roles = new List<StagePropRole>((StagePropRole[])Enum.GetValues(typeof(StagePropRole)));

        public void Show(StageImportPlan plan)
        {
            Clear();
            if (plan == null || plan.Nodes.Count == 0)
            {
                return;
            }

            foreach (var node in plan.Nodes)
            {
                Add(BuildRow(node));
            }
        }

        private static VisualElement BuildRow(StageImportNode node)
        {
            var row = StudioUi.Styled(new VisualElement(), "sk-list-item");
            row.style.height = 30;

            var include = new Toggle { value = node.Include, tooltip = "チェックを外すと取り込みません。" };
            include.RegisterValueChangedCallback(e => node.Include = e.newValue);
            include.style.marginRight = 6;
            row.Add(include);

            var label = StudioUi.Styled(new Label(node.Name), "sk-list-label");
            label.tooltip = string.IsNullOrEmpty(node.Path) ? "モデル全体" : $"モデルの中の場所: {node.Path}";
            row.Add(label);

            if (node.IsMarker)
            {
                row.Add(StudioUi.Chip(MarkerText(node), ChipKind.Info,
                    "目印です。背景にはならず、取り込んだ後に「砲台・コアを目印に合わせる」で使えます。"));
                return row;
            }

            var size = StudioUi.Styled(new Label($"{node.Size.x:0.#}×{node.Size.y:0.#}×高さ{node.Size.z:0.#}"), "sk-list-meta");
            size.tooltip = "取り込んだ後のゲームでの大きさ（横×縦×高さ、ワールド単位）。";
            size.style.width = 120;
            row.Add(size);

            var tris = StudioUi.Styled(new Label(node.TriangleCount > 0 ? $"{node.TriangleCount:N0}面" : "形なし"), "sk-list-meta");
            tris.tooltip = "三角形の数。形が無い物（空の親・ライト等）は取り込みません。";
            tris.style.width = 64;
            row.Add(tris);

            var role = new PopupField<StagePropRole>(Roles, node.Role, r => r.DisplayName(), r => r.DisplayName())
            {
                tooltip = "この物の役割。名前から推測した値です。取り込んだ後でも変えられます。"
            };
            role.style.width = 110;
            role.RegisterValueChangedCallback(e => node.Role = e.newValue);
            row.Add(role);
            return row;
        }

        private static string MarkerText(StageImportNode node) => node.Marker switch
        {
            StageMarkerKind.Core => "目印: コア",
            StageMarkerKind.Turret => $"目印: P{node.MarkerIndex + 1}の砲台",
            StageMarkerKind.Spawn => "目印: 湧き位置",
            _ => "目印"
        };
    }
}
