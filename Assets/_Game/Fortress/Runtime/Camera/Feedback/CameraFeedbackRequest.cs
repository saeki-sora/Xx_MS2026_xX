using UnityEngine;

namespace MS2026.Fortress.Cameras
{
    /// <summary>カメラ演出のきっかけになる出来事の種類。増やすときは値を足し、対応する <see cref="ICameraFeedbackSource"/> を書く。</summary>
    public enum CameraFeedbackEvent
    {
        /// <summary>スマッシュボールが割れた。関係者=割ったプレイヤー。</summary>
        SmashBallBroken,

        /// <summary>コアクリスタルがダメージを受けた。関係者なし(全員に関係する)。量=ダメージ量。</summary>
        CoreDamaged,

        /// <summary>砲台がオーバーヒートした。関係者=その砲台のプレイヤー。</summary>
        TurretOverheated
    }

    /// <summary>どの画面で演出を鳴らすか。</summary>
    public enum CameraFeedbackAudience
    {
        /// <summary>全員の画面。関係者以外の強さは別途倍率で弱められる。</summary>
        Everyone,

        /// <summary>関係するプレイヤーの画面だけ(例: 割った本人・オーバーヒートした本人)。</summary>
        RelatedPlayerOnly,

        /// <summary>関係するプレイヤー以外の画面だけ。</summary>
        OthersOnly
    }

    /// <summary>演出の依頼1件分。値型なのでイベントに気軽に乗せられる。</summary>
    public readonly struct CameraFeedbackRequest
    {
        public readonly CameraFeedbackEvent EventType;

        /// <summary>関係するプレイヤー番号(0-3)。いなければ-1。</summary>
        public readonly int RelatedPlayer;

        public readonly Vector2 WorldPosition;
        public readonly bool HasPosition;

        /// <summary>出来事の大きさ(ダメージ量など)。強さの調整に使う。</summary>
        public readonly float Amount;
        public readonly bool HasAmount;

        public CameraFeedbackRequest(CameraFeedbackEvent eventType, int relatedPlayer, Vector2? worldPosition = null, float? amount = null)
        {
            EventType = eventType;
            RelatedPlayer = relatedPlayer;
            HasPosition = worldPosition.HasValue;
            WorldPosition = worldPosition ?? Vector2.zero;
            HasAmount = amount.HasValue;
            Amount = amount ?? 0f;
        }

        public bool HasRelatedPlayer => ViewerIndex.IsPlayer(RelatedPlayer);
    }
}
