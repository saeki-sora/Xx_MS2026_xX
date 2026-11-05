using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「仕掛け」タブ（2026-10-29、ステージ3「歯車ステージ」から）。歯車の回転と障害物の移動をレベルデザインする。
    /// 時間割とプレビュー（<see cref="GimmickTimelinePanel"/>）・歯車（<see cref="GimmickGearPanel"/>）・
    /// 動く障害物（<see cref="GimmickMoverPanel"/>）・点検（<see cref="GimmickCheckPanel"/>）を並べるだけ。
    /// 敵の仕組みは変えない（仕掛けが動くと経路が作り直され、敵は今までどおりそれに従う）。
    /// </summary>
    public sealed class GimmicksTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;
        private readonly GimmickTimelinePanel _timeline = new GimmickTimelinePanel();
        private readonly GimmickGearPanel _gears = new GimmickGearPanel();
        private readonly GimmickMoverPanel _movers = new GimmickMoverPanel();
        private readonly GimmickCheckPanel _check = new GimmickCheckPanel();

        public GimmicksTabView()
        {
            AddToClassList("fd-tab-content");
            _imgui = new IMGUIContainer(OnIMGUI);
            Add(_imgui);
        }

        public void Refresh()
        {
            _imgui.MarkDirtyRepaint();
        }

        private void OnIMGUI()
        {
            EditorGUILayout.LabelField("仕掛け（歯車・動く障害物）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "決まった時刻に回る歯車と、決まった所を行き来する障害物を作ります。時刻は試合の始め（WAVE 1 の発表）が 0 秒で、" +
                "合間（歯みがきタイムなど）も進みます（WAVE CLEAR の後、残りの敵が逃げている間だけ止まります）。動くと敵の経路が作り直され、敵はいつもどおりそれに従ってコアへ向かいます。" +
                "全員のPCで同じ時刻に同じ形になります（通信は時計だけ）。",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                GimmickSceneOverlay.Show = EditorGUILayout.ToggleLeft("Scene に仕掛けを描く", GimmickSceneOverlay.Show, GUILayout.Width(150));
                GimmickSceneOverlay.ShowGhosts = EditorGUILayout.ToggleLeft(
                    new GUIContent("次の形を点線で", "歯車が次に回った後の腕の形・動く障害物の止まる所での形を点線で出します。"),
                    GimmickSceneOverlay.ShowGhosts, GUILayout.Width(130));
            }

            var all = GimmickClock.FindAllInScenes();
            var gears = all.OfType<GimmickGear>().ToList();
            var movers = all.OfType<GimmickMover>().ToList();
            var active = Selection.activeGameObject;
            var selectedGear = active != null ? active.GetComponentInParent<GimmickGear>() : null;
            var selectedMover = active != null ? active.GetComponent<GimmickMover>() : null;

            _timeline.Draw(all);
            EditorGUILayout.Space(6);
            _gears.Draw(gears, selectedGear);
            _movers.Draw(movers, selectedMover);
            _check.Draw(_timeline.Horizon);

            if (all.Count == 0)
            {
                EditorGUILayout.HelpBox("このシーンには仕掛けがありません。「＋ 歯車を置く」か、障害物を選んで「動かせるようにする」を押してください。", MessageType.Info);
            }
        }
    }
}
