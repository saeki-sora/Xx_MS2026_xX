using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「破壊可能物」タブ。好きな場所・大きさで破壊可能物を置き、耐久・見た目（Prefab差し替え）・演出・ドロップ・連動などを
    /// 分かりやすく調整する。各パネルは独立していて、このクラスは並べるだけ。
    /// </summary>
    public sealed class DestructiblesTabView : VisualElement
    {
        private readonly IMGUIContainer _imgui;
        private readonly DestructiblePlacementPanel _placement = new DestructiblePlacementPanel();
        private readonly DestructibleSelectedPanel _selected = new DestructibleSelectedPanel();
        private readonly DestructibleListPanel _list = new DestructibleListPanel();
        private readonly DestructiblePresetPanel _presets = new DestructiblePresetPanel();

        public DestructiblesTabView()
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
            EditorGUILayout.LabelField("破壊可能物", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "レーザーで壊せて、壊れると敵の経路が開く（再生すると塞がる）オブジェクトを、マップの好きな場所に好きな大きさで置けます。" +
                "見た目はPrefab（2Dスプライトでも3Dモデルでも）を後から差し替えられます。" +
                "壊れていく途中の見た目・エフェクト・ドロップ・グループ連動・連鎖・無敵条件などもここで調整できます。",
                MessageType.None);

            DrawNavigationWarning();
            _placement.Draw();
            _selected.Draw();
            _list.Draw();
            _presets.Draw();
        }

        /// <summary>経路フィールドが無いと敵は障害物を認識せず、素通りしてしまう。</summary>
        private static void DrawNavigationWarning()
        {
            if (Object.FindFirstObjectByType<NavigationField>() != null)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "シーンに経路フィールド（NavigationField）がないため、敵は破壊可能物を認識せず素通りします。" +
                "破壊可能物を置くと自動で作られますが、先に作ることもできます。",
                MessageType.Warning);
            if (GUILayout.Button("経路フィールドを作成"))
            {
                NavigationFieldTools.CreateField();
            }
        }
    }
}
