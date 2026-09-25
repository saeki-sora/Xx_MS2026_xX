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
    }
}
