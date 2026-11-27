using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 砲台の熱を目に見せる設定（2026-11-27、テストプレイで「オーバーヒートが分からない」と多く言われたため。見本の案 A）。
    /// ・ビームの芯: 外側はプレイヤーの色のまま、真ん中の芯だけが熱で 白→黄→橙→赤 に変わり、熱いほど太くなる。
    /// ・砲台の輪: 砲台のまわりの床に、熱のゲージの輪を出す。オーバーヒート中は灰色の輪が減っていき、いつ戻るか分かる。
    /// ・「熱が危ない」（演出の heatWarnAt）を越えると、芯と輪が点滅し、熱いほど点滅が速くなる。
    /// 見た目だけ（当たり判定・ダメージ・熱の計算は変えない）。熱はネット対戦の Client にも Host から届くので、通信は増やさない。
    /// </summary>
    [Serializable]
    public sealed class LaserHeatLookSettings
    {
        [Tooltip("OFFにすると、芯の色・輪・点滅を出さない（前の見た目に戻る）。")]
        public bool enabled = true;

        [Header("ビームの芯")]
        [Tooltip("熱（左＝0、右＝オーバーヒート）ごとの芯の色。外側のジェルはプレイヤーの色のまま。")]
        public Gradient coreColors = DefaultCoreColors();

        [Range(0f, 1f), Tooltip("芯に色を付ける強さ。0 なら前と同じ白い芯。")]
        public float coreTintStrength = 1f;

        [Range(0f, 0.6f), Tooltip("オーバーヒート直前に、芯がどれだけ太くなるか（ビームの幅に対する割合を足す）。")]
        public float coreWidenAtFull = 0.22f;

        [Header("点滅（熱が危ない所を越えたら）")]
        [Min(0f), Tooltip("危ない所を越えた直後の点滅の速さ（回/秒）。")]
        public float blinkPerSecondStart = 3f;

        [Min(0f), Tooltip("オーバーヒート直前の点滅の速さ（回/秒）。")]
        public float blinkPerSecondEnd = 9f;

        [Range(0f, 1f), Tooltip("点滅でどれだけ暗くなるか（0＝点滅しない、1＝消えるまで暗くなる）。")]
        public float blinkDepth = 0.6f;

        [Header("砲台の輪（熱のゲージ）")]
        [Tooltip("OFFにすると輪を出さない（芯の色と点滅だけになる）。")]
        public bool showRing = true;

        [Min(0.05f), Tooltip("輪の半径（メートル）。砲台の絵（半径 約1.15m）と足元の影より外に出す。")]
        public float ringRadius = 1.5f;

        [Min(0.005f), Tooltip("輪の太さ（メートル）。")]
        public float ringWidth = 0.16f;

        [Tooltip("輪の下地（まだ溜まっていない所）の色。溜まった所より少し太く描くので縁取りにもなる。" +
                 "明るい床でも輪が読めるよう、暗い色にしておく。")]
        public Color ringTrackColor = new Color(0.08f, 0.1f, 0.12f, 0.45f);

        [Min(1f), Tooltip("下地の太さ（溜まった所の太さに対する倍率）。1 より大きいと、溜まった所のまわりに縁取りが付く。")]
        public float ringTrackWidthScale = 1.6f;

        [Range(0f, 1f), Tooltip("熱がこれより少ないときは輪を消す（いつも出ているとうるさいので）。")]
        public float ringShowFrom = 0.03f;

        [Min(0.1f), Tooltip("輪が出る・消えるときの速さ（大きいほど早く切り替わる）。")]
        public float ringFadeSpeed = 6f;

        [Tooltip("オーバーヒート中の輪の色（止まっている残り時間ぶんが減っていく）。")]
        public Color overheatRingColor = new Color(0.42f, 0.45f, 0.5f, 0.95f);

        public static Gradient DefaultCoreColors()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.89f, 0.42f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.54f, 0.16f), 0.8f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.14f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
