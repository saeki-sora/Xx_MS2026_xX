using UnityEditor;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「スマッシュボール」タブ。破壊可能物の中から好きな物をスマッシュボール化し、割れたら誰が割ったかを記録・演出できるようにする。
    /// 各パネルは独立していて、このクラスは並べるだけ（他のタブと同じ作り）。
    /// </summary>
    public sealed class SmashBallTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;
        private readonly SmashBallSelectedPanel _selected = new SmashBallSelectedPanel();
        private readonly SmashBallListPanel _list = new SmashBallListPanel();
        private readonly SmashBallHistoryPanel _history = new SmashBallHistoryPanel();

        public SmashBallTabView()
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
            EditorGUILayout.LabelField("スマッシュボール", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "破壊可能物に「スマッシュボール」の機能を追加できます。既定では画面内（経路フィールドの範囲内）を" +
                "無軌道にふわふわ浮遊し、壁にぶつかると向きを変えます（浮遊はOFFにもできます）。割れると、誰が割ったか" +
                "（プレイヤー番号・砲台）を記録し、専用の演出（エフェクト・効果音）を追加で鳴らせます。今のところ割れても" +
                "能力などは発動しませんが、SmashBallModule.AnyBroken を購読すれば、後から誰でも「割れた瞬間」にフックできます。",
                MessageType.None);

            _selected.Draw();
            _list.Draw();
            _history.Draw();
        }
    }
}
