using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// スマッシュボールが画面内を無軌道にふわふわ浮遊する動きの設定（スマブラのアイテムのイメージ）。
    /// OFFにすれば、置いた場所から動かない従来どおりの破壊可能物として使える。
    /// </summary>
    [Serializable]
    public sealed class SmashBallFloatSettings
    {
        [Tooltip("ONの間、画面内を無軌道にふわふわ浮遊する。OFFなら置いた場所から動かない。")]
        public bool enabled = true;

        [Header("漂う動き")]
        [Tooltip("漂う速さ(ワールド単位/秒)。")]
        [Min(0f)]
        public float driftSpeed = 1.8f;

        [Tooltip("次にどちらへ向かうかを選び直すまでの間隔(秒)の範囲。毎回この範囲からランダムに決める。")]
        public Vector2 directionChangeInterval = new Vector2(1.2f, 3f);

        [Tooltip("方向転換の滑らかさ。大きいほど素早く新しい向きに切り替わり、小さいほどゆっくり曲がる。")]
        [Range(0.1f, 10f)]
        public float turnSharpness = 2.5f;

        [Header("ふわふわ（上下の揺れ）")]
        [Tooltip("上下に揺れる幅(ワールド単位)。")]
        [Min(0f)]
        public float bobAmplitude = 0.15f;

        [Tooltip("上下に揺れる速さ(往復/秒)。")]
        [Min(0.05f)]
        public float bobFrequency = 1.1f;

        [Header("壁・障害物を避ける")]
        [Tooltip("これに触れそうになったら向きを変えて避ける（壁・破壊可能物など）。")]
        public LayerMask obstacleLayerMask = ~0;

        [Tooltip("避ける判定に使う半径(ワールド単位)。0以下ならこのオブジェクトの大きさから自動で決める。")]
        public float avoidanceRadius;

        [Header("浮遊する範囲")]
        [Tooltip("ONなら経路フィールド(NavigationField)の範囲内を漂う。OFFなら下の範囲を使う。")]
        public bool useNavigationFieldBounds = true;

        [Tooltip("経路フィールドが無いとき、またはONにしていないときに使う範囲の中心。")]
        public Vector2 fallbackAreaCenter = Vector2.zero;

        [Tooltip("経路フィールドが無いとき、またはONにしていないときに使う範囲の大きさ。")]
        public Vector2 fallbackAreaSize = new Vector2(20f, 12f);

        [Header("画面内に戻る")]
        [Tooltip("ONなら、全員の画面に映る範囲(カメラの視点セットから計算)の外へ出ても、少し経つと画面内へ戻ってくる。" +
                 "浮遊する範囲が画面より広いときに、画面外を漂い続けて見えなくなるのを防ぐ。")]
        public bool returnToScreen = true;

        [Tooltip("画面外にいてよい時間(秒)。これを過ぎると画面内へ向かって戻り始める。0ならはみ出した瞬間に戻り始める。")]
        [Min(0f)]
        public float maxSecondsOffScreen = 1f;

        [Tooltip("画面の端からこの距離(ワールド単位)より内側を「画面内」とみなす。ボールが半分見切れた状態を画面内と数えないため。")]
        [Min(0f)]
        public float screenMargin = 1f;

        [Tooltip("画面内へ戻るときの速さの倍率(漂う速さに掛ける)。")]
        [Min(0.1f)]
        public float returnSpeedMultiplier = 1.5f;
    }
}
