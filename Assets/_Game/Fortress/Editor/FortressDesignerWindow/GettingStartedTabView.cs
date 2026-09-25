using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>「はじめに」タブ。初見の人向けにツールの使い方を説明する。</summary>
    public sealed class GettingStartedTabView : VisualElement
    {
        public GettingStartedTabView(FortressDesignerWindow window)
        {
            AddToClassList("fd-tab-content");

            var title = new Label("Fortress Designer へようこそ");
            title.AddToClassList("fd-section-title");
            Add(title);

            var intro = new Label(
                "「握れ、灼ける前に」の土台システム（砲台配置・握力とレーザーの連動・地形破壊・敵の湧き）を、" +
                "プレイテストしながら調整するためのツールです。数値をいじってPlay Modeで即確認する、を繰り返す想定です。");
            intro.AddToClassList("fd-help-text");
            Add(intro);

            AddStep(
                "1. シーンを準備する",
                "テスト用の要塞シーンが無い場合は、メニューの Tools > Fortress > Build Phase1 Test Scene で" +
                "コア・4基の砲台・敵の湧き位置・サンプルの敵ウェーブを自動生成できます。既存のシーンがある場合は不要です。",
                "テストシーンを自動生成", () => EditorApplication.ExecuteMenuItem("Tools/Fortress/Build Phase1 Test Scene"));

            AddStep(
                "2. 砲台を配置・チューニングする",
                "「砲台配置」タブで4基の砲台の位置や、太さ・熱・オーバーヒートの数値を調整します。" +
                "位置そのものはシーン上でGameObjectを選んで普通に動かすだけでOKです。",
                "砲台配置タブを開く", window.SwitchToTurretsTab);

            AddStep(
                "3. 地形障害物を配置する",
                "「地形破壊」タブで、レーザーを当てるほどひびが濃くなり、限界で崩れ落ちて時間経過で再生する" +
                "障害物を配置できます。健在な間はレーザーを完全に遮るので、まずこれを壊してから奥の敵を狙う" +
                "駆け引きが生まれます。プレイヤーとコアの間を漂う動きや、コアを中心に円形に周回する動きも" +
                "設定で選べます(動かないままにもできます)。",
                "地形破壊タブを開く", window.SwitchToTerrainTab);

            AddStep(
                "4. 敵の湧き方を調整する",
                "「敵ウェーブ」タブで、いつ・どこに・何体・どの種類の敵を湧かせるかを一覧で編集できます。",
                "敵ウェーブタブを開く", window.SwitchToEnemyWavesTab);

            AddStep(
                "5. Play Modeでテストする",
                "「テスト」タブでは、4人分の握力値・熱ゲージ・砲台の状態・障害物のHPや再生タイマーをリアルタイムに確認しながら、" +
                "ウェーブの再生/停止をボタン一つで試せます。実機が無い場合は Grip Input Bridge のシミュレータで代用できます。",
                "Grip Input Bridgeを開く", () => EditorApplication.ExecuteMenuItem("Tools/Grip Input Bridge/Open Window"));

            var note = new Label(
                "ヒント: 太さ・熱の増加量・オーバーヒートしきい値などは全て ScriptableObject (Laser Tuning Config) に" +
                "まとまっているので、コードを触らずに何度でも調整・比較できます。");
            note.AddToClassList("fd-help-text");
            Add(note);
        }

        private void AddStep(string title, string body, string buttonLabel, System.Action onClick)
        {
            var step = new VisualElement();
            step.AddToClassList("fd-getting-started-step");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("fd-getting-started-step-title");
            step.Add(titleLabel);

            var bodyLabel = new Label(body) { style = { whiteSpace = WhiteSpace.Normal } };
            step.Add(bodyLabel);

            if (!string.IsNullOrEmpty(buttonLabel) && onClick != null)
            {
                var button = new Button(onClick) { text = buttonLabel };
                button.AddToClassList("fd-step-button");
                step.Add(button);
            }

            Add(step);
        }
    }
}
