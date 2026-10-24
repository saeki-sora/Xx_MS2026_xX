using System.Collections.Generic;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.Blue.EditorTools
{
    /// <summary>
    /// Scene ビューの目安表示。青スキルの設定ファイル（BlueVortexSkill）を選んでいる間、シーンの全ての砲台について
    /// 「今の向きで投げたら落ちる場所」と渦の大きさを描く（Play していなくても見える）。落ちる場所の調整に使う。
    /// Play 中は出ている渦も描く。
    /// </summary>
    [InitializeOnLoad]
    internal static class BlueSkillSceneGuide
    {
        private static readonly Color GuideColor = new Color(0.3f, 0.75f, 1f, 0.9f);
        private static readonly Color FillColor = new Color(0.2f, 0.5f, 1f, 0.08f);
        private static readonly List<Vector3> Landings = new List<Vector3>(8);

        static BlueSkillSceneGuide()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView view)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (Selection.activeObject is BlueVortexSkill skill)
            {
                DrawLandingGuides(skill);
            }

            if (EditorApplication.isPlaying)
            {
                DrawActiveAreas();
            }
        }

        private static void DrawLandingGuides(BlueVortexSkill skill)
        {
            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var radius = Mathf.Max(0.05f, skill.area.radius * skill.AreaRadiusScale);
            for (var i = 0; i < turrets.Length; i++)
            {
                var turret = turrets[i];
                var count = skill.PredictLandings(turret, Landings);
                var from = turret.MuzzlePosition;
                for (var k = 0; k < count; k++)
                {
                    var landing = Landings[k];
                    Handles.color = GuideColor;
                    Handles.DrawDottedLine(from, landing, 4f);
                    Handles.color = FillColor;
                    Handles.DrawSolidDisc(landing, Vector3.forward, radius);
                    Handles.color = GuideColor;
                    Handles.DrawWireDisc(landing, Vector3.forward, radius, 2f);
                    Handles.DrawWireDisc(landing, Vector3.forward, radius * Mathf.Clamp01(skill.debuff.pullInnerRadius));
                }

                var center = Landings[(count - 1) / 2];
                var label = "P" + (turret.playerIndex + 1) + " 青スキルの渦（半径 " + radius.ToString("0.0") + "m" + (count > 1 ? " × " + count + "個" : "") + "）";
                Handles.Label(center + new Vector3(0f, radius + 0.3f, 0f), label);
            }
        }

        private static void DrawActiveAreas()
        {
            var areas = BlueVortexArea.Active;
            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                if (area == null)
                {
                    continue;
                }

                Handles.color = GuideColor;
                Handles.DrawWireDisc(area.Center, Vector3.forward, Mathf.Max(0.05f, area.CurrentRadius), 2f);
                Handles.Label(area.Center, "渦 P" + (area.OwnerPlayerIndex + 1) + "  つかまっている敵 " + BlueVortexField.AffectedCount);
            }
        }
    }
}
