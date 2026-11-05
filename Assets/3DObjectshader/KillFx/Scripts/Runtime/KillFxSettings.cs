using MS2026.Fortress;
using UnityEngine;

namespace MS2026.ObjectToon.KillFx
{
    /// <summary>
    /// 敵を倒したときの演出（手描き風エフェクト・効果音・まわりを照らす光）の設定。
    /// 置き場所は Resources/ObjectToon/KillFxSettings.asset（「倒したときの演出」ウィンドウが作る）。
    /// 見た目だけなので通信はしない（各PCが、自分のPCで起きた「敵が倒れた」を見て出す）。
    /// </summary>
    public sealed class KillFxSettings : ScriptableObject
    {
        public const string ResourcePath = "ObjectToon/KillFxSettings";

        [Tooltip("倒したときの演出を出す")]
        public bool enabled = true;

        [Tooltip("トゥーンにするステージ（3Dトゥーン調整の「全体」タブのシーン）を遊んでいるときだけ出す")]
        public bool onlyOnToonStage = true;

        [Header("エフェクト・効果音")]
        [Tooltip("倒したときに出すエフェクト(VFX)と効果音(SE)。VFX は D-Drive に登録した手描き風エフェクト")]
        public FortressEffect onKill = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("1フレームに出すエフェクトの数の上限（群れをまとめて倒したときの負担を抑える）")]
        [Range(1, 10)] public int maxEffectsPerFrame = 2;

        [Tooltip("同じ場所の近くに続けて出さない距離（m）。この距離の中では、下の秒数のあいだ1つだけ")]
        [Range(0f, 5f)] public float minDistance = 1.2f;

        [Tooltip("上の距離の中で、次を出すまで待つ秒数")]
        [Range(0f, 1f)] public float minSeconds = 0.15f;

        [Tooltip("エフェクトを出す高さ（床からの高さ。このゲームでは -Z が上）")]
        [Range(0f, 3f)] public float height = 0.4f;

        [Header("まわりを照らす光")]
        [Tooltip("倒した場所を一瞬照らす（床や3Dの物が照らされる）")]
        public bool light = true;

        [Tooltip("倒した人（プレイヤー）の色で照らす。オフなら下の色")]
        public bool lightUsePlayerColor = true;

        [Tooltip("プレイヤーが分からないとき・プレイヤー色を使わないときの光の色")]
        public Color lightColor = new Color(1f, 0.85f, 0.6f);

        [Tooltip("光の色を白に寄せる量（0=プレイヤー色そのまま）")]
        [Range(0f, 1f)] public float lightWhiteMix = 0.35f;

        [Tooltip("光の強さ（いちばん明るい瞬間）")]
        [Range(0f, 20f)] public float lightIntensity = 6f;

        [Tooltip("光が届く距離（m）")]
        [Range(0.5f, 20f)] public float lightRange = 4f;

        [Tooltip("光が消えるまでの秒数")]
        [Range(0.05f, 2f)] public float lightSeconds = 0.3f;

        [Tooltip("光を浮かせる高さ（m）。高いほど広く、低いほど足元だけ照らす")]
        [Range(0f, 5f)] public float lightHeight = 1.2f;

        [Tooltip("同時に点ける光の数の上限（多いほど重い）")]
        [Range(1, 16)] public int maxLights = 6;

        [Tooltip("近くで光っている光があれば、新しく点けずにその光を明るくし直す距離（m）")]
        [Range(0f, 10f)] public float lightMergeDistance = 2.5f;
    }
}
