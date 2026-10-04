using MS2026.Fortress.Cameras;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>演出まわりの列挙値の表示名。</summary>
    public static class CameraFeedbackLabels
    {
        public static string Event(CameraFeedbackEvent eventType) => eventType switch
        {
            CameraFeedbackEvent.SmashBallBroken => "スマッシュボール破壊",
            CameraFeedbackEvent.CoreDamaged => "コア被弾",
            CameraFeedbackEvent.TurretOverheated => "オーバーヒート",
            _ => eventType.ToString()
        };

        public static string EventDescription(CameraFeedbackEvent eventType) => eventType switch
        {
            CameraFeedbackEvent.SmashBallBroken => "関係者 = 割ったプレイヤー。位置 = 割れた場所。",
            CameraFeedbackEvent.CoreDamaged => "関係者なし(全員に同じ強さ)。大きさ = ダメージ量。位置 = コア。",
            CameraFeedbackEvent.TurretOverheated => "関係者 = オーバーヒートした砲台のプレイヤー。位置 = その砲台。",
            _ => string.Empty
        };

        public static readonly string[] Audiences = { "全員の画面", "関係するプレイヤーの画面だけ", "関係するプレイヤー以外の画面だけ" };

        public static readonly string[] ShakeBackends = { "簡易揺れ（アセット不要）", "D-Driveの揺れアセット" };
    }
}
