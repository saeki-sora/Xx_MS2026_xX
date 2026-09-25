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

            var title = new Label("要塞デザイナーへようこそ");
            title.AddToClassList("fd-section-title");
            Add(title);

            var intro = new Label(
                "「握れ、灼ける前に」の土台システム（砲台配置・握力とレーザーの連動・敵の湧き）を、" +
                "プレイテストしながら調整するためのツールです。数値をいじってPlay Modeで即確認する、を繰り返す想定です。");
            intro.AddToClassList("fd-help-text");
            Add(intro);

            AddStep(
                "1. シーンを準備する",
                "テスト用の要塞シーンが無い場合は、メニューの Tools > 要塞 > フェーズ1テストシーンを生成 で" +
                "コア・4基の砲台・敵の湧き位置・サンプルの敵ウェーブを自動生成できます。既存のシーンがある場合は不要です。",
                "テストシーンを自動生成", () => EditorApplication.ExecuteMenuItem("Tools/要塞/フェーズ1テストシーンを生成"));

            AddStep(
                "2. 砲台を配置・チューニングする",
                "「砲台配置」タブで、4基の砲台の位置、太さ・熱・オーバーヒート、自動回転（射出中は強さに応じて減速）、照準線の数値を調整します。" +
                "位置そのものはシーン上でGameObjectを選んで普通に動かすだけでOKです。",
                "砲台配置タブを開く", window.SwitchToTurretsTab);

            AddStep(
                "3. 敵の湧き方を調整する",
                "「敵ウェーブ」タブで、いつ・どこに・何体・どの種類の敵を湧かせるかを一覧で編集できます。",
                "敵ウェーブタブを開く", window.SwitchToEnemyWavesTab);

            AddStep(
                "4. 障害物と敵の経路を作る",
                "「経路・障害物」タブで、破壊可能なブロック・壊れない壁・通行コスト地帯を置き、" +
                "敵の迂回ルートをSceneに表示して確認できます。コアへ到達できない湧き位置は自動で警告されます。",
                "経路・障害物タブを開く", window.SwitchToNavigationTab);

            AddStep(
                "5. 破壊可能物を置く",
                "「破壊可能物」タブで、レーザーで壊せて時間で再生するオブジェクトを、Sceneをクリック/ドラッグして好きな場所・大きさで置けます。" +
                "見た目はPrefabで差し替え可能（2D/3D）。ダメージで変わる見た目・エフェクト・ドロップ・グループ連動なども調整できます。",
                "破壊可能物タブを開く", window.SwitchToDestructiblesTab);

            AddStep(
                "6. スマッシュボールを仕込む",
                "「スマッシュボール」タブで、好きな破壊可能物にスマッシュボール機能を追加できます。割ると誰が割ったか（プレイヤー番号・砲台）を記録し、" +
                "専用の演出を追加で鳴らせます。今は記録だけですが、割れた瞬間のイベントは後から誰でも拾えるようにしてあります。",
                "スマッシュボールタブを開く", window.SwitchToSmashBallsTab);

            AddStep(
                "7. 数千体の群衆を調整する",
                "「群衆」タブで、敵同士の押し合い・渋滞のしやすさ（流体らしさ）を調整し、" +
                "ストレステストで何体まで60FPSを保てるか計測できます。敵の種類（大きさ・重さ・アニメ）もここで編集します。",
                "群衆タブを開く", window.SwitchToSwarmTab);

            AddStep(
                "8. Play Modeでテストする",
                "「テスト」タブでは、4人分の握力値・熱ゲージ・砲台の状態をリアルタイムに確認しながら、" +
                "ウェーブの再生/停止をボタン一つで試せます。実機が無い場合は 握力入力ブリッジのシミュレータで代用できます。",
                "握力入力ブリッジを開く", () => EditorApplication.ExecuteMenuItem("Tools/握力入力ブリッジ/ウィンドウを開く"));

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
